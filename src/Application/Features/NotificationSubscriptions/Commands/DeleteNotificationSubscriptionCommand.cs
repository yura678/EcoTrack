using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.NotificationSubscriptions.Exceptions;
using Domain.Entities.Notifications;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.NotificationSubscriptions.Commands;

public class DeleteNotificationSubscriptionCommand
    : IRequest<Either<NotificationSubscriptionException, NotificationSubscription>>,
        IValidatableModel<DeleteNotificationSubscriptionCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeleteNotificationSubscriptionCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeleteNotificationSubscriptionCommand> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        return validator;
    }
}

public class DeleteNotificationSubscriptionCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteNotificationSubscriptionCommand,
        Either<NotificationSubscriptionException, NotificationSubscription>>
{
    public async Task<Either<NotificationSubscriptionException, NotificationSubscription>> Handle(
        DeleteNotificationSubscriptionCommand request, CancellationToken cancellationToken)
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

                    unitOfWork.NotificationSubscriptionRepository.Delete(entity);
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
