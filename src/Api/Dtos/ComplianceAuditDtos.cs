using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record ComplianceAuditQueryDto(
    Guid EmissionSourceId,
    Guid PollutantId,
    decimal LimitValue,
    Guid LimitUnitId,
    AveragingWindow Period,
    DateTime From,
    DateTime To);

public record ComplianceAuditResultDto(
    DateTime From,
    DateTime To,
    AveragingWindow Period,
    decimal LimitValueInBase,
    string LimitUnitSymbol,
    int TotalBuckets,
    long BucketsWithData,
    long ExceedanceBuckets,
    decimal? MaxValueInBase,
    decimal? AvgValueInBase,
    decimal? MaxRatio,
    decimal? ExceedanceRate,
    decimal DataAvailability)
{
    public static ComplianceAuditResultDto FromReadModel(ComplianceAuditResult r) =>
        new(r.From, r.To, r.Period, r.LimitValueInBase, r.LimitUnitSymbol,
            r.TotalBuckets, r.BucketsWithData, r.ExceedanceBuckets,
            r.MaxValueInBase, r.AvgValueInBase, r.MaxRatio, r.ExceedanceRate,
            r.DataAvailability);
}
