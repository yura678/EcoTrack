using Application.Common.Interfaces.Persistence;
using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Permits.Commands;

public class DeletePermitCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeletePermitCommand, Either<PermitException, Permit>>
{
    public async Task<Either<PermitException, Permit>> Handle(
        DeletePermitCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
            .BindAsync(m => DeleteEntity(m, cancellationToken));
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
                        "Only Draft permits can be deleted.");
                }

                return p;
            },
            () => new PermitNotFoundException(permitId)
        );
    }


    private async Task<Either<PermitException, Permit>> DeleteEntity(
        Permit entity,
        CancellationToken cancellationToken)
    {
        try
        {
            var deletedPermit = unitOfWork.PermitRepository.Delete(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return deletedPermit;
        }
        catch (Exception exception)
        {
            return new UnhandledPermitException(entity.Id, exception);
        }
    }
}