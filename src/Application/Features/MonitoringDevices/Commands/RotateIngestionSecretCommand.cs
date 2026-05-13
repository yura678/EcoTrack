using Application.Features.MonitoringDevices.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.MonitoringDevices.Commands;

public class RotateIngestionSecretCommand
    : IRequest<Either<MonitoringDeviceException, RotateIngestionSecretResult>>,
        IValidatableModel<RotateIngestionSecretCommand>
{
    public required Guid DeviceId { get; init; }

    public IValidator<RotateIngestionSecretCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RotateIngestionSecretCommand> validator)
    {
        validator.RuleFor(x => x.DeviceId).NotEmpty();
        return validator;
    }
}

public record RotateIngestionSecretResult(Guid DeviceId, string Secret);
