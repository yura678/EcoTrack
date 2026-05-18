using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.NotificationSubscriptions.Exceptions;
using Domain.Entities.Notifications;
using LanguageExt;
using MediatR;

namespace Application.Features.NotificationSubscriptions.Commands;

public class UpdateNotificationSubscriptionCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateNotificationSubscriptionCommand,
        Either<NotificationSubscriptionException, NotificationSubscription>>
{
    public async Task<Either<NotificationSubscriptionException, NotificationSubscription>> Handle(
        UpdateNotificationSubscriptionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var entityOption = await unitOfWork.NotificationSubscriptionRepository
                .GetByIdAsync(request.Id, cancellationToken);
            return await entityOption.MatchAsync<NotificationSubscription, Either<NotificationSubscriptionException, NotificationSubscription>>(
                async entity =>
                {
                    var currentUserId = currentUserService.GetCurrentUserId();
                    if (currentUserId != entity.UserId)
                    {
                        return new NotificationSubscriptionForbiddenException(request.Id);
                    }

                    if (entity.Channel == NotificationChannel.Email && request.Email is not null)
                    {
                        entity.UpdateEmailTarget(request.Email);
                    }
                    if (entity.Channel == NotificationChannel.Webhook && request.WebhookUrl is not null)
                    {
                        entity.UpdateWebhookTarget(request.WebhookUrl, request.WebhookSecret);
                    }

                    entity.UpdateFilters(request.EventTypes, request.EmissionSourceIds, request.IsEnabled);
                    unitOfWork.NotificationSubscriptionRepository.Update(entity);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return entity;
                },
                () => new NotificationSubscriptionNotFoundException(request.Id));
        }
        catch (Exception ex)
        {
            return new UnhandledNotificationSubscriptionException(request.Id, ex);
        }
    }
}
