using Api.Dtos;
using Domain.Entities.Notifications;
using FluentValidation;

namespace Api.Modules.Validators.Notifications;

public class CreateNotificationSubscriptionDtoValidator
    : AbstractValidator<CreateNotificationSubscriptionDto>
{
    public CreateNotificationSubscriptionDtoValidator()
    {
        RuleFor(x => x.Channel).IsInEnum();

        When(x => x.Channel == NotificationChannel.Email, () =>
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        });

        When(x => x.Channel == NotificationChannel.Webhook, () =>
        {
            RuleFor(x => x.WebhookUrl).NotEmpty().MaximumLength(2048)
                .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri)
                           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .WithMessage("WebhookUrl must be an absolute http(s) URL.");
            RuleFor(x => x.WebhookSecret).NotEmpty().MinimumLength(16).MaximumLength(256);
        });

        RuleForEach(x => x.EventTypes).IsInEnum();
    }
}

public class UpdateNotificationSubscriptionDtoValidator
    : AbstractValidator<UpdateNotificationSubscriptionDto>
{
    public UpdateNotificationSubscriptionDtoValidator()
    {
        When(x => !string.IsNullOrEmpty(x.Email), () =>
            RuleFor(x => x.Email!).EmailAddress().MaximumLength(320));

        When(x => !string.IsNullOrEmpty(x.WebhookUrl), () =>
            RuleFor(x => x.WebhookUrl!).MaximumLength(2048)
                .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri)
                           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .WithMessage("WebhookUrl must be an absolute http(s) URL."));

        When(x => !string.IsNullOrEmpty(x.WebhookSecret), () =>
            RuleFor(x => x.WebhookSecret!).MinimumLength(16).MaximumLength(256));

        RuleForEach(x => x.EventTypes).IsInEnum();
    }
}
