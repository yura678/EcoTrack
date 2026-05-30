using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.ComplianceEvents.Exceptions;
using Application.Features.ComplianceEvents.Notifications;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.ComplianceEvents.Commands.BulkCloseComplianceEvents;

public class BulkCloseComplianceEventsCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IPublisher publisher)
    : IRequestHandler<BulkCloseComplianceEventsCommand,
        Either<ComplianceEventException, BulkCloseComplianceEventsResult>>
{
    public async Task<Either<ComplianceEventException, BulkCloseComplianceEventsResult>> Handle(
        BulkCloseComplianceEventsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = currentUserService.GetCurrentUserId();
            var closed = new List<Guid>();
            var failed = new List<BulkCloseFailure>();

            foreach (var id in request.Ids.Distinct())
            {
                // GetByIdAsync is tenant-filtered, so an id from another enterprise comes back None
                // and lands in `failed` as "not found" — no cross-tenant leakage.
                var entityOption = await unitOfWork.ComplianceEventRepository.GetByIdAsync(id, cancellationToken);
                entityOption.Match(
                    Some: entity =>
                    {
                        if (entity.Status != ComplianceEventStatus.Open
                            && entity.Status != ComplianceEventStatus.Investigating)
                        {
                            failed.Add(new BulkCloseFailure(id,
                                $"Event is {entity.Status}. Only Open or Investigating events can be closed."));
                            return;
                        }

                        entity.Close(request.Reason, request.Note, userId);
                        unitOfWork.ComplianceEventRepository.Update(entity);
                        closed.Add(id);
                    },
                    None: () => failed.Add(new BulkCloseFailure(id, "Compliance event not found.")));
            }

            if (closed.Count > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                // Publish AFTER the commit — the broadcast handler reloads each event from the DB,
                // so one notification per closed event drives the per-row SignalR transition.
                foreach (var id in closed)
                {
                    await publisher.Publish(new ComplianceEventClosedNotification(id), cancellationToken);
                }
            }

            return new BulkCloseComplianceEventsResult(closed, failed);
        }
        catch (Exception exception)
        {
            return new UnhandledComplianceEventException(Guid.Empty, exception);
        }
    }
}
