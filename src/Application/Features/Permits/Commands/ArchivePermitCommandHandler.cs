using Application.Common.Interfaces.Persistence;
using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Permits.Commands;

public class ArchivePermitCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<ArchivePermitCommand, Either<PermitException, Permit>>
{
    public async Task<Either<PermitException, Permit>> Handle(
        ArchivePermitCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
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
                if (p.PermitStatus != PermitStatus.Active)
                {
                    return new PermitInvalidStatusException(
                        permitId,
                        p.PermitStatus,
                        "Only Active permits can be archive.");
                }

                return p;
            },
            () => new PermitNotFoundException(permitId)
        );
    }

    private async Task<Either<PermitException, Permit>> UpdateEntity(
        Permit entity,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.ChangeStatus(PermitStatus.Archived);
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