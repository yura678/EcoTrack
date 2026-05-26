using Application.Common.Interfaces.Persistence;
using Application.Features.ComplianceEvents.Exceptions;
using Application.Features.ComplianceEvents.Notifications;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.ComplianceEvents.Commands.ReopenComplianceEvent;

public class ReopenComplianceEventCommandHandler(
    IUnitOfWork unitOfWork,
    IMediator mediator)
    : IRequestHandler<ReopenComplianceEventCommand,
        Either<ComplianceEventException, ComplianceEvent>>
{
    public async Task<Either<ComplianceEventException, ComplianceEvent>> Handle(
        ReopenComplianceEventCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entityOption = await unitOfWork.ComplianceEventRepository.GetByIdAsync(request.Id, cancellationToken);

            return await entityOption.MatchAsync<ComplianceEvent, Either<ComplianceEventException, ComplianceEvent>>(
                async entity =>
                {
                    entity.ChangeStatus(ComplianceEventStatus.Open);
                    unitOfWork.ComplianceEventRepository.Update(entity);
                    await unitOfWork.SaveChangesAsync(cancellationToken);

                    // Distinct notification from Opened — the SPA may distinguish "newly
                    // detected by the system" from "re-opened by a human", e.g. show a
                    // different toast or icon.
                    await mediator.Publish(
                        new ComplianceEventReopenedNotification(entity.Id), cancellationToken);
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
