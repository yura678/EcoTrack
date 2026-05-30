using Api.Dtos;
using Domain.Entities.Monitoring;
using FluentValidation;

namespace Api.Modules.Validators.ComplianceEvents;

public class ComplianceAuditQueryDtoValidator : AbstractValidator<ComplianceAuditQueryDto>
{
    // The audit reads one window-row per period from measurement_1m into memory before folding
    // in C# (canonical conversion can't run in SQL). Capping the bucket count keeps the working
    // set bounded regardless of period — without this, a 1-minute audit over years would pull
    // millions of rows. ~100k buckets is a generous ceiling that still covers realistic ranges.
    private const long MaxBuckets = 100_000;

    public ComplianceAuditQueryDtoValidator()
    {
        RuleFor(x => x.EmissionSourceId).NotEmpty();
        RuleFor(x => x.PollutantId).NotEmpty();
        RuleFor(x => x.LimitUnitId).NotEmpty();
        RuleFor(x => x.LimitValue).GreaterThan(0);
        RuleFor(x => x.Period).IsInEnum();
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).NotEmpty();

        RuleFor(x => x.Period)
            .Must(p => PeriodToTimeSpan(p) != TimeSpan.Zero)
            .WithMessage("Audit supports 1-minute, 10-minute, half-hour, 1-hour and 24-hour " +
                         "periods only. Monthly and annual windows are not available.");

        RuleFor(x => x).Must(q => q.From < q.To)
            .WithMessage("'From' must be earlier than 'To'.");

        // Per-period range cap: bound the number of buckets the audit has to materialize.
        RuleFor(x => x)
            .Must(q =>
            {
                var period = PeriodToTimeSpan(q.Period);
                if (period == TimeSpan.Zero) return true; // handled by the period rule above
                if (q.To <= q.From) return true;          // handled by the ordering rule above
                return (q.To - q.From).Ticks / period.Ticks <= MaxBuckets;
            })
            .WithMessage(q =>
            {
                var period = PeriodToTimeSpan(q.Period);
                var maxDays = period == TimeSpan.Zero
                    ? 0
                    : MaxBuckets * period.TotalDays;
                return $"Range too large for the {q.Period} period — keep it within " +
                       $"{maxDays:0} days (max {MaxBuckets:N0} averaging windows).";
            });
    }

    // Mirrors RawMeasurementQueries.PeriodToTimeSpan: the set of windows the audit can evaluate.
    private static TimeSpan PeriodToTimeSpan(AveragingWindow period) => period switch
    {
        AveragingWindow.Minute1 => TimeSpan.FromMinutes(1),
        AveragingWindow.Minute10 => TimeSpan.FromMinutes(10),
        AveragingWindow.HalfHour => TimeSpan.FromMinutes(30),
        AveragingWindow.Hour1 => TimeSpan.FromHours(1),
        AveragingWindow.Hour24 => TimeSpan.FromHours(24),
        _ => TimeSpan.Zero
    };
}
