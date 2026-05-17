using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.ProcessParameters;

public class ProcessParameterLatestQueryDtoValidator
    : AbstractValidator<ProcessParameterLatestQueryDto>
{
    public ProcessParameterLatestQueryDtoValidator()
    {
        RuleFor(x => x.EmissionSourceId).NotEmpty();
        When(x => x.ParameterTypes is { Length: > 0 },
            () => RuleForEach(x => x.ParameterTypes!).IsInEnum());
    }
}
