using Application.Features.MonitoringDevices.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.MonitoringDevices.Commands;

public class UpdateMonitoringDeviceCommand : IRequest<Either<MonitoringDeviceException, MonitoringDevice>>,
    IValidatableModel<UpdateMonitoringDeviceCommand>
{
    public required Guid Id { get; init; }
    public required Guid? EmissionSourceId { get; init; }
    public required bool IsOnline { get; init; }
    public required string? Notes { get; init; }

    public IValidator<UpdateMonitoringDeviceCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateMonitoringDeviceCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();
        
        validator.RuleFor(x => x.IsOnline)
            .NotNull();

        validator.RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null);

        return validator;
    }
}