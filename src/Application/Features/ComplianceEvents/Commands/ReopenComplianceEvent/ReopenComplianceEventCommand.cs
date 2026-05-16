using Application.Features.ComplianceEvents.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.ComplianceEvents.Commands.ReopenComplianceEvent;

public class ReopenComplianceEventCommand : IRequest<Either<ComplianceEventException, ComplianceEvent>>,
    IValidatableModel<ReopenComplianceEventCommand>
{
    public required Guid Id { get; init; }

    public IValidator<ReopenComplianceEventCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<ReopenComplianceEventCommand> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        return validator;
    }
}
