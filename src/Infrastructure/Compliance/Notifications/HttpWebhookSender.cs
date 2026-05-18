using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Application.Common.Interfaces.Notifications;

namespace Infrastructure.Compliance.Notifications;

/// <summary>
/// POSTs the JSON payload to the subscriber's URL with HMAC-SHA256 headers. The signing
/// scheme matches the device ingest HMAC contract (X-Signature over
/// "{timestamp}.{nonce}.{body}") so subscribers can reuse one verification path for both
/// inbound and outbound payloads.
/// </summary>
public class HttpWebhookSender(HttpClient httpClient) : IWebhookSender
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public async Task SendAsync(
        string url, string secret, string payloadJson, CancellationToken cancellationToken)
    {
        var timestamp = DateTime.UtcNow.ToString("o");
        var nonce = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var canonical = $"{timestamp}.{nonce}.{payloadJson}";
        var signature = ComputeSignature(secret, canonical);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Nonce", nonce);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(RequestTimeout);

        using var response = await httpClient.SendAsync(request, cts.Token);
        response.EnsureSuccessStatusCode();
    }

    private static string ComputeSignature(string secret, string canonical)
    {
        // Secret is treated as an opaque UTF-8 string. Subscribers reconstruct the canonical
        // payload as "{X-Timestamp}.{X-Nonce}.{body}" and run HMACSHA256 over UTF-8 bytes of
        // both the secret and the canonical message. Auto-decoding base64 was deliberately
        // dropped — some plain strings happen to be valid base64 and that ambiguity made
        // signatures unverifiable from the subscriber side.
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(canonical));
        return Convert.ToBase64String(hash);
    }
}
