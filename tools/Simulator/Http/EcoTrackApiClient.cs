using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Simulator.Json;

namespace Simulator.Http;

public sealed class EcoTrackApiClient(HttpClient httpClient)
{
    public string? AccessToken { get; private set; }

    /// <summary>
    /// Set from the login profile. A superAdmin bypasses tenant query filters and write
    /// validation server-side, so bootstrap must NOT call switch-enterprise for it — that
    /// endpoint is membership-gated and 403s for a user with no membership in the target tenant.
    /// </summary>
    public bool IsSuperAdmin { get; private set; }

    public void SetAccessToken(string token)
    {
        AccessToken = token;
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<string> LoginByPasswordAsync(string email, string password, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(
            "/api/v1/auth/login/password",
            new { email, password },
            SimulatorJson.Options,
            ct);
        var session = await ReadEnvelopeAsync<AuthSessionDto>(response, ct);
        var token = session.Token?.AccessToken
            ?? throw new InvalidOperationException("Login response missing token.access_token.");
        IsSuperAdmin = session.Profile?.IsSuperAdmin ?? false;
        SetAccessToken(token);
        return token;
    }

    public async Task<string> SwitchEnterpriseAsync(Guid enterpriseId, CancellationToken ct)
    {
        var response = await httpClient.PostAsync(
            $"/api/v1/auth/switch-enterprise/{enterpriseId}",
            content: null,
            ct);
        var session = await ReadEnvelopeAsync<AuthSessionDto>(response, ct);
        var token = session.Token?.AccessToken
            ?? throw new InvalidOperationException("switch-enterprise response missing token.access_token.");
        SetAccessToken(token);
        return token;
    }

    public Task<IReadOnlyList<PollutantSummary>> GetPollutantsAsync(CancellationToken ct) =>
        GetListAsync<PollutantSummary>("/api/v1/pollutants", ct);

    public Task<IReadOnlyList<MeasureUnitSummary>> GetUnitsAsync(CancellationToken ct) =>
        GetListAsync<MeasureUnitSummary>("/api/v1/units", ct);

    public async Task<IReadOnlyList<EnterpriseSummary>> GetEnterprisesAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var response = await httpClient.GetAsync(
            $"/api/v1/enterprises?page={page}&pageSize={pageSize}", ct);
        var pageResult = await ReadEnvelopeAsync<PageResultDto<EnterpriseSummary>>(response, ct);
        return (IReadOnlyList<EnterpriseSummary>?)pageResult.Items ?? Array.Empty<EnterpriseSummary>();
    }

    public Task<IReadOnlyList<SiteSummary>> GetSitesAsync(Guid enterpriseId, CancellationToken ct) =>
        GetListAsync<SiteSummary>($"/api/v1/enterprises/{enterpriseId}/sites", ct);

    public Task<IReadOnlyList<InstallationSummary>> GetInstallationsAsync(Guid siteId, CancellationToken ct) =>
        GetListAsync<InstallationSummary>($"/api/v1/sites/{siteId}/installations", ct);

    public async Task<IReadOnlyList<EmissionSourceSummary>> GetEmissionSourcesAsync(
        Guid installationId, int page, int pageSize, CancellationToken ct)
    {
        var response = await httpClient.GetAsync(
            $"/api/v1/installations/{installationId}/emission-sources?page={page}&pageSize={pageSize}", ct);
        var pageResult = await ReadEnvelopeAsync<PageResultDto<EmissionSourceSummary>>(response, ct);
        return (IReadOnlyList<EmissionSourceSummary>?)pageResult.Items ?? Array.Empty<EmissionSourceSummary>();
    }

    public async Task<IReadOnlyList<MonitoringDeviceSummary>> GetMonitoringDevicesAsync(
        Guid installationId, int page, int pageSize, CancellationToken ct)
    {
        var response = await httpClient.GetAsync(
            $"/api/v1/installations/{installationId}/monitoring-devices?page={page}&pageSize={pageSize}", ct);
        var pageResult = await ReadEnvelopeAsync<PageResultDto<MonitoringDeviceSummary>>(response, ct);
        return (IReadOnlyList<MonitoringDeviceSummary>?)pageResult.Items ?? Array.Empty<MonitoringDeviceSummary>();
    }

