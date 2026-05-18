using Application.Features.NotificationSubscriptions.Exceptions;
using Domain.Entities.Monitoring;
using Domain.Entities.Notifications;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.NotificationSubscriptions.Commands;

public class UpdateNotificationSubscriptionCommand
    : IRequest<Either<NotificationSubscriptionException, NotificationSubscription>>,
        IValidatableModel<UpdateNotificationSubscriptionCommand>
{
    public required Guid Id { get; init; }
    public string? Email { get; init; }
    public string? WebhookUrl { get; init; }
    public string? WebhookSecret { get; init; }
    public ComplianceEventType[]? EventTypes { get; init; }
    public Guid[]? EmissionSourceIds { get; init; }
    public required bool IsEnabled { get; init; }

    public IValidator<UpdateNotificationSubscriptionCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateNotificationSubscriptionCommand> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        validator.RuleFor(x => x.Email).EmailAddress().MaximumLength(320)
            .When(x => !string.IsNullOrEmpty(x.Email));
        validator.RuleFor(x => x.WebhookUrl).MaximumLength(2048)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .WithMessage("WebhookUrl must be an absolute http(s) URL.")
            .When(x => !string.IsNullOrEmpty(x.WebhookUrl));
        validator.RuleFor(x => x.WebhookSecret)
            .MinimumLength(16).MaximumLength(256)
            .When(x => !string.IsNullOrEmpty(x.WebhookSecret));
        validator.RuleForEach(x => x.EventTypes).IsInEnum();
        return validator;
    }
}
