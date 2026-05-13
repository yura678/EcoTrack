using Application.Features.ComplianceEvents.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.ComplianceEvents.Commands.InvestigateComplianceEvent;

public class InvestigateComplianceEventCommand : IRequest<Either<ComplianceEventException, ComplianceEvent>>,
    IValidatableModel<InvestigateComplianceEventCommand>
{
    public required Guid Id { get; init; }

    public IValidator<InvestigateComplianceEventCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<InvestigateComplianceEventCommand> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        return validator;
    }
}