    public Task<IReadOnlyList<CapabilitySummary>> GetCapabilitiesAsync(Guid deviceId, CancellationToken ct) =>
        GetListAsync<CapabilitySummary>($"/api/v1/monitoring-devices/{deviceId}/capabilities", ct);

    public async Task<RotateSecretResult> RotateIngestionSecretAsync(Guid deviceId, CancellationToken ct)
    {
        var response = await httpClient.PostAsync(
            $"/api/v1/monitoring-devices/{deviceId}/rotate-ingestion-secret",
            content: null,
            ct);
        return await ReadEnvelopeAsync<RotateSecretResult>(response, ct);
    }

    public async Task<CapabilitySummary> CreateCapabilityAsync(
        Guid deviceId,
        Guid pollutantId,
        decimal rangeMin,
        decimal rangeMax,
        Guid rangeUnitId,
        int expectedIntervalMinutes,
        CancellationToken ct)
    {
        var body = new
        {
            pollutantId,
            rangeMin,
            rangeMax,
            rangeUnitId,
            accuracyClass = "Class A",
            expectedIntervalMinutes,
        };
        var response = await httpClient.PostAsJsonAsync(
            $"/api/v1/monitoring-devices/{deviceId}/capabilities",
            body,
            SimulatorJson.Options,
            ct);
        return await ReadEnvelopeAsync<CapabilitySummary>(response, ct);
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken ct)
    {
        
        var response = await httpClient.GetAsync(path, ct);
        var list = await ReadEnvelopeAsync<List<T>>(response, ct);
        return list;
    }

    private static async Task<T> ReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var bodyBytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var raw = bodyBytes.Length == 0
                ? "(empty body)"
                : System.Text.Encoding.UTF8.GetString(bodyBytes);
            throw new HttpRequestException(
                $"{(int)response.StatusCode} {response.ReasonPhrase} for {response.RequestMessage?.RequestUri}: {raw}");
        }

        if (bodyBytes.Length == 0)
        {
            throw new InvalidOperationException(
                $"Empty body for {response.RequestMessage?.RequestUri}.");
        }

        var envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(bodyBytes, SimulatorJson.Options)
            ?? throw new InvalidOperationException(
                $"Could not parse envelope for {response.RequestMessage?.RequestUri}.");

        if (envelope.Data is null)
        {
            throw new InvalidOperationException(
                $"Envelope.data is null for {response.RequestMessage?.RequestUri}; isSuccess={envelope.IsSuccess}, message={envelope.Message}.");
        }
        return envelope.Data;
    }
}

public sealed class AuthSessionDto
{
    public AccessTokenDto? Token { get; set; }
    public AuthProfileDto? Profile { get; set; }
}

public sealed class AuthProfileDto
{
    public bool IsSuperAdmin { get; set; }
    public Guid? ActiveEnterpriseId { get; set; }
}

public sealed class AccessTokenDto
{
    // The API field is `access_token` (see Application.Models.Jwt.AccessToken). CamelCase
    // policy keeps the underscored name as-is, so deserialize via JsonPropertyName.
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
}

public sealed class PageResultDto<T>
{
    public List<T>? Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class PollutantSummary
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid CanonicalUnitId { get; set; }
}

public sealed class MeasureUnitSummary
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? Dimension { get; set; }
}

public sealed class EnterpriseSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Status { get; set; }
}

public sealed class SiteSummary
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
}

public sealed class InstallationSummary
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
}

public sealed class EmissionSourceSummary
{
    public Guid Id { get; set; }
    public Guid InstallationId { get; set; }
    public string? Code { get; set; }
}

public sealed class MonitoringDeviceSummary
{
    public Guid Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? Status { get; set; }
    public Guid? EmissionSourceId { get; set; }
    public Guid InstallationId { get; set; }
}

public sealed class CapabilitySummary
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid PollutantId { get; set; }
    public decimal RangeMin { get; set; }
    public decimal RangeMax { get; set; }
    public Guid RangeUnitId { get; set; }
}

public sealed class RotateSecretResult
{
    public Guid DeviceId { get; set; }
    public string Secret { get; set; } = string.Empty;
}
