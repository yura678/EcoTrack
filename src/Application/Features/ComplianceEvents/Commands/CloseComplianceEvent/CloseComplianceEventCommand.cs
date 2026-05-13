using Application.Features.ComplianceEvents.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.ComplianceEvents.Commands.CloseComplianceEvent;

public class CloseComplianceEventCommand : IRequest<Either<ComplianceEventException, ComplianceEvent>>,
    IValidatableModel<CloseComplianceEventCommand>
{
    public required Guid Id { get; init; }

    public IValidator<CloseComplianceEventCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CloseComplianceEventCommand> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        return validator;
    }
}
