using Application.Common.Interfaces.Persistence;
using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Permits.Commands;

public class RevokePermitCommandCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<RevokePermitCommand, Either<PermitException, Permit>>
{
    public async Task<Either<PermitException, Permit>> Handle(
        RevokePermitCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
            .BindAsync(p => UpdateEntity(p, cancellationToken));
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
                        "Only Active permit can be revoked.");
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
            entity.ChangeStatus(PermitStatus.Revoked);
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