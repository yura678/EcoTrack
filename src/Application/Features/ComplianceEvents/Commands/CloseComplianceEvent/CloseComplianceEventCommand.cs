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
    public required ResolutionReason Reason { get; init; }
    public string? Note { get; init; }

    public IValidator<CloseComplianceEventCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CloseComplianceEventCommand> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        validator.RuleFor(x => x.Reason).IsInEnum();
        // Other is free-form so we require a note to keep the audit trail meaningful.
        validator.RuleFor(x => x.Note)
            .NotEmpty()
            .When(x => x.Reason == ResolutionReason.Other)
            .WithMessage("Note is required when Reason is Other.");
        validator.RuleFor(x => x.Note).MaximumLength(1000);
        return validator;
    }
}
