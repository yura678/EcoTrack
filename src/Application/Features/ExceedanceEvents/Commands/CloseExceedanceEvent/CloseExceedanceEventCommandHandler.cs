using Application.Common.Interfaces.Persistence;
using Application.Features.ExceedanceEvents.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.ExceedanceEvents.Commands.CloseExceedanceEvent;

public class CloseExceedanceEventCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<CloseExceedanceEventCommand,
        Either<ExceedanceEventException, ExceedanceEvent>>
{
    public async Task<Either<ExceedanceEventException, ExceedanceEvent>> Handle(
        CloseExceedanceEventCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entityOption = await unitOfWork.ExceedanceEventRepository.GetByIdAsync(request.Id, cancellationToken);

            return await entityOption.MatchAsync<ExceedanceEvent, Either<ExceedanceEventException, ExceedanceEvent>>(
                    async entity =>
                    {
                        entity.ChangeStatus(ExceedanceEventStatus.Closed);
                        unitOfWork.ExceedanceEventRepository.Update(entity);
                        await unitOfWork.SaveChangesAsync(cancellationToken);
                        return entity;
                    },
                    () => new ExceedanceEventNotFoundException(request.Id));
        }
        catch (Exception exception)
        {
            return new UnhandledExceedanceEventException(request.Id, exception);
        }
    }
}