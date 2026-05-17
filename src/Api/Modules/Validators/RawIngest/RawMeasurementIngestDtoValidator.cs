using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.RawIngest;

public class RawMeasurementIngestDtoValidator : AbstractValidator<RawMeasurementIngestDto>
{
    public RawMeasurementIngestDtoValidator()
    {
        RuleFor(x => x.Time).NotEmpty()
            .Must(t => t <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Time cannot be more than 5 minutes in the future.")
            .Must(t => t >= DateTime.UtcNow.AddDays(-30))
            .WithMessage("Time cannot be older than 30 days.");
        RuleFor(x => x.EmissionSourceId).NotEmpty();
        RuleFor(x => x.PollutantId).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.Quality).IsInEnum();
    }
}

public class RawProcessParameterIngestDtoValidator : AbstractValidator<RawProcessParameterIngestDto>
{
    public RawProcessParameterIngestDtoValidator()
    {
        RuleFor(x => x.Time).NotEmpty()
            .Must(t => t <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Time cannot be more than 5 minutes in the future.")
            .Must(t => t >= DateTime.UtcNow.AddDays(-30))
            .WithMessage("Time cannot be older than 30 days.");
        RuleFor(x => x.EmissionSourceId).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.ParameterType).IsInEnum();
        RuleFor(x => x.Quality).IsInEnum();
    }
}

// Generic argument matches the controller's [FromBody] parameter runtime type — the global
// ModelStateValidationAttribute does an exact-type lookup of IValidator<T> via the runtime
// type of each action argument, so List<T> here must mirror the controller signature.
public class RawMeasurementBatchValidator : AbstractValidator<List<RawMeasurementIngestDto>>
{
    public RawMeasurementBatchValidator()
    {
        RuleFor(x => x).NotEmpty()
            .Must(b => b.Count <= 1000)
            .WithMessage("Batch size must not exceed 1000.");
        RuleForEach(x => x).SetValidator(new RawMeasurementIngestDtoValidator());
    }
}

public class RawProcessParameterBatchValidator : AbstractValidator<List<RawProcessParameterIngestDto>>
{
    public RawProcessParameterBatchValidator()
    {
        RuleFor(x => x).NotEmpty()
            .Must(b => b.Count <= 1000)
            .WithMessage("Batch size must not exceed 1000.");
        RuleForEach(x => x).SetValidator(new RawProcessParameterIngestDtoValidator());
    }
}
