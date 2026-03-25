using System.Text.Json;
using Xunit;

namespace AddressEnrichment.Api.Tests.Unit;

public class ApiResponseMapperTests
{
    [Fact]
    public void BuildPostalBoundaryTarget_PicksPostalCodeResultAndMapsViewport()
    {
        using var document = JsonDocument.Parse("""
        {
          "results": [
            {
              "types": ["locality"],
              "place_id": "locality-1",
              "geometry": {
                "location": { "lat": 0, "lng": 0 }
              }
            },
            {
              "types": ["postal_code"],
              "place_id": "postal-123",
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
        """);

        var result = ApiResponseMapper.BuildPostalBoundaryTarget(document.RootElement);

        Assert.NotNull(result);
        Assert.Equal("postal-123", result!.PlaceId);
        Assert.Equal("560068, Bengaluru, Karnataka, India", result.FormattedAddress);
        Assert.Equal(12.9121, result.Location.Lat);
        Assert.Equal(77.6446, result.Location.Lng);
        Assert.NotNull(result.Viewport);
        Assert.Equal(12.99, result.Viewport!.NorthEast.Lat);
        Assert.Equal(77.58, result.Viewport.SouthWest.Lng);
    }

    [Fact]
    public void BuildValidationResult_FallsBackToAddressComponentsPostalCode()
    {
        using var document = JsonDocument.Parse("""
        {
          "result": {
            "verdict": {
              "validationGranularity": "PREMISE",
              "geocodeGranularity": "PREMISE",
              "addressComplete": true
            },
            "address": {
              "formattedAddress": "12 Main St, Bengaluru 560068, India",
              "addressComponents": [
                {
                  "componentType": "postal_code",
                  "componentName": { "text": "560068" }
                }
              ]
            },
            "geocode": {
              "location": {
                "latitude": 12.9121,
                "longitude": 77.6446
              }
            }
          }
        }
        """);

        var result = ApiResponseMapper.BuildValidationResult(document.RootElement);

        Assert.Equal("12 Main St, Bengaluru 560068, India", result.FormattedAddress);
        Assert.Equal("560068", result.PostalCode);
        Assert.Equal("PREMISE", result.Granularity);
        Assert.True(result.AddressComplete);
        Assert.Equal(12.9121, result.Lat);
        Assert.Equal(77.6446, result.Lng);
    }

    [Fact]
    public void BuildPlacesResult_MapsPlacesIntoUiShape()
    {
        using var document = JsonDocument.Parse("""
        {
          "places": [
            {
              "id": "place-1",
              "displayName": { "text": "Kempegowda International Airport" },
              "location": { "latitude": 13.1986, "longitude": 77.7066 }
            }
          ]
        }
        """);

        var result = ApiResponseMapper.BuildPlacesResult(document.RootElement);

        var place = Assert.Single(result);
        Assert.Equal("place-1", place.PlaceId);
        Assert.Equal("Kempegowda International Airport", place.Name);
        Assert.Equal(13.1986, place.Geometry.Location.Lat);
        Assert.Equal(77.7066, place.Geometry.Location.Lng);
    }

    [Fact]
    public void BuildPostalBoundaryTargetFromPlaces_MapsFirstPlace()
    {
        using var document = JsonDocument.Parse("""
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
        """);

        var result = ApiResponseMapper.BuildPostalBoundaryTargetFromPlaces(document.RootElement);

        Assert.NotNull(result);
        Assert.Equal("places-postal-1", result!.PlaceId);
        Assert.Equal("560068, Bengaluru, Karnataka, India", result.FormattedAddress);
        Assert.Equal(12.9121, result.Location.Lat);
    }

    [Fact]
    public void BuildPostalBoundaryCandidates_FromGeocode_MapsAllCandidates()
    {
        using var document = JsonDocument.Parse("""
        {
          "results": [
            {
              "place_id": "one",
              "formatted_address": "A",
              "types": ["postal_code"]
            },
            {
              "place_id": "two",
              "formatted_address": "B",
              "types": ["locality"]
            }
          ]
        }
        """);

        var result = ApiResponseMapper.BuildPostalBoundaryCandidates(document.RootElement);

        Assert.Equal(2, result.Count);
        Assert.Equal("one", result[0].PlaceId);
        Assert.Equal("locality", result[1].Types[0]);
    }

    [Fact]
    public void BuildPostalBoundaryCandidates_FromPlaces_MapsAllCandidates()
    {
        using var document = JsonDocument.Parse("""
        {
          "places": [
            {
              "id": "places-postal-1",
              "formattedAddress": "560068, Bengaluru, Karnataka, India",
              "types": ["postal_code", "locality"]
            }
          ]
        }
        """);

        var result = ApiResponseMapper.BuildPostalBoundaryCandidatesFromPlaces(document.RootElement);

        var candidate = Assert.Single(result);
        Assert.Equal("places-postal-1", candidate.PlaceId);
        Assert.Contains("postal_code", candidate.Types);
    }

    [Fact]
    public void ExtractGoogleError_ReturnsMessageFromValidJson()
    {
        var result = ApiResponseMapper.ExtractGoogleError("""
        {
          "error": {
            "message": "API key invalid"
          }
        }
        """, "fallback");

        Assert.Equal("API key invalid", result);
    }

    [Fact]
    public void ExtractGoogleError_ReturnsFallbackForInvalidJson()
    {
        var result = ApiResponseMapper.ExtractGoogleError("not-json", "fallback");

        Assert.Equal("fallback", result);
    }
}
