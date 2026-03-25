using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AddressEnrichment.Api.Tests.Integration;

public class ApiEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory factory;
    private readonly HttpClient client;

    public ApiEndpointsTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetConfig_ReturnsBrowserConfiguration()
    {
        var result = await client.GetFromJsonAsync<ClientConfigResponse>("/api/config");

        Assert.NotNull(result);
        Assert.Equal("test-maps-key", result!.MapsApiKey);
        Assert.Equal("test-map-id", result.MapId);
    }

    [Fact]
    public async Task GetPostalBoundaryTarget_ReturnsMappedPostalCodeResult()
    {
        factory.When(
            HttpMethod.Get,
            "https://maps.googleapis.com/maps/api/geocode/json?components=country:IN|postal_code:560068&key=test-google-key",
            Json(HttpStatusCode.OK, """
            {
              "results": [
                {
                  "types": ["postal_code"],
                  "place_id": "postal-place-id",
                  "formatted_address": "560068, Bengaluru, Karnataka, India",
                  "geometry": {
                    "location": { "lat": 12.9121, "lng": 77.6446 },
                    "viewport": {
                      "northeast": { "lat": 12.99, "lng": 77.70 },
                      "southwest": { "lat": 12.85, "lng": 77.58 }
                    }
                  }
                }
              ]
            }
            """));

        var response = await client.GetAsync("/api/postal-boundary-target?postalCode=560068&iso2=IN");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        var result = await response.Content.ReadFromJsonAsync<PostalBoundaryTargetResponse>();

        Assert.NotNull(result);
        Assert.Equal("postal-place-id", result!.PlaceId);
        Assert.Equal(12.9121, result.Location.Lat);
    }

    [Fact]
    public async Task GetPostalBoundaryTarget_FallsBackToPlacesTextSearch_WhenGeocodeReturnsNoResults()
    {
        factory.When(
            HttpMethod.Get,
            "https://maps.googleapis.com/maps/api/geocode/json?components=country:IN|postal_code:560068&key=test-google-key",
            Json(HttpStatusCode.OK, """
            {
              "results": []
            }
            """));

        factory.When(
            HttpMethod.Post,
            "https://places.googleapis.com/v1/places:searchText",
            Json(HttpStatusCode.OK, """
            {
              "places": [
                {
                  "id": "places-postal-1",
                  "formattedAddress": "560068, Bengaluru, Karnataka, India",
                  "types": ["postal_code", "locality"],
                  "location": {
                    "latitude": 12.9121,
                    "longitude": 77.6446
                  }
                }
              ]
            }
            """));

        var response = await client.GetAsync("/api/postal-boundary-target?postalCode=560068&iso2=IN");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        var result = await response.Content.ReadFromJsonAsync<PostalBoundaryTargetResponse>();

        Assert.NotNull(result);
        Assert.Equal("places-postal-1", result!.PlaceId);
    }

    [Fact]
    public async Task AddressValidation_ReturnsBadRequest_WhenRequiredFieldsAreMissing()
    {
        var response = await client.PostAsJsonAsync("/api/address-validation", new
        {
            address = "",
            city = "",
            state = "",
            iso2 = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(result);
        Assert.Equal("Address, city, and iso2 are required.", result!.Error);
    }

    [Fact]
    public async Task AddressValidation_ReturnsMappedResult_WhenGoogleSucceeds()
    {
        factory.When(
            HttpMethod.Post,
            "https://addressvalidation.googleapis.com/v1:validateAddress?key=test-google-key",
            Json(HttpStatusCode.OK, """
            {
              "result": {
                "verdict": {
                  "validationGranularity": "PREMISE",
                  "geocodeGranularity": "PREMISE",
                  "addressComplete": true
                },
                "address": {
                  "formattedAddress": "12 Main St, Bengaluru 560068, India",
                  "postalAddress": {
                    "postalCode": "560068"
                  }
                },
                "geocode": {
                  "location": {
                    "latitude": 12.9121,
                    "longitude": 77.6446
                  }
                }
              }
            }
            """));

        var response = await client.PostAsJsonAsync("/api/address-validation", new
        {
            address = "12 Main St",
            city = "Bengaluru",
            state = "Karnataka",
            iso2 = "IN"
        });

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        var result = await response.Content.ReadFromJsonAsync<ValidationResultResponse>();

        Assert.NotNull(result);
        Assert.Equal("560068", result!.PostalCode);
        Assert.Equal("PREMISE", result.Granularity);
    }

    [Fact]
    public async Task AddressValidation_ReturnsUpstreamErrorMessage_WhenGoogleFails()
    {
        factory.When(
            HttpMethod.Post,
            "https://addressvalidation.googleapis.com/v1:validateAddress?key=test-google-key",
            Json(HttpStatusCode.BadRequest, """
            {
              "error": {
                "message": "Invalid address"
              }
            }
            """));

        var response = await client.PostAsJsonAsync("/api/address-validation", new
        {
            address = "bad",
            city = "Bengaluru",
            state = "Karnataka",
            iso2 = "IN"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(result);
        Assert.Equal("Invalid address", result!.Detail);
    }

    [Fact]
    public async Task NearbySearch_ReturnsMappedPlaces()
    {
        factory.When(
            HttpMethod.Post,
            "https://places.googleapis.com/v1/places:searchNearby",
            Json(HttpStatusCode.OK, """
            {
              "places": [
                {
                  "id": "airport-1",
                  "displayName": { "text": "Kempegowda International Airport" },
                  "location": { "latitude": 13.1986, "longitude": 77.7066 }
                }
              ]
            }
            """));

        var response = await client.PostAsJsonAsync("/api/places/nearby-search", new
        {
            lat = 12.9121,
            lng = 77.6446,
            type = "airport"
        });

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        var result = await response.Content.ReadFromJsonAsync<List<PlaceResultResponse>>();

        var place = Assert.Single(result!);
        Assert.Equal("airport-1", place.PlaceId);
    }

    [Fact]
    public async Task NearbySearch_ReturnsBadRequest_WhenTypeIsMissing()
    {
        var response = await client.PostAsJsonAsync("/api/places/nearby-search", new
        {
            lat = 12.9121,
            lng = 77.6446,
            type = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(result);
        Assert.Equal("type is required.", result!.Error);
    }

    [Fact]
    public async Task TextSearch_ReturnsMappedPlaces()
    {
        factory.When(
            HttpMethod.Post,
            "https://places.googleapis.com/v1/places:searchText",
            Json(HttpStatusCode.OK, """
            {
              "places": [
                {
                  "id": "port-1",
                  "displayName": { "text": "Chennai Port" },
                  "location": { "latitude": 13.0827, "longitude": 80.2707 }
                }
              ]
            }
            """));

        var response = await client.PostAsJsonAsync("/api/places/text-search", new
        {
            lat = 12.9121,
            lng = 77.6446,
            keyword = "port harbor seaport"
        });

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        var result = await response.Content.ReadFromJsonAsync<List<PlaceResultResponse>>();

        var place = Assert.Single(result!);
        Assert.Equal("port-1", place.PlaceId);
        Assert.Equal("Chennai Port", place.Name);
    }

    [Fact]
    public async Task GetPostalBoundaryTarget_ReturnsNotFoundWithCombinedCandidates_WhenBothLookupsFail()
    {
        factory.When(
            HttpMethod.Get,
            "https://maps.googleapis.com/maps/api/geocode/json?components=country:IN|postal_code:560068&key=test-google-key",
            Json(HttpStatusCode.OK, """
            {
              "results": [
                {
                  "types": ["locality"],
                  "place_id": "geo-1",
                  "formatted_address": "Bengaluru, Karnataka, India"
                }
              ]
            }
            """));

        factory.When(
            HttpMethod.Post,
            "https://places.googleapis.com/v1/places:searchText",
            Json(HttpStatusCode.OK, """
            {
              "places": [
                {
                  "id": "places-1",
                  "formattedAddress": "560068, Bengaluru, Karnataka, India",
                  "types": ["locality"]
                }
              ]
            }
            """));

        var response = await client.GetAsync("/api/postal-boundary-target?postalCode=560068&iso2=IN");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PostalBoundaryLookupFailureResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.Candidates.Count);
        Assert.Contains(result.Candidates, candidate => candidate.PlaceId == "geo-1");
        Assert.Contains(result.Candidates, candidate => candidate.PlaceId == "places-1");
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
