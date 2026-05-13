using Application.Features.ExceedanceEvents.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.ExceedanceEvents.Commands.CloseExceedanceEvent;

public class CloseExceedanceEventCommand : IRequest<Either<ExceedanceEventException, ExceedanceEvent>>,
    IValidatableModel<CloseExceedanceEventCommand>
{
    public required Guid Id { get; init; }

    public IValidator<CloseExceedanceEventCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CloseExceedanceEventCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}