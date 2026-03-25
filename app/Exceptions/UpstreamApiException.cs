public sealed class UpstreamApiException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
