using System.Reflection;
using System.Text.Json;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Address Enrichment API",
        Version = "v1",
        Description = "Server-side API for postal boundary lookup and related address enrichment services."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHttpClient();

var app = builder.Build();

if (!app.Urls.Any())
{
    app.Urls.Add("http://localhost:5080");
}

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/config", (IConfiguration config) =>
{
    var mapsApiKey = config["Google:MapsApiKey"];
    if (string.IsNullOrWhiteSpace(mapsApiKey))
    {
        return Results.Problem("Google Maps API key is not configured on the server.", statusCode: 500);
    }

    return Results.Ok(new ClientConfigResponse(
        mapsApiKey,
        config["Google:MapId"] ?? string.Empty));
})
.WithName("GetClientConfig")
.WithSummary("Get UI runtime configuration")
.WithDescription("Returns browser-consumable runtime configuration such as the Google Maps JavaScript API key.")
.Produces<ClientConfigResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status500InternalServerError);

app.MapGet("/api/postal-boundary-target", async (
    string postalCode,
    string iso2,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(postalCode) || string.IsNullOrWhiteSpace(iso2))
    {
        return Results.BadRequest(new { error = "Both postalCode and iso2 are required." });
    }

    var googleApiKey = config["Google:ApiKey"];
    if (string.IsNullOrWhiteSpace(googleApiKey))
    {
        logger.LogError("Google API key is not configured.");
        return Results.Problem("Google API key is not configured on the server.", statusCode: 500);
    }

    var geocodeUrl =
        "https://maps.googleapis.com/maps/api/geocode/json" +
        $"?components=country:{Uri.EscapeDataString(iso2.Trim())}%7Cpostal_code:{Uri.EscapeDataString(postalCode.Trim())}" +
        $"&key={Uri.EscapeDataString(googleApiKey)}";

    using var client = httpClientFactory.CreateClient();
    using var response = await client.GetAsync(geocodeUrl);
    var bodyText = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        logger.LogWarning("Google Geocoder failed with status {StatusCode}: {Body}", (int)response.StatusCode, bodyText);
        return Results.Problem(
            $"Google Geocoder {(int)response.StatusCode}: {TrimForError(bodyText)}",
            statusCode: (int)response.StatusCode);
    }

    using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText);
    logger.LogInformation("Google postal boundary geocoding lookup succeeded for {PostalCode} {Iso2}", postalCode, iso2);

    var boundaryTarget = ApiResponseMapper.BuildPostalBoundaryTarget(document.RootElement);
    if (boundaryTarget is not null)
    {
        return Results.Ok(boundaryTarget);
    }

    var placesUrl = "https://places.googleapis.com/v1/places:searchText";
    using var placesRequest = new HttpRequestMessage(HttpMethod.Post, placesUrl)
    {
        Content = JsonContent.Create(new
        {
            textQuery = $"{postalCode.Trim()} {iso2.Trim()}",
            maxResultCount = 5
        })
    };
    placesRequest.Headers.Add("X-Goog-Api-Key", googleApiKey);
    placesRequest.Headers.Add("X-Goog-FieldMask", "places.id,places.formattedAddress,places.location,places.types");

    using var placesResponse = await client.SendAsync(placesRequest);
    var placesBodyText = await placesResponse.Content.ReadAsStringAsync();

    if (!placesResponse.IsSuccessStatusCode)
    {
        logger.LogWarning("Google Places Text Search failed with status {StatusCode}: {Body}", (int)placesResponse.StatusCode, placesBodyText);
        return Results.Problem(
            $"Google Places Text Search {(int)placesResponse.StatusCode}: {TrimForError(placesBodyText)}",
            statusCode: (int)placesResponse.StatusCode);
    }

    using var placesDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(placesBodyText) ? "{}" : placesBodyText);
    logger.LogInformation("Google postal boundary places fallback lookup succeeded for {PostalCode} {Iso2}", postalCode, iso2);

    boundaryTarget = ApiResponseMapper.BuildPostalBoundaryTargetFromPlaces(placesDocument.RootElement);
    if (boundaryTarget is not null)
    {
        return Results.Ok(boundaryTarget);
    }

    var candidates = ApiResponseMapper.BuildPostalBoundaryCandidates(document.RootElement);
    candidates.AddRange(ApiResponseMapper.BuildPostalBoundaryCandidatesFromPlaces(placesDocument.RootElement));

    return Results.NotFound(new PostalBoundaryLookupFailureResponse(
        "No Google postal boundary target was found for this postcode/country.",
        candidates));
})
.WithName("GetPostalBoundaryTarget")
.WithSummary("Get postal boundary target")
.WithDescription("Looks up a Google place ID and viewport for a postal code so the UI can render Google's POSTAL_CODE boundary layer.")
.Produces<PostalBoundaryTargetResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status500InternalServerError);

