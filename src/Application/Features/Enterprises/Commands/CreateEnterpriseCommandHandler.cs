using Application.Common.Interfaces.Persistence;
using Application.Features.Enterprises.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.Enterprises.Commands;


public class CreateEnterpriseCommandHandler(
   IUnitOfWork unitOfWork)
    : IRequestHandler<CreateEnterpriseCommand, Either<EnterpriseException, Enterprise>>
{
    public async Task<Either<EnterpriseException, Enterprise>> Handle(CreateEnterpriseCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckEnterprise(request.Edrpou, cancellationToken)
            .BindAsync(_ => CheckSector(request.SectorId, cancellationToken))
            .BindAsync(_ => CreateEntity(request, cancellationToken));
    }

    private async Task<Either<EnterpriseException, Unit>> CheckEnterprise(
        string edrpou,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.EnterpriseRepository.GetByEdrpouAsync(edrpou, cancellationToken);

        return entity.Match<Either<EnterpriseException, Unit>>(
            e => new EnterpriseEdrpouAlreadyExistsException(e.Id, e.Edrpou),
            () => Unit.Default
        );
    }

    private async Task<Either<EnterpriseException, Unit>> CheckSector(
       Guid sectorId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.SectorRepository.GetByIdAsync(sectorId, cancellationToken);

        return entity.Match<Either<EnterpriseException, Unit>>(
            _ => Unit.Default,
            () => new SectorNotFoundException(Guid.Empty,  sectorId)
        );
    }

    private async Task<Either<EnterpriseException, Enterprise>> CreateEntity(
        CreateEnterpriseCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var newEnterprise = await unitOfWork.EnterpriseRepository.AddAsync(
                Enterprise.New(Guid.NewGuid(), request.Name, request.Edrpou, request.Address,
                    request.RiskGroup, request.SectorId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return newEnterprise;
        }
        catch (Exception exception)
        {
            return new UnhandledEnterpriseException(Guid.Empty, exception);
        }
    }
}