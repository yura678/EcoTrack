using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Simulator.Http;

public readonly record struct SignedRequest(
    string Timestamp,
    string Nonce,
    string Signature,
    byte[] Body);

public static class HmacSigner
{
    public static SignedRequest Sign(string secretBase64, byte[] body)
    {
        var timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));

        var bodyString = Encoding.UTF8.GetString(body);
        var payload = Encoding.UTF8.GetBytes($"{timestamp}.{nonce}.{bodyString}");

        byte[] secretBytes;
        try
        {
            secretBytes = Convert.FromBase64String(secretBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "IngestionSecret is not valid base64. Re-run bootstrap to rotate.", ex);
        }

        var signature = Convert.ToBase64String(HMACSHA256.HashData(secretBytes, payload));
        return new SignedRequest(timestamp, nonce, signature, body);
    }
}
