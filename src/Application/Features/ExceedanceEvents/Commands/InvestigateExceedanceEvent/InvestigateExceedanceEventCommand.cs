using Application.Features.ExceedanceEvents.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.ExceedanceEvents.Commands.InvestigateExceedanceEvent;

public class InvestigateExceedanceEventCommand : IRequest<Either<ExceedanceEventException, ExceedanceEvent>>,
    IValidatableModel<InvestigateExceedanceEventCommand>

{
    public required Guid Id { get; init; }

    public IValidator<InvestigateExceedanceEventCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<InvestigateExceedanceEventCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}