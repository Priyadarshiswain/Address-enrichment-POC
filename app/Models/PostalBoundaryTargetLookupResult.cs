public sealed record PostalBoundaryTargetLookupResult(
    PostalBoundaryTargetResponse? Target,
    PostalBoundaryLookupFailureResponse? Failure)
{
    public static PostalBoundaryTargetLookupResult Success(PostalBoundaryTargetResponse target) => new(target, null);
    public static PostalBoundaryTargetLookupResult NotFound(PostalBoundaryLookupFailureResponse failure) => new(null, failure);
}
