using Domain.Entities.EmissionSources;

namespace Tests.Data.EmissionSources;

public static class EmissionSourcesData
{
    private const double TestLatitude = 50.4501;
    private const double TestLongitude = 30.5234;

    public static EmissionSource FirstTestEmissionSource(Guid installationId)
        => AirEmissionSource.New(
            Guid.NewGuid(),
            installationId,
            "code",
            TestLatitude,
            TestLongitude,
            20,
            3,
            1000);


    public static EmissionSource SecondTestEmissionSource(Guid installationId)
        => WaterEmissionSource.New(
            Guid.NewGuid(),
            installationId,
            "code2",
            TestLatitude,
            TestLongitude,
            "River",
            1000);


    public static AirEmissionSource FirstTestAirEmissionSource(Guid installationId)
        => AirEmissionSource.New(
            Guid.NewGuid(),
            installationId,
            "AIR-001",
            TestLatitude,
            TestLongitude,
            30,
            1.2,
            2500);

    public static AirEmissionSource SecondTestAirEmissionSource(Guid installationId)
        => AirEmissionSource.New(
            Guid.NewGuid(),
            installationId,
            "AIR-002",
            TestLatitude,
            TestLongitude,
            40,
            1.5,
            5000);

    public static WaterEmissionSource FirstTestWaterEmissionSource(Guid installationId)
        => WaterEmissionSource.New(
            Guid.NewGuid(),
            installationId,
            "WAT-001",
            TestLatitude,
            TestLongitude,
            "River Dnipro",
            1000);

    public static WaterEmissionSource SecondTestWaterEmissionSource(Guid installationId)
        => WaterEmissionSource.New(
            Guid.NewGuid(),
            installationId,
            "WAT-002",
            TestLatitude,
            TestLongitude,
            "Test River 2",
            2000);
}
