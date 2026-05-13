using Application.Common.Interfaces.Persistence;
using Application.Features.DevicePollutantCapabilities.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.DevicePollutantCapabilities.Commands;

public class DeleteDevicePollutantCapabilityCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteDevicePollutantCapabilityCommand,
        Either<DevicePollutantCapabilityException, DevicePollutantCapability>>
{
    public async Task<Either<DevicePollutantCapabilityException, DevicePollutantCapability>> Handle(
        DeleteDevicePollutantCapabilityCommand request, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.DevicePollutantCapabilityRepository
            .GetByIdAsync(request.Id, cancellationToken);

        return await entity.Match<Task<Either<DevicePollutantCapabilityException, DevicePollutantCapability>>>(
            async e =>
            {
                try
                {
                    var deleted = unitOfWork.DevicePollutantCapabilityRepository.Delete(e);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return deleted;
                }
                catch (Exception exception)
                {
                    return new UnhandledDevicePollutantCapabilityException(e.Id, exception);
                }
            },
            () => Task.FromResult<Either<DevicePollutantCapabilityException, DevicePollutantCapability>>(
                new DevicePollutantCapabilityNotFoundException(request.Id))
        );
    }
}
