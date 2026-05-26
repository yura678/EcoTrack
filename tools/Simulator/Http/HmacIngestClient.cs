using System.Net.Http.Headers;
using System.Text.Json;
using Simulator.Json;

namespace Simulator.Http;

public sealed class HmacIngestClient(HttpClient httpClient)
{
    public async Task<HttpResponseMessage> PostAsync<T>(
        string path,
        T payload,
        string deviceSerial,
        string ingestionSecret,
        CancellationToken ct)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, SimulatorJson.Options);
        var signed = HmacSigner.Sign(ingestionSecret, body);

        var content = new ByteArrayContent(signed.Body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.TryAddWithoutValidation("X-Device-Serial", deviceSerial);
        request.Headers.TryAddWithoutValidation("X-Timestamp", signed.Timestamp);
        request.Headers.TryAddWithoutValidation("X-Nonce", signed.Nonce);
        request.Headers.TryAddWithoutValidation("X-Signature", signed.Signature);

        return await httpClient.SendAsync(request, ct);
    }
}
