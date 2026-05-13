using Application.Common.Interfaces.Persistence;
using Application.Features.ComplianceEvents.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.ComplianceEvents.Commands.InvestigateComplianceEvent;

public class InvestigateComplianceEventCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<InvestigateComplianceEventCommand,
        Either<ComplianceEventException, ComplianceEvent>>
{
    public async Task<Either<ComplianceEventException, ComplianceEvent>> Handle(
        InvestigateComplianceEventCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entityOption = await unitOfWork.ComplianceEventRepository.GetByIdAsync(request.Id, cancellationToken);

            return await entityOption.MatchAsync<ComplianceEvent, Either<ComplianceEventException, ComplianceEvent>>(
                async entity =>
                {
                    entity.ChangeStatus(ComplianceEventStatus.Investigating);
                    unitOfWork.ComplianceEventRepository.Update(entity);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
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
