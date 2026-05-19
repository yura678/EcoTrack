using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record TimeSeriesQueryDto(
    Guid PollutantId,
    Guid EmissionSourceId,
    DateTime From,
    DateTime To,
    BucketWindow Window,
    AggregationFunc Aggregation = AggregationFunc.Average);

public record HeatmapQueryDto(
    Guid PollutantId,
    DateTime From,
    DateTime To,
    AggregationFunc Aggregation = AggregationFunc.Average);

public record TimeSeriesPointDto(
    DateTime BucketStart,
    decimal Value,
    int TotalPointsCount,
    int ValidPointsCount)
{
    public static TimeSeriesPointDto FromReadModel(TimeSeriesPoint p) =>
        new(p.BucketStart, p.Value, p.TotalPointsCount, p.ValidPointsCount);
}

public record HeatmapPointDto(
    Guid EmissionSourceId,
    double Latitude,
    double Longitude,
    decimal Value,
    Guid UnitId,
    int TotalPointsCount,
    int ValidPointsCount)
{
    public static HeatmapPointDto FromReadModel(HeatmapPoint p) =>
        new(p.EmissionSourceId, p.Latitude, p.Longitude, p.Value, p.UnitId,
            p.TotalPointsCount, p.ValidPointsCount);
}

public record ComplianceAggregatePointDto(
    Guid LimitId,
    Guid InstallationId,
    string InstallationName,
    LimitType LimitType,
    AveragingWindow LimitPeriod,
    decimal LimitValue,
    string LimitUnitSymbol,
    decimal? AggregateValue,
    decimal? Severity,
    string SeverityLevel,
    int ContributingSourcesCount,
    int DerivedSourcesCount,
    int ExcludedSourcesCount,
    int OpenEventCount,
    DateTime? MeasuredAt)
{
    public static ComplianceAggregatePointDto FromReadModel(ComplianceAggregatePoint p) =>
        new(p.LimitId, p.InstallationId, p.InstallationName,
            p.LimitType, p.LimitPeriod, p.LimitValue, p.LimitUnitSymbol,
            p.AggregateValue, p.Severity,
            BuildSeverityLevel(p.Severity, p.OpenEventCount),
            p.ContributingSourcesCount, p.DerivedSourcesCount, p.ExcludedSourcesCount,
            p.OpenEventCount, p.MeasuredAt);

    private static string BuildSeverityLevel(decimal? severity, int openEvents)
    {
        if (openEvents > 0) return "exceedance";
        if (severity is null) return "unknown";
        if (severity > 1.0m) return "exceedance";
        if (severity > 0.7m) return "warning";
        return "green";
    }
}

public record ComplianceHeatmapPointDto(
    Guid EmissionSourceId,
    string EmissionSourceCode,
    Guid InstallationId,
    string InstallationName,
    double Latitude,
    double Longitude,
    Guid? LimitId,
    AveragingWindow? LimitPeriod,
    decimal? LimitValue,
    string? LimitUnitSymbol,
    decimal? CurrentValue,
    bool CurrentValueIsNormalized,
    decimal? Severity,
    string SeverityLevel,
    int OpenEventCount,
    DateTime? MeasuredAt)
{
    public static ComplianceHeatmapPointDto FromReadModel(ComplianceHeatmapPoint p) =>
        new(p.EmissionSourceId, p.EmissionSourceCode, p.InstallationId, p.InstallationName,
            p.Latitude, p.Longitude,
            p.LimitId, p.LimitPeriod, p.LimitValue, p.LimitUnitSymbol,
            p.CurrentValue, p.CurrentValueIsNormalized, p.Severity,
            BuildSeverityLevel(p.Severity, p.OpenEventCount),
            p.OpenEventCount, p.MeasuredAt);

    private static string BuildSeverityLevel(decimal? severity, int openEvents)
    {
        // Any open ComplianceEvent → critical regardless of current value (operator must act).
        if (openEvents > 0) return "exceedance";
        if (severity is null) return "unknown";
        if (severity > 1.0m) return "exceedance";
        if (severity > 0.7m) return "warning";
        return "green";
    }
}

public record MeasurementQueryDto(
    Guid InstallationId,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20);

public record CreateMeasurementDto(
    DateTime Timestamp,
    Guid EmissionSourceId,
    Guid PollutantId,
    Guid DeviceId,
    Guid UnitId,
    AveragingWindow Window,
    decimal Value);

public record RejectMeasurementDto(
    string Reason);

public record MeasurementDto(
    Guid Id,
    DateTime WindowStart,
    DateTime WindowEnd,
    Guid EmissionSourceId,
    EmissionSourceDto? EmissionSourceDto,
    Guid PollutantId,
    PollutantDto? PollutantDto,
    Guid DeviceId,
    Guid UnitId,
    MeasureUnitDto? MeasureUnitDto,
    AveragingWindow Window,
    Aggregation Aggregation,
    decimal Value,
    decimal? NormalizedValue,
    decimal? Uncertainty,
    int ValidPointsCount,
    int ExpectedPointsCount,
    decimal DataAvailability,
    bool IsRepresentative,
    Quality Quality,
    string? QualityNote,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static MeasurementDto FromDomainModel(Measurement measurement)
    {
        return new MeasurementDto(
            measurement.Id,
            measurement.WindowStart,
            measurement.WindowEnd,
            measurement.EmissionSourceId,
            measurement.EmissionSource is not null
                ? EmissionSourceDto.FromDomainModel(measurement.EmissionSource)
                : null,
            measurement.PollutantId,
            measurement.Pollutant is not null
                ? PollutantDto.FromDomainModel(measurement.Pollutant)
                : null,
            measurement.DeviceId,
            measurement.UnitId,
            measurement.Unit != null
                ? MeasureUnitDto.FromDomainModel(measurement.Unit)
                : null,
            measurement.Window,
            measurement.Aggregation,
            measurement.Value,
            measurement.NormalizedValue,
            measurement.Uncertainty,
            measurement.ValidPointsCount,
            measurement.ExpectedPointsCount,
            measurement.DataAvailability,
            measurement.IsRepresentative,
            measurement.Quality,
            measurement.QualityNote,
            measurement.CreatedAt,
            measurement.UpdatedAt
        );
    }
}
