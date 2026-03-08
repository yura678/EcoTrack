using Application.Features.MonitoringDevices.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.MonitoringDevices.Commands;

public class CreateMonitoringDeviceCommand : IRequest<Either<MonitoringDeviceException, MonitoringDevice>>,
    IValidatableModel<CreateMonitoringDeviceCommand>
{
    public required Guid? EmissionSourceId { get; init; }
    public required Guid InstallationId { get; init; }
    public required string Model { get; init; }
    public required string SerialNumber { get; init; }
    public required MonitoringDeviceType Type { get; init; }
    public required bool IsOnline { get; init; }
    public required string? Notes { get; init; }

    public IValidator<CreateMonitoringDeviceCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreateMonitoringDeviceCommand> validator)
    {
        validator.RuleFor(x => x.InstallationId)
            .NotEmpty();

        validator.RuleFor(x => x.Model)
            .NotEmpty()
            .MaximumLength(100);

        validator.RuleFor(x => x.SerialNumber)
            .NotEmpty()
            .MaximumLength(100);

        validator.RuleFor(x => x.Type)
            .IsInEnum();

        validator.RuleFor(x => x.IsOnline)
            .NotNull();

        validator.RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null);

        return validator;
    }
}