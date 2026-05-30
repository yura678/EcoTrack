using Application.Features.ComplianceEvents.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.ComplianceEvents.Commands.BulkCloseComplianceEvents;

/// <summary>
/// Closes many compliance events in one request with a single shared resolution reason + note.
/// Per-event problems (not found, wrong status) are reported in the result's <see cref="BulkCloseFailure"/>
/// list rather than failing the whole batch; only a catastrophic error surfaces as the Either-left.
/// </summary>
public class BulkCloseComplianceEventsCommand
    : IRequest<Either<ComplianceEventException, BulkCloseComplianceEventsResult>>,
        IValidatableModel<BulkCloseComplianceEventsCommand>
{
    public required IReadOnlyList<Guid> Ids { get; init; }
    public required ResolutionReason Reason { get; init; }
    public string? Note { get; init; }

    public IValidator<BulkCloseComplianceEventsCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<BulkCloseComplianceEventsCommand> validator)
    {
        validator.RuleFor(x => x.Ids).NotEmpty();
        validator.RuleFor(x => x.Ids)
            .Must(ids => ids.Count <= 200)
            .WithMessage("A bulk close can target at most 200 events.");
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

public record BulkCloseComplianceEventsResult(
    IReadOnlyList<Guid> ClosedIds,
    IReadOnlyList<BulkCloseFailure> Failed);

public record BulkCloseFailure(Guid Id, string Reason);
