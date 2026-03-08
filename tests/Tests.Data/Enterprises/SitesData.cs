using Domain.Entities.Enterprises;

namespace Tests.Data.Enterprises;

public static class SitesData
{
    public static Site FirstTestSite(Guid enterpriseId) => Site.New(Guid.NewGuid(),
        "First test site", "Address", 1000, enterpriseId);

    public static Site SecondTestSite(Guid enterpriseId) => Site.New(Guid.NewGuid(),
        "Second test site", "Address2", 2000, enterpriseId);

    public static Site ThirdTestSite(Guid enterpriseId) => Site.New(Guid.NewGuid(),
        "Third test site", "Address3", 4000, enterpriseId);
}