using System.Collections.Concurrent;

namespace Libris.Api.Tests.Infrastructure;

/// <summary>Replaces the network for outbound HttpClients and records what was asked.</summary>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    private readonly ConcurrentQueue<Uri> _requests = new();

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public IReadOnlyCollection<Uri> Requests => _requests;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _requests.Enqueue(request.RequestUri!);
        return Task.FromResult(_respond(request));
    }
}
