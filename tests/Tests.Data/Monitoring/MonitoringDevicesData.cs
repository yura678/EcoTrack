using Domain.Entities.Monitoring;

namespace Tests.Data.Monitoring;

public static class MonitoringDevicesData
{
    public static MonitoringDevice FirstTestDevice(Guid emissionSourceId,
        Guid installationId)
        => MonitoringDevice.New(
            id: Guid.NewGuid(),
            emissionSourceId: emissionSourceId,
            installationId: installationId,
            model: "CEMS-1000",
            serialNumber: "SN-001",
            type: MonitoringDeviceType.CEMS,
            status: DeviceStatus.Operational,
            notes: "First device for testing"
        );


    public static MonitoringDevice SecondTestDevice(Guid emissionSourceId,
        Guid installationId)
        => MonitoringDevice.New(
            id: Guid.NewGuid(),
            emissionSourceId: emissionSourceId,
            installationId: installationId,
            model: "CEMS-2000",
            serialNumber: "SN-002",
            type: MonitoringDeviceType.CEMS,
            status: DeviceStatus.Offline,
            notes: "Second device for testing"
        );


    public static MonitoringDevice DeviceToCreate(Guid emissionSourceId,
        Guid installationId)
        => MonitoringDevice.New(
            id: Guid.NewGuid(),
            emissionSourceId: emissionSourceId,
            installationId: installationId,
            model: "CEMS-NEW",
            serialNumber: "SN-NEW",
            type: MonitoringDeviceType.CEMS,
            status: DeviceStatus.Operational,
            notes: "Created from test"
        );


    public static MonitoringDevice DeviceToUpdate(Guid emissionSourceId,
        Guid installationId)
        => MonitoringDevice.New(
            id: Guid.NewGuid(),
            emissionSourceId: emissionSourceId,
            installationId: installationId,
            model: "UPDATED-MODEL",
            serialNumber: "UPDATED-SN",
            type: MonitoringDeviceType.CEMS,
            status: DeviceStatus.Offline,
            notes: "Updated in test"
        );
}
