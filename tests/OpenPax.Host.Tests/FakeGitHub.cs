using System.Net;

namespace OpenPax.Host.Tests;

/// <summary>Answers requests from a table of addresses, as GitHub would, with no network.</summary>
internal sealed class FakeGitHub : HttpMessageHandler
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public List<string> Asked { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";
        Asked.Add(url);
        return Task.FromResult(Files.TryGetValue(url, out var body)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
