/// <summary>
/// Browser-consumable runtime configuration for the Angular UI.
/// </summary>
/// <param name="MapsApiKey">Google Maps JavaScript API key. This key is not secret at runtime and should be restricted by HTTP referrer.</param>
/// <param name="MapId">Google Maps JavaScript vector map ID with the POSTAL_CODE boundary feature layer enabled.</param>
public sealed record ClientConfigResponse(string MapsApiKey, string MapId);

/// <summary>
/// Standard error payload returned for handled request failures.
/// </summary>
/// <param name="Error">Human-readable error message.</param>
public sealed record ErrorResponse(string Error);

/// <summary>
/// Postal boundary target metadata used to render Google's POSTAL_CODE boundary layer.
/// </summary>
public sealed record PostalBoundaryTargetResponse(string PlaceId, LatLngResponse Location, ViewportResponse? Viewport, string FormattedAddress);

/// <summary>
/// Diagnostic payload returned when a postal boundary target lookup fails.
/// </summary>
public sealed record PostalBoundaryLookupFailureResponse(string Error, List<PostalBoundaryCandidateResponse> Candidates);

/// <summary>
/// Candidate geocoding result inspected during postal boundary lookup.
/// </summary>
public sealed record PostalBoundaryCandidateResponse(string PlaceId, string FormattedAddress, List<string> Types);

/// <summary>
/// Latitude/longitude pair.
/// </summary>
public sealed record LatLngResponse(double Lat, double Lng);

/// <summary>
/// Map viewport corners.
/// </summary>
public sealed record ViewportResponse(LatLngResponse NorthEast, LatLngResponse SouthWest);

/// <summary>
/// Request payload for Google Address Validation.
/// </summary>
/// <param name="Address">Street address line.</param>
/// <param name="City">City or locality.</param>
/// <param name="State">State, province, or administrative area.</param>
/// <param name="Iso2">Two-letter ISO country code.</param>
public sealed record AddressValidationRequest(string Address, string City, string State, string Iso2);

/// <summary>
/// Normalized response from Google Address Validation.
/// </summary>
public sealed record ValidationResultResponse(
    string FormattedAddress,
    string PostalCode,
    string Granularity,
    string GeocodeGranularity,
    bool AddressComplete,
    double? Lat,
    double? Lng);

/// <summary>
/// Request payload for a nearby places search.
/// </summary>
/// <param name="Lat">Center latitude.</param>
/// <param name="Lng">Center longitude.</param>
/// <param name="Type">Google Places type to search for, such as airport.</param>
public sealed record PlacesNearbySearchRequest(double Lat, double Lng, string Type);

/// <summary>
/// Request payload for a text-based places search.
/// </summary>
/// <param name="Lat">Center latitude.</param>
/// <param name="Lng">Center longitude.</param>
/// <param name="Keyword">Text query, such as port harbor seaport.</param>
public sealed record PlacesTextSearchRequest(double Lat, double Lng, string Keyword);

/// <summary>
/// Simplified place result returned to the UI.
/// </summary>
public sealed record PlaceResultResponse(string Name, string PlaceId, PlaceGeometryResponse Geometry);

/// <summary>
/// Geometry payload for a place result.
/// </summary>
public sealed record PlaceGeometryResponse(PlaceLocationResponse Location);

/// <summary>
/// Latitude and longitude pair for a place result.
/// </summary>
public sealed record PlaceLocationResponse(double Lat, double Lng);
