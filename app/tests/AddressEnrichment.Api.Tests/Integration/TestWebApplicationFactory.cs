using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AddressEnrichment.Api.Tests.Integration;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly StubHttpMessageHandler handler = new();

    public void When(HttpMethod method, string url, HttpResponseMessage response)
    {
        handler.When(method, url, response);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:ApiKey"] = "test-google-key",
                ["Google:MapsApiKey"] = "test-maps-key",
                ["Google:MapId"] = "test-map-id",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(_ => new StubHttpClientFactory(handler));
        });
    }
}

internal sealed class StubHttpClientFactory(StubHttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler)
    {
        BaseAddress = new Uri("https://stubbed.invalid")
    };
}

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(HttpMethod Method, string UrlPrefix, HttpResponseMessage Response)> responses = [];

    public void When(HttpMethod method, string url, HttpResponseMessage response)
    {
        responses.Add((method, url, response));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestUrl = request.RequestUri!.ToString();
        var match = responses.LastOrDefault(entry =>
            entry.Method == request.Method &&
            requestUrl.StartsWith(entry.UrlPrefix, StringComparison.Ordinal));

        if (match.Response is not null)
        {
            return Task.FromResult(Clone(match.Response));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No stub registered for {request.Method} {request.RequestUri}", Encoding.UTF8, "text/plain")
        });
    }

    private static HttpResponseMessage Clone(HttpResponseMessage source)
    {
        var clone = new HttpResponseMessage(source.StatusCode);
        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (source.Content is not null)
        {
            var body = source.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var mediaType = source.Content.Headers.ContentType?.MediaType ?? "application/json";
            clone.Content = new StringContent(body, Encoding.UTF8, mediaType);
        }

        return clone;
    }
}
