using Application.Features.NotificationSubscriptions.Exceptions;
using Domain.Entities.Monitoring;
using Domain.Entities.Notifications;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.NotificationSubscriptions.Commands;

public class CreateNotificationSubscriptionCommand
    : IRequest<Either<NotificationSubscriptionException, NotificationSubscription>>,
        IValidatableModel<CreateNotificationSubscriptionCommand>
{
    public required NotificationChannel Channel { get; init; }
    public string? Email { get; init; }
    public string? WebhookUrl { get; init; }
    public string? WebhookSecret { get; init; }
    public ComplianceEventType[]? EventTypes { get; init; }
    public Guid[]? EmissionSourceIds { get; init; }

    public IValidator<CreateNotificationSubscriptionCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreateNotificationSubscriptionCommand> validator)
    {
        validator.RuleFor(x => x.Channel).IsInEnum();

        validator.RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320)
            .When(x => x.Channel == NotificationChannel.Email);

        validator.RuleFor(x => x.WebhookUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(BeAbsoluteHttpUrl)
            .WithMessage("WebhookUrl must be an absolute http(s) URL.")
            .When(x => x.Channel == NotificationChannel.Webhook);

        validator.RuleFor(x => x.WebhookSecret)
            .NotEmpty()
            .MinimumLength(16)
            .MaximumLength(256)
            .When(x => x.Channel == NotificationChannel.Webhook);

        validator.RuleForEach(x => x.EventTypes).IsInEnum();

        return validator;
    }

    private static bool BeAbsoluteHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
