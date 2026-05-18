using System.Data;
using Application.Common.Interfaces.Repositories;
using Application.Common.Interfaces.Repositories.Emissions;
using Application.Common.Interfaces.Repositories.Enterprises;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Interfaces.Repositories.Notifications;

namespace Application.Common.Interfaces.Persistence;

public interface IUnitOfWork
{
    IUserRefreshTokenRepository UserRefreshTokenRepository { get; }
    IEnterpriseRepository EnterpriseRepository { get; }
    IIedCategoryRepository IedCategoryRepository { get; }
    IInstallationRepository InstallationRepository { get; }
    ISectorRepository SectorRepository { get; }
    ISiteRepository SiteRepository { get; }
    IEmissionSourceRepository EmissionSourceRepository { get; }
    IMeasureUnitRepository MeasureUnitRepository { get; }
    IPollutantRepository PollutantRepository { get; }
    IMeasurementRepository MeasurementRepository { get; }
    IMonitoringDeviceRepository MonitoringDeviceRepository { get; }
    IComplianceEventRepository ComplianceEventRepository { get; }
    IDevicePollutantCapabilityRepository DevicePollutantCapabilityRepository { get; }
    ICalibrationRecordRepository CalibrationRecordRepository { get; }
    IPermitRepository PermitRepository { get; }
    IEmissionLimitRepository EmissionLimitRepository { get; }
    IInvitationRepository InvitationRepository { get; }
    IUserEnterpriseMembershipRepository UserEnterpriseMembershipRepository { get; }
    INotificationSubscriptionRepository NotificationSubscriptionRepository { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}