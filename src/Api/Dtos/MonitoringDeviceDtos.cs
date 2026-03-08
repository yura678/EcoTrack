using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record MonitoringDeviceQueryDto(
    int Page = 1,
    int PageSize = 20);

public record CreateMonitoringDeviceDto(
    Guid? EmissionSourceId,
    string Model,
    string SerialNumber,
    MonitoringDeviceType Type,
    bool IsOnline,
    string? Notes);

public record UpdateMonitoringDeviceDto(
    Guid? EmissionSourceId,
    bool IsOnline,
    string? Notes);

public record MonitoringDeviceDto(
    Guid Id,
    Guid? EmissionSourceId,
    EmissionSourceDto? EmissionSourceDto,
    Guid InstallationId,
    InstallationDto? InstallationDto,
    string Model,
    string SerialNumber,
    MonitoringDeviceType Type,
    DateTime? InstalledAt,
    bool IsOnline,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? Notes)
{
    public static MonitoringDeviceDto FromDomainModel(MonitoringDevice monitoringDevice)
    {
        return new MonitoringDeviceDto(
            Id: monitoringDevice.Id,
            EmissionSourceId: monitoringDevice.EmissionSourceId,
            EmissionSourceDto: monitoringDevice.EmissionSource is not null
                ? EmissionSourceDto.FromDomainModel(monitoringDevice.EmissionSource)
                : null,
            InstallationId: monitoringDevice.InstallationId,
            InstallationDto: monitoringDevice.Installation is not null
                ? InstallationDto.FromDomainModel(monitoringDevice.Installation)
                : null,
            Model: monitoringDevice.Model,
            SerialNumber: monitoringDevice.SerialNumber,
            Type: monitoringDevice.Type,
            InstalledAt: monitoringDevice.InstalledAt,
            IsOnline: monitoringDevice.IsOnline,
            CreatedAt: monitoringDevice.CreatedAt,
            UpdatedAt: monitoringDevice.UpdatedAt,
            Notes: monitoringDevice.Notes
        );
    }
}