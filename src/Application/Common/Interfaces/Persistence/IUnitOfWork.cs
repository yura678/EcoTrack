using System.Data;
using Application.Common.Interfaces.Repositories;
using Application.Common.Interfaces.Repositories.Emissions;
using Application.Common.Interfaces.Repositories.Enterprises;
using Application.Common.Interfaces.Repositories.Monitoring;

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
    IMonitoringRequirementRepository MonitoringRequirementRepository { get; }
    IExceedanceEventRepository ExceedanceEventRepository { get; }
    IMonitoringPlanRepository MonitoringPlanRepository { get; }
    IPermitRepository PermitRepository { get; }
    IEmissionLimitRepository EmissionLimitRepository { get; }
    IInvitationRepository InvitationRepository { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}