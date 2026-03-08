using Domain.Entities.Enterprises;

namespace Tests.Data.Enterprises;

public static class SectorsData
{
    public static Sector FirstTestSector() => Sector.New(Guid.NewGuid(),
        "First sector", Guid.NewGuid().ToString("N")[..8]);
}