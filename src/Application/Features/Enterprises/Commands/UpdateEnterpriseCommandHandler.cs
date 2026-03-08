using Application.Common.Interfaces.Persistence;
using Application.Features.Enterprises.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.Enterprises.Commands;



public class UpdateEnterpriseCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateEnterpriseCommand, Either<EnterpriseException, Enterprise>>
{
    public async Task<Either<EnterpriseException, Enterprise>> Handle(UpdateEnterpriseCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckSector(request.SectorId, cancellationToken)
            .BindAsync(_ => CheckEnterprise(request.Id, cancellationToken))
            .BindAsync(e => UpdateEntity(e, request, cancellationToken));
    }

    private async Task<Either<EnterpriseException, Enterprise>> CheckEnterprise(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.EnterpriseRepository.GetByIdAsync(id, cancellationToken);

        return entity.Match<Either<EnterpriseException, Enterprise>>(
            e => e,
            () => new EnterpriseNotFoundException(id)
        );
    }

    private async Task<Either<EnterpriseException, Unit>> CheckSector(
        Guid sectorId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.SectorRepository.GetByIdAsync(sectorId, cancellationToken);

        return entity.Match<Either<EnterpriseException, Unit>>(
            _ => Unit.Default,
            () => new SectorNotFoundException(Guid.NewGuid(), sectorId)
        );
    }

    private async Task<Either<EnterpriseException, Enterprise>> UpdateEntity(
        Enterprise entity,
        UpdateEnterpriseCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.UpdateDetails(request.Name, request.Address, request.RiskGroup, request.SectorId);
            var updatedEnterprise = unitOfWork.EnterpriseRepository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return updatedEnterprise;
        }
        catch (Exception exception)
        {
            return new UnhandledEnterpriseException(Guid.NewGuid(), exception);
        }
    }
}