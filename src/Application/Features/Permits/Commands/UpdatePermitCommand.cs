using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Permits.Commands;

public record UpdateEmissionLimitCommandDto(
    Guid? Id,
    decimal Value,
    AveragingWindow Period,
    Guid UnitId,
    Guid PollutantId,
    Guid EmissionSourceId,
    DateTime? ValidFrom,
    DateTime? ValidTo
);

public class UpdatePermitCommand : IRequest<Either<PermitException, Permit>>,
    IValidatableModel<UpdatePermitCommand>
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public required PermitType PermitType { get; init; }
    public required DateTime IssuedAt { get; init; }
    public required DateTime ValidUntil { get; init; }
    public required string Authority { get; init; }
    public required string? Notes { get; init; }

    public required IReadOnlyList<UpdateEmissionLimitCommandDto> EmissionLimits { get; init; }

    public IValidator<UpdatePermitCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdatePermitCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        validator.RuleFor(x => x.Number)
            .NotEmpty()
            .MaximumLength(50);

        validator.RuleFor(x => x.PermitType).IsInEnum();

        validator.RuleFor(x => x.IssuedAt).NotEmpty();
        validator.RuleFor(x => x.ValidUntil).NotEmpty();

        validator.RuleFor(x => x)
            .Must(x => x.IssuedAt < x.ValidUntil)
            .WithMessage("Issue date must be before expiration date.")
            .When(x => x.IssuedAt != default && x.ValidUntil != default);

        validator.RuleFor(x => x.Authority)
            .NotEmpty()
            .MaximumLength(200);

        validator.RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null);

        validator.RuleFor(x => x.EmissionLimits)
            .NotNull()
            .Must(r => r != null && r.Any())
            .WithMessage("Permit must contain at least one emission limit.");

        validator.RuleFor(x => x.EmissionLimits)
            .Must(NoOverlappingLimits)
            .When(x => x.EmissionLimits != null)
            .WithMessage("The list contains overlapping limits for the same Source, Pollutant, and Period.");

        validator.RuleForEach(x => x.EmissionLimits)
            .SetValidator(new UpdateEmissionLimitDtoValidator());

        return validator;
    }

    private bool NoOverlappingLimits(IReadOnlyList<UpdateEmissionLimitCommandDto> limits)
    {
        var groups = limits.GroupBy(x => new
        {
            x.EmissionSourceId,
            x.PollutantId,
            x.Period
        });

        foreach (var group in groups)
        {
            var groupList = group.ToList();

            if (groupList.Count > 1)
            {
                for (var i = 0; i < groupList.Count; i++)
                {
                    for (var j = i + 1; j < groupList.Count; j++)
                    {
                        var limitA = groupList[i];
                        var limitB = groupList[j];

                        if (AreOverlapping(limitA, limitB))
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    private bool AreOverlapping(UpdateEmissionLimitCommandDto a, UpdateEmissionLimitCommandDto b)
    {
        DateTime startA = a.ValidFrom ?? DateTime.MinValue;
        DateTime startB = b.ValidFrom ?? DateTime.MinValue;

        DateTime endA = a.ValidTo ?? DateTime.MaxValue;
        DateTime endB = b.ValidTo ?? DateTime.MaxValue;

        return startA <= endB && endA >= startB;
    }

    private class UpdateEmissionLimitDtoValidator : AbstractValidator<UpdateEmissionLimitCommandDto>
    {
        public UpdateEmissionLimitDtoValidator()
        {
            RuleFor(x => x.EmissionSourceId).NotEmpty();
            RuleFor(x => x.PollutantId).NotEmpty();
            RuleFor(x => x.UnitId).NotEmpty();

            RuleFor(x => x.Period).IsInEnum();

            RuleFor(x => x.Value)
                .GreaterThan(0)
                .WithMessage("Limit value must be greater than 0.");

            RuleFor(x => x)
                .Must(HaveValidDateRange)
                .WithMessage("ValidFrom cannot be later than ValidTo.")
                .When(x => x.ValidFrom.HasValue && x.ValidTo.HasValue);
        }

        private bool HaveValidDateRange(UpdateEmissionLimitCommandDto dto)
        {
            return dto.ValidFrom!.Value <= dto.ValidTo!.Value;
        }
    }
}