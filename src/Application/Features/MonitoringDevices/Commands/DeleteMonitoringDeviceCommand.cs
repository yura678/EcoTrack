using Application.Features.MonitoringDevices.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.MonitoringDevices.Commands;

public class DeleteMonitoringDeviceCommand : IRequest<Either<MonitoringDeviceException, MonitoringDevice>>,
    IValidatableModel<DeleteMonitoringDeviceCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeleteMonitoringDeviceCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeleteMonitoringDeviceCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}