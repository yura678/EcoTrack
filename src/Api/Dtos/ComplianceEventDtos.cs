using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record ComplianceEventListQueryDto(
    ComplianceEventStatus? Status,
    ComplianceEventType? EventType,
    Guid? EmissionSourceId,
    Guid? DeviceId,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20);

public record CloseComplianceEventDto(
    ResolutionReason Reason,
    string? Note);

public record ComplianceEventDto(
    Guid Id,
    ComplianceEventType EventType,
    Guid EmissionSourceId,
    string? EmissionSourceCode,
    Guid? MeasurementId,
    Guid? LimitId,
    Guid? DeviceId,
    string? DeviceSerialNumber,
    DateTime WindowStart,
    DateTime WindowEnd,
    decimal? Ratio,
    ComplianceEventStatus Status,
    DateTime DetectedAt,
    DateTime? ClosedAt,
    DateTime? UpdatedAt,
    string? Notes,
    bool? IsCurrentlyViolating,
    ResolutionReason? ResolutionReason,
    string? ResolutionNote,
    Guid? ResolvedByUserId)
{
    public static ComplianceEventDto FromDomainModel(ComplianceEvent ev, bool? isCurrentlyViolating = null) =>
        new(
            ev.Id,
            ev.EventType,
            ev.EmissionSourceId,
            ev.EmissionSource?.Code,
            ev.MeasurementId,
            ev.LimitId,
            ev.DeviceId,
            ev.Device?.SerialNumber,
            ev.WindowStart,
            ev.WindowEnd,
            ev.Ratio,
            ev.Status,
            ev.DetectedAt,
            ev.ClosedAt,
            ev.UpdatedAt,
            ev.Notes,
            isCurrentlyViolating,
            ev.ResolutionReason,
            ev.ResolutionNote,
            ev.ResolvedByUserId);
}

public record ComplianceEventSourceRefDto(
    Guid Id,
    string Code,
    string Kind, // "Air" | "Water"
    Guid InstallationId,
    string InstallationName,
    Guid SiteId,
    string SiteName)
{
    public static ComplianceEventSourceRefDto FromDomainModel(EmissionSource source)
    {
        var kind = source switch
        {
            AirEmissionSource => "Air",
            WaterEmissionSource => "Water",
            _ => "Unknown"
        };
        return new ComplianceEventSourceRefDto(
            source.Id,
            source.Code,
            kind,
            source.InstallationId,
            source.Installation?.Name ?? string.Empty,
            source.Installation?.SiteId ?? Guid.Empty,
            source.Installation?.Site?.Name ?? string.Empty);
    }
}

public record ComplianceEventPollutantRefDto(
    Guid Id,
    string Code,
    string Name,
    string? CanonicalUnit)
{
    public static ComplianceEventPollutantRefDto FromDomainModel(Pollutant? p) =>
        p is null
            ? new ComplianceEventPollutantRefDto(Guid.Empty, string.Empty, string.Empty, null)
            : new ComplianceEventPollutantRefDto(p.Id, p.Code, p.Name, p.CanonicalUnit?.Symbol);
}

public record ComplianceEventMeasurementRefDto(
    Guid Id,
    decimal Value,
    decimal? NormalizedValue,
    string Unit,
    DateTime WindowStart,
    DateTime WindowEnd,
    AveragingWindow Window,
    Aggregation Aggregation,
    int ValidPointsCount,
    int ExpectedPointsCount,
    Quality Quality,
    ComplianceEventPollutantRefDto Pollutant)
{
    public static ComplianceEventMeasurementRefDto FromDomainModel(Measurement m) =>
        new(
            m.Id,
            m.Value,
            m.NormalizedValue,
            m.Unit?.Symbol ?? string.Empty,
            m.WindowStart,
            m.WindowEnd,
            m.Window,
            m.Aggregation,
            m.ValidPointsCount,
            m.ExpectedPointsCount,
            m.Quality,
            ComplianceEventPollutantRefDto.FromDomainModel(m.Pollutant));
}

public record ComplianceEventLimitRefDto(
    Guid Id,
    decimal Value,
    string Unit,
    AveragingWindow Period,
    LimitType LimitType,
    DateTime ValidFrom,
    DateTime? ValidTo,
    ComplianceEventPollutantRefDto Pollutant)
{
    public static ComplianceEventLimitRefDto FromDomainModel(EmissionLimit l) =>
        new(
            l.Id,
            l.Value,
            l.Unit?.Symbol ?? string.Empty,
            l.Period,
            l.LimitType,
            l.ValidFrom,
            l.ValidTo,
            ComplianceEventPollutantRefDto.FromDomainModel(l.Pollutant));
}

public record ComplianceEventDeviceRefDto(
    Guid Id,
    string Model,
    string SerialNumber,
    MonitoringDeviceType Type,
    DeviceStatus Status)
{
    public static ComplianceEventDeviceRefDto FromDomainModel(MonitoringDevice d) =>
        new(d.Id, d.Model, d.SerialNumber, d.Type, d.Status);
}

public record ComplianceEventResolutionRefDto(
    ResolutionReason Reason,
    string? Note,
    Guid? ResolvedByUserId,
    string? ResolvedByDisplayName,
    DateTime? ClosedAt);

public record ComplianceEventDetailDto(
    Guid Id,
    ComplianceEventType EventType,
    ComplianceEventStatus Status,
    DateTime DetectedAt,
    DateTime WindowStart,
    DateTime WindowEnd,
    DateTime? ClosedAt,
    DateTime? UpdatedAt,
    decimal? Ratio,
    string? Notes,
    bool? IsCurrentlyViolating,
    ComplianceEventSourceRefDto EmissionSource,
    ComplianceEventMeasurementRefDto? Measurement,
    ComplianceEventLimitRefDto? Limit,
    ComplianceEventDeviceRefDto? Device,
    ComplianceEventResolutionRefDto? Resolution)
{
    public static ComplianceEventDetailDto FromDomainModel(
        ComplianceEventDetailView view, bool? currentlyViolating)
    {
        var ev = view.Event;

        ComplianceEventResolutionRefDto? resolution = null;
        if (ev.Status != ComplianceEventStatus.Open && ev.ResolutionReason is { } reason)
        {
            resolution = new ComplianceEventResolutionRefDto(
                reason,
                ev.ResolutionNote,
                ev.ResolvedByUserId,
                BuildDisplayName(view.ResolvedByName, view.ResolvedByFamilyName, view.ResolvedByEmail),
                ev.ClosedAt);
        }

        return new ComplianceEventDetailDto(
            ev.Id,
            ev.EventType,
            ev.Status,
            ev.DetectedAt,
            ev.WindowStart,
            ev.WindowEnd,
            ev.ClosedAt,
            ev.UpdatedAt,
            ev.Ratio,
            ev.Notes,
            currentlyViolating,
            ComplianceEventSourceRefDto.FromDomainModel(ev.EmissionSource!),
            ev.Measurement is { } m ? ComplianceEventMeasurementRefDto.FromDomainModel(m) : null,
            ev.Limit is { } l ? ComplianceEventLimitRefDto.FromDomainModel(l) : null,
            ev.Device is { } d ? ComplianceEventDeviceRefDto.FromDomainModel(d) : null,
            resolution);
    }

    private static string? BuildDisplayName(string? name, string? familyName, string? email)
    {
        var combined = string.Join(' ', new[] { name, familyName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(combined) ? email : combined;
    }
}