app.MapPost("/api/address-validation", async (
    AddressValidationRequest request,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Address) ||
        string.IsNullOrWhiteSpace(request.City) ||
        string.IsNullOrWhiteSpace(request.Iso2))
    {
        return Results.BadRequest(new ErrorResponse("Address, city, and iso2 are required."));
    }

    var googleApiKey = config["Google:ApiKey"];
    if (string.IsNullOrWhiteSpace(googleApiKey))
    {
        logger.LogError("Google API key is not configured.");
        return Results.Problem("Google API key is not configured on the server.", statusCode: 500);
    }

    var url = $"https://addressvalidation.googleapis.com/v1:validateAddress?key={Uri.EscapeDataString(googleApiKey)}";
    var payload = new
    {
        address = new
        {
            addressLines = new[] { request.Address },
            locality = request.City,
            administrativeArea = string.IsNullOrWhiteSpace(request.State) ? null : request.State.Trim(),
            regionCode = request.Iso2
        }
    };

    using var client = httpClientFactory.CreateClient();
    using var response = await client.PostAsJsonAsync(url, payload);
    var bodyText = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        logger.LogWarning("Address Validation failed with status {StatusCode}: {Body}", (int)response.StatusCode, bodyText);
        return Results.Problem(ApiResponseMapper.ExtractGoogleError(bodyText, $"Address Validation API {(int)response.StatusCode}"), statusCode: (int)response.StatusCode);
    }

    using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText);
    var result = ApiResponseMapper.BuildValidationResult(document.RootElement);
    return Results.Ok(result);
})
.WithName("ValidateAddress")
.WithSummary("Validate address")
.WithDescription("Calls the Google Address Validation API and returns normalized address, granularity, and geocode data.")
.Produces<ValidationResultResponse>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status500InternalServerError);

app.MapPost("/api/places/nearby-search", async (
    PlacesNearbySearchRequest request,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Type))
    {
        return Results.BadRequest(new ErrorResponse("type is required."));
    }

    return await ExecutePlacesSearchAsync(
        config,
        httpClientFactory,
        logger,
        "https://places.googleapis.com/v1/places:searchNearby",
        new
        {
            includedTypes = new[] { request.Type },
            maxResultCount = 10,
            locationRestriction = new
            {
                circle = new
                {
                    center = new { latitude = request.Lat, longitude = request.Lng },
                    radius = 50000.0
                }
            }
        });
})
.WithName("NearbySearch")
.WithSummary("Search nearby places")
.WithDescription("Calls Google Places Nearby Search to find places of a given type within 50 km.")
.Produces<List<PlaceResultResponse>>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status500InternalServerError);

app.MapPost("/api/places/text-search", async (
    PlacesTextSearchRequest request,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    var delta = 0.45;
    return await ExecutePlacesSearchAsync(
        config,
        httpClientFactory,
        logger,
        "https://places.googleapis.com/v1/places:searchText",
        new
        {
            textQuery = string.IsNullOrWhiteSpace(request.Keyword) ? "port harbor seaport" : request.Keyword,
            maxResultCount = 10,
            locationRestriction = new
            {
                rectangle = new
                {
                    low = new { latitude = request.Lat - delta, longitude = request.Lng - delta },
                    high = new { latitude = request.Lat + delta, longitude = request.Lng + delta }
                }
            }
        });
})
.WithName("TextSearch")
.WithSummary("Search places by text")
.WithDescription("Calls Google Places Text Search with a bounded location rectangle around the address coordinates.")
.Produces<List<PlaceResultResponse>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status500InternalServerError);

app.Run();

static string TrimForError(string value)
{
    return value.Length <= 200 ? value : value[..200];
}

static async Task<IResult> ExecutePlacesSearchAsync(
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    string url,
    object payload)
{
    var googleApiKey = config["Google:ApiKey"];
    if (string.IsNullOrWhiteSpace(googleApiKey))
    {
        logger.LogError("Google API key is not configured.");
        return Results.Problem("Google API key is not configured on the server.", statusCode: 500);
    }

    using var request = new HttpRequestMessage(HttpMethod.Post, url)
    {
        Content = JsonContent.Create(payload)
    };
    request.Headers.Add("X-Goog-Api-Key", googleApiKey);
    request.Headers.Add("X-Goog-FieldMask", "places.id,places.displayName,places.location");

    using var client = httpClientFactory.CreateClient();
    using var response = await client.SendAsync(request);
    var bodyText = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        logger.LogWarning("Places API failed with status {StatusCode}: {Body}", (int)response.StatusCode, bodyText);
        return Results.Problem(ApiResponseMapper.ExtractGoogleError(bodyText, $"Places API {(int)response.StatusCode}"), statusCode: (int)response.StatusCode);
    }

    using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText);
    var places = ApiResponseMapper.BuildPlacesResult(document.RootElement);
    return Results.Ok(places);
}

/// <summary>
/// Entry point marker for integration testing.
/// </summary>
public partial class Program;
