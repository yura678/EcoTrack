using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace Tests.Common;

public record CapturedHttpRequest(
    HttpMethod Method,
    Uri Uri,
    string Body,
    HttpHeaders Headers);

/// <summary>
/// Test message handler that swallows outgoing HTTP requests and records them so tests can
/// assert on URL/headers/body. Returns 200 OK by default; tests can override StatusCode to
/// simulate failures and exercise the dispatcher's per-recipient isolation.
/// </summary>
public class CapturingHttpMessageHandler : HttpMessageHandler
{
    public ConcurrentQueue<CapturedHttpRequest> Requests { get; } = new();
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        // Merge content headers + request headers into one bag for easier assertion.
        var headers = new HttpRequestMessage().Headers;
        foreach (var h in request.Headers) headers.TryAddWithoutValidation(h.Key, h.Value);
        if (request.Content is not null)
        {
            foreach (var h in request.Content.Headers) headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        Requests.Enqueue(new CapturedHttpRequest(
            request.Method, request.RequestUri!, body, headers));

        return new HttpResponseMessage(StatusCode);
    }

    public void Clear()
    {
        Requests.Clear();
        StatusCode = HttpStatusCode.OK;
    }
}
