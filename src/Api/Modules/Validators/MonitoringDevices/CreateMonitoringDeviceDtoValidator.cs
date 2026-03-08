using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.MonitoringDevices;

public class CreateMonitoringDeviceDtoValidator : AbstractValidator<CreateMonitoringDeviceDto>
{
    public CreateMonitoringDeviceDtoValidator()
    {

        RuleFor(x => x.Model)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.SerialNumber)
            .NotEmpty()
            .MaximumLength(100);
        
        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.IsOnline)
            .NotNull();

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null);
    }
}