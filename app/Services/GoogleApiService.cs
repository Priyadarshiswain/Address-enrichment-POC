using System.Text.Json;

namespace AddressEnrichment.Api.Services;

public interface IGoogleApiService
{
    ClientConfigResponse GetClientConfig();
    Task<PostalBoundaryTargetLookupResult> GetPostalBoundaryTargetAsync(string postalCode, string iso2, CancellationToken cancellationToken = default);
    Task<ValidationResultResponse> ValidateAddressAsync(AddressValidationRequest request, CancellationToken cancellationToken = default);
    Task<List<PlaceResultResponse>> NearbySearchAsync(PlacesNearbySearchRequest request, CancellationToken cancellationToken = default);
    Task<List<PlaceResultResponse>> TextSearchAsync(PlacesTextSearchRequest request, CancellationToken cancellationToken = default);
}

public sealed class GoogleApiService(
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<GoogleApiService> logger) : IGoogleApiService
{
    public ClientConfigResponse GetClientConfig()
    {
        var mapsApiKey = config["Google:MapsApiKey"];
        if (string.IsNullOrWhiteSpace(mapsApiKey))
        {
            throw new InvalidOperationException("Google Maps API key is not configured on the server.");
        }

        return new ClientConfigResponse(mapsApiKey, config["Google:MapId"] ?? string.Empty);
    }

    public async Task<PostalBoundaryTargetLookupResult> GetPostalBoundaryTargetAsync(string postalCode, string iso2, CancellationToken cancellationToken = default)
    {
        EnsureGoogleApiKeyConfigured();

        using var client = httpClientFactory.CreateClient();

        var geocodeUrl =
            "https://maps.googleapis.com/maps/api/geocode/json" +
            $"?components=country:{Uri.EscapeDataString(iso2.Trim())}%7Cpostal_code:{Uri.EscapeDataString(postalCode.Trim())}" +
            $"&key={Uri.EscapeDataString(GetGoogleApiKey())}";

        using var geocodeResponse = await client.GetAsync(geocodeUrl, cancellationToken);
        var geocodeBody = await geocodeResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!geocodeResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Google Geocoder failed with status {StatusCode}: {Body}", (int)geocodeResponse.StatusCode, geocodeBody);
            throw new UpstreamApiException(
                $"Google Geocoder {(int)geocodeResponse.StatusCode}: {TrimForError(geocodeBody)}",
                (int)geocodeResponse.StatusCode);
        }

        using var geocodeDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(geocodeBody) ? "{}" : geocodeBody);
        logger.LogInformation("Google postal boundary geocoding lookup succeeded for {PostalCode} {Iso2}", postalCode, iso2);

        var boundaryTarget = ApiResponseMapper.BuildPostalBoundaryTarget(geocodeDocument.RootElement);
        if (boundaryTarget is not null)
        {
            return PostalBoundaryTargetLookupResult.Success(boundaryTarget);
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
        placesRequest.Headers.Add("X-Goog-Api-Key", GetGoogleApiKey());
        placesRequest.Headers.Add("X-Goog-FieldMask", "places.id,places.formattedAddress,places.location,places.types");

        using var placesResponse = await client.SendAsync(placesRequest, cancellationToken);
        var placesBody = await placesResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!placesResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Google Places Text Search failed with status {StatusCode}: {Body}", (int)placesResponse.StatusCode, placesBody);
            throw new UpstreamApiException(
                $"Google Places Text Search {(int)placesResponse.StatusCode}: {TrimForError(placesBody)}",
                (int)placesResponse.StatusCode);
        }

        using var placesDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(placesBody) ? "{}" : placesBody);
        logger.LogInformation("Google postal boundary places fallback lookup succeeded for {PostalCode} {Iso2}", postalCode, iso2);

        boundaryTarget = ApiResponseMapper.BuildPostalBoundaryTargetFromPlaces(placesDocument.RootElement);
        if (boundaryTarget is not null)
        {
            return PostalBoundaryTargetLookupResult.Success(boundaryTarget);
        }

        var candidates = ApiResponseMapper.BuildPostalBoundaryCandidates(geocodeDocument.RootElement);
        candidates.AddRange(ApiResponseMapper.BuildPostalBoundaryCandidatesFromPlaces(placesDocument.RootElement));

        return PostalBoundaryTargetLookupResult.NotFound(new PostalBoundaryLookupFailureResponse(
            "No Google postal boundary target was found for this postcode/country.",
            candidates));
    }

    public async Task<ValidationResultResponse> ValidateAddressAsync(AddressValidationRequest request, CancellationToken cancellationToken = default)
    {
        EnsureGoogleApiKeyConfigured();

        var url = $"https://addressvalidation.googleapis.com/v1:validateAddress?key={Uri.EscapeDataString(GetGoogleApiKey())}";
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
        using var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
        var bodyText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Address Validation failed with status {StatusCode}: {Body}", (int)response.StatusCode, bodyText);
            throw new UpstreamApiException(
                ApiResponseMapper.ExtractGoogleError(bodyText, $"Address Validation API {(int)response.StatusCode}"),
                (int)response.StatusCode);
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText);
        return ApiResponseMapper.BuildValidationResult(document.RootElement);
    }

    public Task<List<PlaceResultResponse>> NearbySearchAsync(PlacesNearbySearchRequest request, CancellationToken cancellationToken = default)
    {
        return ExecutePlacesSearchAsync(
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
            },
            cancellationToken);
    }

    public Task<List<PlaceResultResponse>> TextSearchAsync(PlacesTextSearchRequest request, CancellationToken cancellationToken = default)
    {
        const double delta = 0.45;

        return ExecutePlacesSearchAsync(
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
            },
            cancellationToken);
    }

    private async Task<List<PlaceResultResponse>> ExecutePlacesSearchAsync(string url, object payload, CancellationToken cancellationToken)
    {
        EnsureGoogleApiKeyConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Goog-Api-Key", GetGoogleApiKey());
        request.Headers.Add("X-Goog-FieldMask", "places.id,places.displayName,places.location");

        using var client = httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        var bodyText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Places API failed with status {StatusCode}: {Body}", (int)response.StatusCode, bodyText);
            throw new UpstreamApiException(
                ApiResponseMapper.ExtractGoogleError(bodyText, $"Places API {(int)response.StatusCode}"),
                (int)response.StatusCode);
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText);
        return ApiResponseMapper.BuildPlacesResult(document.RootElement);
    }

    private string GetGoogleApiKey() => config["Google:ApiKey"] ?? string.Empty;

    private void EnsureGoogleApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(GetGoogleApiKey()))
        {
            logger.LogError("Google API key is not configured.");
            throw new InvalidOperationException("Google API key is not configured on the server.");
        }
    }

    private static string TrimForError(string value)
    {
        return value.Length <= 200 ? value : value[..200];
    }
}
