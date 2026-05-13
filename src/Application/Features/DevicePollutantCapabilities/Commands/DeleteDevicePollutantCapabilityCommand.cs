using Application.Features.DevicePollutantCapabilities.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.DevicePollutantCapabilities.Commands;

public class DeleteDevicePollutantCapabilityCommand
    : IRequest<Either<DevicePollutantCapabilityException, DevicePollutantCapability>>,
        IValidatableModel<DeleteDevicePollutantCapabilityCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeleteDevicePollutantCapabilityCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeleteDevicePollutantCapabilityCommand> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        return validator;
    }
}
