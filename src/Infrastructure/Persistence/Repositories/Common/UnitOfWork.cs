using System.Data;
using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Repositories;
using Application.Common.Interfaces.Repositories.Emissions;
using Application.Common.Interfaces.Repositories.Enterprises;
using Application.Common.Interfaces.Repositories.Monitoring;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence.Repositories.Common;

public class UnitOfWork(
    ApplicationDbContext db,
    IUserRefreshTokenRepository userRefreshTokenRepository,
    IEnterpriseRepository enterpriseRepository,
    IIedCategoryRepository iedCategoryRepository,
    IInstallationRepository installationRepository,
    ISectorRepository sectorRepository,
    ISiteRepository siteRepository,
    IEmissionSourceRepository emissionSourceRepository,
    IMeasureUnitRepository measureUnitRepository,
    IPollutantRepository pollutantRepository,
    IMeasurementRepository measurementRepository,
    IMonitoringDeviceRepository monitoringDeviceRepository,
    IMonitoringRequirementRepository monitoringRequirementRepository,
    IExceedanceEventRepository exceedanceEventRepository,
    IMonitoringPlanRepository monitoringPlanRepository,
    IPermitRepository permitRepository,
    IEmissionLimitRepository emissionLimitRepository,
    IInvitationRepository invitationRepository)
    : IUnitOfWork
{
    public IUserRefreshTokenRepository UserRefreshTokenRepository { get; } = userRefreshTokenRepository;
    public IEnterpriseRepository EnterpriseRepository { get; } = enterpriseRepository;
    public IIedCategoryRepository IedCategoryRepository { get; } = iedCategoryRepository;
    public IInstallationRepository InstallationRepository { get; } = installationRepository;
    public ISectorRepository SectorRepository { get; } = sectorRepository;
    public ISiteRepository SiteRepository { get; } = siteRepository;
    public IEmissionSourceRepository EmissionSourceRepository { get; } = emissionSourceRepository;
    public IMeasureUnitRepository MeasureUnitRepository { get; } = measureUnitRepository;
    public IPollutantRepository PollutantRepository { get; } = pollutantRepository;
    public IMeasurementRepository MeasurementRepository { get; } = measurementRepository;
    public IMonitoringDeviceRepository MonitoringDeviceRepository { get; } = monitoringDeviceRepository;
    public IMonitoringRequirementRepository MonitoringRequirementRepository { get; } = monitoringRequirementRepository;
    public IExceedanceEventRepository ExceedanceEventRepository { get; } = exceedanceEventRepository;
    public IMonitoringPlanRepository MonitoringPlanRepository { get; } = monitoringPlanRepository;
    public IPermitRepository PermitRepository { get; } = permitRepository;
    public IEmissionLimitRepository EmissionLimitRepository { get; } = emissionLimitRepository;
    public IInvitationRepository InvitationRepository { get; } = invitationRepository;


    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        return transaction.GetDbTransaction();
    }
}