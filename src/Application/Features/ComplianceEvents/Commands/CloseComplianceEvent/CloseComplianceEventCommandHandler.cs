using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.ComplianceEvents.Exceptions;
using Application.Features.ComplianceEvents.Notifications;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.ComplianceEvents.Commands.CloseComplianceEvent;

public class CloseComplianceEventCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IPublisher publisher)
    : IRequestHandler<CloseComplianceEventCommand,
        Either<ComplianceEventException, ComplianceEvent>>
{
    public async Task<Either<ComplianceEventException, ComplianceEvent>> Handle(
        CloseComplianceEventCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entityOption = await unitOfWork.ComplianceEventRepository.GetByIdAsync(request.Id, cancellationToken);

            return await entityOption.MatchAsync<ComplianceEvent, Either<ComplianceEventException, ComplianceEvent>>(
                async entity =>
                {
                    if (entity.Status != ComplianceEventStatus.Open
                        && entity.Status != ComplianceEventStatus.Investigating)
                    {
                        return new ComplianceEventInvalidStatusException(
                            request.Id, entity.Status,
                            "Only Open or Investigating events can be closed.");
                    }

                    entity.Close(request.Reason, request.Note, currentUserService.GetCurrentUserId());
                    unitOfWork.ComplianceEventRepository.Update(entity);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    // Mirrors Investigate/Reopen handlers — without this publish, the SignalR
                    // broadcast handler never fires for manual closes and live clients never
                    // see the row transition out of Open/Investigating.
                    await publisher.Publish(
                        new ComplianceEventClosedNotification(entity.Id), cancellationToken);
                    return entity;
                },
                () => new ComplianceEventNotFoundException(request.Id));
        }
        catch (Exception exception)
        {
            return new UnhandledComplianceEventException(request.Id, exception);
        }
    }
}
