using System.Text.Json;

/// <summary>
/// Maps raw Google API payloads into the simplified backend response contracts consumed by the UI.
/// </summary>
public static class ApiResponseMapper
{
    /// <summary>
    /// Builds a postal-boundary target from a Google Geocoding payload.
    /// </summary>
    public static PostalBoundaryTargetResponse? BuildPostalBoundaryTarget(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty("types", out var types) || types.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var isPostalCode = types.EnumerateArray().Any(type => string.Equals(type.GetString(), "postal_code", StringComparison.Ordinal));
            if (!isPostalCode)
            {
                continue;
            }

            var placeId = result.TryGetProperty("place_id", out var placeIdNode)
                ? placeIdNode.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(placeId))
            {
                continue;
            }

            if (!result.TryGetProperty("geometry", out var geometry))
            {
                continue;
            }

            var location = geometry.TryGetProperty("location", out var locationNode)
                ? BuildLatLng(locationNode)
                : null;
            if (location is null)
            {
                continue;
            }

            var viewport = geometry.TryGetProperty("viewport", out var viewportNode)
                ? BuildViewport(viewportNode)
                : null;

            return new PostalBoundaryTargetResponse(
                placeId,
                location,
                viewport,
                result.TryGetProperty("formatted_address", out var formattedAddressNode)
                    ? formattedAddressNode.GetString() ?? string.Empty
                    : string.Empty);
        }

        return null;
    }

    /// <summary>
    /// Builds a postal-boundary target from a Google Places Text Search payload.
    /// </summary>
    public static PostalBoundaryTargetResponse? BuildPostalBoundaryTargetFromPlaces(JsonElement root)
    {
        if (!root.TryGetProperty("places", out var places) || places.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var place in places.EnumerateArray())
        {
            var placeId = place.TryGetProperty("id", out var placeIdNode)
                ? placeIdNode.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(placeId))
            {
                continue;
            }

            var location = place.TryGetProperty("location", out var locationNode)
                ? BuildPlaceLatLng(locationNode)
                : null;
            if (location is null)
            {
                continue;
            }

            var formattedAddress = place.TryGetProperty("formattedAddress", out var formattedAddressNode)
                ? formattedAddressNode.GetString() ?? string.Empty
                : string.Empty;

            return new PostalBoundaryTargetResponse(placeId, location, null, formattedAddress);
        }

        return null;
    }

    /// <summary>
    /// Builds the candidate list seen in a Google Geocoding payload for postal-boundary diagnostics.
    /// </summary>
    public static List<PostalBoundaryCandidateResponse> BuildPostalBoundaryCandidates(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return results.EnumerateArray().Select(result =>
        {
            var placeId = result.TryGetProperty("place_id", out var placeIdNode)
                ? placeIdNode.GetString() ?? string.Empty
                : string.Empty;
            var formattedAddress = result.TryGetProperty("formatted_address", out var formattedAddressNode)
                ? formattedAddressNode.GetString() ?? string.Empty
                : string.Empty;
            var types = result.TryGetProperty("types", out var typesNode) && typesNode.ValueKind == JsonValueKind.Array
                ? typesNode.EnumerateArray().Select(type => type.GetString() ?? string.Empty).ToList()
                : [];

            return new PostalBoundaryCandidateResponse(placeId, formattedAddress, types);
        }).ToList();
    }

    /// <summary>
    /// Builds the candidate list seen in a Google Places payload for postal-boundary diagnostics.
    /// </summary>
    public static List<PostalBoundaryCandidateResponse> BuildPostalBoundaryCandidatesFromPlaces(JsonElement root)
    {
        if (!root.TryGetProperty("places", out var places) || places.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return places.EnumerateArray().Select(place =>
        {
            var placeId = place.TryGetProperty("id", out var placeIdNode)
                ? placeIdNode.GetString() ?? string.Empty
                : string.Empty;
            var formattedAddress = place.TryGetProperty("formattedAddress", out var formattedAddressNode)
                ? formattedAddressNode.GetString() ?? string.Empty
                : string.Empty;
            var types = place.TryGetProperty("types", out var typesNode) && typesNode.ValueKind == JsonValueKind.Array
                ? typesNode.EnumerateArray().Select(type => type.GetString() ?? string.Empty).ToList()
                : [];

            return new PostalBoundaryCandidateResponse(placeId, formattedAddress, types);
        }).ToList();
    }

    /// <summary>
    /// Builds the normalized address-validation response consumed by the UI.
    /// </summary>
    public static ValidationResultResponse BuildValidationResult(JsonElement root)
    {
        var result = root.TryGetProperty("result", out var resultNode) ? resultNode : default;
        var verdict = result.ValueKind != JsonValueKind.Undefined && result.TryGetProperty("verdict", out var verdictNode) ? verdictNode : default;
        var address = result.ValueKind != JsonValueKind.Undefined && result.TryGetProperty("address", out var addressNode) ? addressNode : default;
        var geocode = result.ValueKind != JsonValueKind.Undefined && result.TryGetProperty("geocode", out var geocodeNode) ? geocodeNode : default;
        var postalAddress = address.ValueKind != JsonValueKind.Undefined && address.TryGetProperty("postalAddress", out var postalNode) ? postalNode : default;

        string postalCode = postalAddress.ValueKind != JsonValueKind.Undefined && postalAddress.TryGetProperty("postalCode", out var postalCodeNode)
            ? postalCodeNode.GetString() ?? string.Empty
            : ExtractPostalCodeFromComponents(address);

        return new ValidationResultResponse(
            address.ValueKind != JsonValueKind.Undefined && address.TryGetProperty("formattedAddress", out var formattedAddressNode)
                ? formattedAddressNode.GetString() ?? string.Empty
                : string.Empty,
            postalCode,
            verdict.ValueKind != JsonValueKind.Undefined && verdict.TryGetProperty("validationGranularity", out var validationGranularityNode)
                ? validationGranularityNode.GetString() ?? string.Empty
                : string.Empty,
            verdict.ValueKind != JsonValueKind.Undefined && verdict.TryGetProperty("geocodeGranularity", out var geocodeGranularityNode)
                ? geocodeGranularityNode.GetString() ?? string.Empty
                : string.Empty,
            verdict.ValueKind != JsonValueKind.Undefined &&
            verdict.TryGetProperty("addressComplete", out var addressCompleteNode) &&
            addressCompleteNode.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            addressCompleteNode.GetBoolean(),
            ExtractLatitude(geocode),
            ExtractLongitude(geocode));
    }

    /// <summary>
    /// Builds the simplified places list consumed by the UI.
    /// </summary>
    public static List<PlaceResultResponse> BuildPlacesResult(JsonElement root)
    {
        if (!root.TryGetProperty("places", out var places) || places.ValueKind != JsonValueKind.Array)
        {
            return new List<PlaceResultResponse>();
        }

        var results = new List<PlaceResultResponse>();
        foreach (var place in places.EnumerateArray())
        {
            var name = place.TryGetProperty("displayName", out var displayName) &&
                       displayName.TryGetProperty("text", out var text)
                ? text.GetString() ?? "Unknown"
                : "Unknown";
            var id = place.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? string.Empty : string.Empty;

            double lat = 0;
            double lng = 0;
            if (place.TryGetProperty("location", out var location))
            {
                lat = location.TryGetProperty("latitude", out var latNode) ? latNode.GetDouble() : 0;
                lng = location.TryGetProperty("longitude", out var lngNode) ? lngNode.GetDouble() : 0;
            }

            results.Add(new PlaceResultResponse(name, id, new PlaceGeometryResponse(new PlaceLocationResponse(lat, lng))));
        }

        return results;
    }

    /// <summary>
    /// Extracts a Google-style error message from a JSON payload, or returns the fallback text.
    /// </summary>
    public static string ExtractGoogleError(string bodyText, string fallback)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(bodyText);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? fallback;
            }
        }
        catch
        {
        }

        return fallback;
    }

    private static LatLngResponse? BuildLatLng(JsonElement root)
    {
        if (!root.TryGetProperty("lat", out var latNode) || !root.TryGetProperty("lng", out var lngNode))
        {
            return null;
        }

        return new LatLngResponse(latNode.GetDouble(), lngNode.GetDouble());
    }

    private static ViewportResponse? BuildViewport(JsonElement root)
    {
        if (!root.TryGetProperty("northeast", out var northEastNode) || !root.TryGetProperty("southwest", out var southWestNode))
        {
            return null;
        }

        var northEast = BuildLatLng(northEastNode);
        var southWest = BuildLatLng(southWestNode);
        if (northEast is null || southWest is null)
        {
            return null;
        }

        return new ViewportResponse(northEast, southWest);
    }

    private static LatLngResponse? BuildPlaceLatLng(JsonElement root)
    {
        if (!root.TryGetProperty("latitude", out var latNode) || !root.TryGetProperty("longitude", out var lngNode))
        {
            return null;
        }

        return new LatLngResponse(latNode.GetDouble(), lngNode.GetDouble());
    }

    private static string ExtractPostalCodeFromComponents(JsonElement address)
    {
        if (!address.TryGetProperty("addressComponents", out var components) || components.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var component in components.EnumerateArray())
        {
            if (!component.TryGetProperty("componentType", out var typeNode) ||
                !string.Equals(typeNode.GetString(), "postal_code", StringComparison.Ordinal))
            {
                continue;
            }

            if (component.TryGetProperty("componentName", out var componentName) &&
                componentName.TryGetProperty("text", out var textNode))
            {
                return textNode.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static double? ExtractLatitude(JsonElement geocode)
    {
        if (geocode.ValueKind == JsonValueKind.Undefined ||
            !geocode.TryGetProperty("location", out var location) ||
            !location.TryGetProperty("latitude", out var latitude))
        {
            return null;
        }

        return latitude.GetDouble();
    }

    private static double? ExtractLongitude(JsonElement geocode)
    {
        if (geocode.ValueKind == JsonValueKind.Undefined ||
            !geocode.TryGetProperty("location", out var location) ||
            !location.TryGetProperty("longitude", out var longitude))
        {
            return null;
        }

        return longitude.GetDouble();
    }
}
