using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Application.Common.Interfaces.Persistence;
using Domain.Entities.Monitoring;
using LanguageExt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity.Ingestion;

public static class DeviceHmacAuthDefaults
{
    public const string Scheme = "DeviceHmac";
    public const string SerialHeader = "X-Device-Serial";
    public const string SignatureHeader = "X-Signature";
    public const string TimestampHeader = "X-Timestamp";
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);
}

public class DeviceHmacAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceProvider serviceProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var serial = Context.Request.Headers[DeviceHmacAuthDefaults.SerialHeader].FirstOrDefault();
        var signature = Context.Request.Headers[DeviceHmacAuthDefaults.SignatureHeader].FirstOrDefault();
        var timestamp = Context.Request.Headers[DeviceHmacAuthDefaults.TimestampHeader].FirstOrDefault();

        if (string.IsNullOrEmpty(serial) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
        {
            return AuthenticateResult.NoResult();
        }

        if (!DateTime.TryParse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
        {
            return AuthenticateResult.Fail("Invalid X-Timestamp.");
        }

        var skew = (DateTime.UtcNow - ts.ToUniversalTime()).Duration();
        if (skew > DeviceHmacAuthDefaults.MaxClockSkew)
        {
            return AuthenticateResult.Fail($"X-Timestamp out of allowed skew ({skew.TotalSeconds:F0}s).");
        }

        using var scope = serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var deviceOption = await unitOfWork.MonitoringDeviceRepository.GetBySerialNumberAsync(
            serial, Context.RequestAborted);

        MonitoringDevice? device = deviceOption.Match<MonitoringDevice?>(d => d, () => null);
        if (device is null)
            return AuthenticateResult.Fail("Unknown device serial.");

        if (string.IsNullOrEmpty(device.IngestionSecret))
            return AuthenticateResult.Fail("Device has no ingestion secret configured.");

        Context.Request.EnableBuffering();
        Context.Request.Body.Position = 0;
        using var reader = new StreamReader(Context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(Context.RequestAborted);
        Context.Request.Body.Position = 0;

        var payload = $"{timestamp}.{body}";
        byte[] secretBytes;
        try
        {
            secretBytes = Convert.FromBase64String(device.IngestionSecret);
        }
        catch (FormatException)
        {
            return AuthenticateResult.Fail("Stored secret is not valid base64.");
        }

        var expected = Convert.ToBase64String(HMACSHA256.HashData(secretBytes, Encoding.UTF8.GetBytes(payload)));

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature)))
        {
            return AuthenticateResult.Fail("Signature mismatch.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, device.Id.ToString()),
            new Claim("DeviceSerial", device.SerialNumber),
            new Claim("DeviceId", device.Id.ToString()),
        };
        var identity = new ClaimsIdentity(claims, DeviceHmacAuthDefaults.Scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), DeviceHmacAuthDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }
}
