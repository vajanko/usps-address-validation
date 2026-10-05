using System.Net;
using System.Text;

namespace USPS.Tests.Infrastructure;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers from a scripted queue, so tests can drive the
/// client through every USPS response shape without touching the network.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

    /// <summary>Every request the handler saw, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Number of requests the handler saw.</summary>
    public int RequestCount => Requests.Count;

    /// <summary>Requests sent to the OAuth token endpoint.</summary>
    public IReadOnlyList<HttpRequestMessage> TokenRequests =>
        [.. Requests.Where(r => r.RequestUri!.AbsolutePath.Contains("/oauth2/", StringComparison.Ordinal))];

    /// <summary>Requests sent to the Addresses endpoint.</summary>
    public IReadOnlyList<HttpRequestMessage> AddressRequests =>
        [.. Requests.Where(r => r.RequestUri!.AbsolutePath.Contains("/addresses/", StringComparison.Ordinal))];

    /// <summary>Queues a raw responder.</summary>
    public StubHttpMessageHandler Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responders.Enqueue(responder);
        return this;
    }

    /// <summary>Queues a JSON response.</summary>
    public StubHttpMessageHandler EnqueueJson(HttpStatusCode statusCode, string json)
        => Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    /// <summary>Queues a successful OAuth token response.</summary>
    public StubHttpMessageHandler EnqueueToken(string accessToken = "test-access-token", int expiresIn = 28799)
        => EnqueueJson(
            HttpStatusCode.OK,
            $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":{{expiresIn}},"scope":"addresses"}""");

    /// <summary>Queues a responder that throws, simulating an unreachable service.</summary>
    public StubHttpMessageHandler EnqueueThrow(Exception exception)
        => Enqueue(_ => throw exception);

    /// <summary>
    /// The body of a recorded request. Captured while the request is in flight, because the client
    /// disposes the request — and with it the content — as soon as it has been sent.
    /// </summary>
    public string? BodyOf(HttpRequestMessage request)
    {
        var index = Requests.IndexOf(request);
        return index >= 0 && index < _bodies.Count ? _bodies[index] : null;
    }

    private readonly List<string?> _bodies = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        _bodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken));

        if (_responders.Count == 0)
        {
            throw new InvalidOperationException(
                $"The stub handler received an unexpected {request.Method} request to '{request.RequestUri}'.");
        }

        var response = _responders.Dequeue()(request);
        response.RequestMessage = request;
        return response;
    }
}
