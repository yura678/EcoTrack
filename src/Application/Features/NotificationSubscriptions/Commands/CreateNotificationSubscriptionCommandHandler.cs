using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.NotificationSubscriptions.Exceptions;
using Domain.Entities.Notifications;
using LanguageExt;
using MediatR;

namespace Application.Features.NotificationSubscriptions.Commands;

public class CreateNotificationSubscriptionCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateNotificationSubscriptionCommand,
        Either<NotificationSubscriptionException, NotificationSubscription>>
{
    public async Task<Either<NotificationSubscriptionException, NotificationSubscription>> Handle(
        CreateNotificationSubscriptionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = currentUserService.GetCurrentUserId();
            var enterpriseId = currentUserService.GetCurrentEnterpriseId();
            if (userId is null || enterpriseId is null)
            {
                return new UnhandledNotificationSubscriptionException(
                    Guid.Empty,
                    new InvalidOperationException("Current user or enterprise is unknown."));
            }

            var entity = request.Channel switch
            {
                NotificationChannel.Email => NotificationSubscription.NewEmail(
                    Guid.NewGuid(), userId.Value, request.Email!,
                    request.EventTypes, request.EmissionSourceIds),
                NotificationChannel.Webhook => NotificationSubscription.NewWebhook(
                    Guid.NewGuid(), userId.Value, request.WebhookUrl!, request.WebhookSecret!,
                    request.EventTypes, request.EmissionSourceIds),
                _ => throw new InvalidOperationException(
                    $"Unsupported channel {request.Channel}.")
            };
            entity.AssignTenant(enterpriseId.Value);

            var saved = await unitOfWork.NotificationSubscriptionRepository.AddAsync(entity, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return saved;
        }
        catch (Exception ex)
        {
            return new UnhandledNotificationSubscriptionException(Guid.Empty, ex);
        }
    }
}
