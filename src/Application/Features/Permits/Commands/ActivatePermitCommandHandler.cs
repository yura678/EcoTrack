using Application.Common.Interfaces.Persistence;
using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Permits.Commands;

public class ActivatePermitCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<ActivatePermitCommand, Either<PermitException, Permit>>
{
    public async Task<Either<PermitException, Permit>> Handle(
        ActivatePermitCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
            .BindAsync(m => CheckNoActivePermitExists(m, cancellationToken))
            .BindAsync(m => UpdateEntity(m, cancellationToken));
    }

    private async Task<Either<PermitException, Permit>> CheckId(
        Guid permitId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.PermitRepository.GetByIdAsync(permitId, cancellationToken);

        return entity.Match<Either<PermitException, Permit>>(
            p =>
            {
                if (p.PermitStatus != PermitStatus.Draft)
                {
                    return new PermitInvalidStatusException(
                        permitId,
                        p.PermitStatus,
                        "Only Draft permits can be activated.");
                }

                return p;
            },
            () => new PermitNotFoundException(permitId)
        );
    }

    private async Task<Either<PermitException, Permit>> CheckNoActivePermitExists(
        Permit permit,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.PermitRepository.GetActiveAsync(
            permit.InstallationId, permit.PermitType, cancellationToken);

        return entity.Match<Either<PermitException, Permit>>(
            m => new ActivePermitAlreadyExistsException(m.Id),
            () => permit
        );
    }

    private async Task<Either<PermitException, Permit>> UpdateEntity(
        Permit entity,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.ChangeStatus(PermitStatus.Active);
            var updatedPermit = unitOfWork.PermitRepository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return updatedPermit;
        }
        catch (Exception exception)
        {
            return new UnhandledPermitException(entity.Id, exception);
        }
    }
}