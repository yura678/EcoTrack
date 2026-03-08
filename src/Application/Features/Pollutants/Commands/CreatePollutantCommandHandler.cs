using Application.Common.Interfaces.Persistence;
using Application.Features.Pollutants.Exceptions;
using Domain.Entities.EmissionSources;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.Pollutants.Commands;



public class CreatePollutantCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreatePollutantCommand, Either<PollutantException, Pollutant>>
{
    public async Task<Either<PollutantException, Pollutant>> Handle(
        CreatePollutantCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckPollutantCode(request.Code, cancellationToken)
            .BindAsync(_ => CheckPollutantName(request.Name, cancellationToken))
            .BindAsync(_ => CreateEntity(request, cancellationToken));
    }

    private async Task<Either<PollutantException, Unit>> CheckPollutantCode(
        string code,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.PollutantRepository.GetByCodeAsync(code, cancellationToken);

        return entity.Match<Either<PollutantException, Unit>>(
            p => new PollutantCodeAlreadyExistsException(p.Id, p.Code),
            () => Unit.Default
        );
    }

    private async Task<Either<PollutantException, Unit>> CheckPollutantName(
        string name,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.PollutantRepository.GetByNameAsync(name, cancellationToken);

        return entity.Match<Either<PollutantException, Unit>>(
            p => new PollutantNameAlreadyExistsException(p.Id, p.Name),
            () => Unit.Default
        );
    }

    private async Task<Either<PollutantException, Pollutant>> CreateEntity(
        CreatePollutantCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var newPollutant = await unitOfWork.PollutantRepository.AddAsync(
                Pollutant.New(Guid.NewGuid(), request.Name, request.Code), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return newPollutant;
        }
        catch (Exception exception)
        {
            return new UnhandledPollutantException(Guid.Empty, exception);
        }
    }
}