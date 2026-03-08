using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Permits;

public class UpdatePermitDtoValidator : AbstractValidator<UpdatePermitDto>
{
    public UpdatePermitDtoValidator()
    {
        RuleFor(x => x.Number)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.PermitType).IsInEnum();

        RuleFor(x => x.IssuedAt).NotEmpty();
        RuleFor(x => x.ValidUntil).NotEmpty();

        RuleFor(x => x)
            .Must(x => x.IssuedAt < x.ValidUntil)
            .WithMessage("Issue date must be before expiration date.")
            .When(x => x.IssuedAt != default && x.ValidUntil != default);

        RuleFor(x => x.Authority)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null);

        RuleFor(x => x.EmissionLimits)
            .NotNull()
            .Must(r => r.Any())
            .WithMessage("Permit must contain at least one emission limit.");

        RuleForEach(x => x.EmissionLimits)
            .SetValidator(new UpdateEmissionLimitDtoValidator());

        RuleFor(x => x.EmissionLimits)
            .Must(NoOverlappingLimits)
            .WithMessage("The list contains overlapping limits for the same Source, Pollutant, and Period.");
    }

    private bool NoOverlappingLimits(IReadOnlyList<UpdateEmissionLimitDto> limits)
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

    private bool AreOverlapping(UpdateEmissionLimitDto a, UpdateEmissionLimitDto b)
    {
        // Логіка: Перетин є, якщо (StartA <= EndB) І (EndA >= StartB)

        DateTime startA = a.ValidFrom ?? DateTime.MinValue;
        DateTime startB = b.ValidFrom ?? DateTime.MinValue;

        DateTime endA = a.ValidTo ?? DateTime.MaxValue;
        DateTime endB = b.ValidTo ?? DateTime.MaxValue;

        return startA <= endB && endA >= startB;
    }
}

public class UpdateEmissionLimitDtoValidator : AbstractValidator<UpdateEmissionLimitDto>
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

    private bool HaveValidDateRange(UpdateEmissionLimitDto dto)
    {
        return dto.ValidFrom!.Value <= dto.ValidTo!.Value;
    }
}