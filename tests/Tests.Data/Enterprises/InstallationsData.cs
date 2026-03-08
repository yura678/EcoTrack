using Domain.Entities.Enterprises;

namespace Tests.Data.Enterprises;

public static class InstallationData
{
    public static Installation FirstTestInstallation(Guid siteId,Guid iedCategoryId) =>
        Installation.New(
            Guid.NewGuid(), "First test installation", iedCategoryId, siteId,
            InstallationStatus.Operating);

    public static Installation SecondTestInstallation(Guid siteId,Guid iedCategoryId) =>
        Installation.New(
            Guid.NewGuid(), "Second test installation", iedCategoryId, siteId,
            InstallationStatus.Decommissioned);


    public static Installation ThirdTestInstallation(Guid siteId,Guid iedCategoryId) =>
        Installation.New(
            Guid.NewGuid(), "Third test installation", iedCategoryId, siteId,
            InstallationStatus.Decommissioned);
}