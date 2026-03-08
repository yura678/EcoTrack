using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.MonitoringDevices;

public class UpdateMonitoringDeviceDtoValidator : AbstractValidator<UpdateMonitoringDeviceDto>
{
    public UpdateMonitoringDeviceDtoValidator()
    {
        RuleFor(x => x.IsOnline)
            .NotNull();

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null);
    }
}