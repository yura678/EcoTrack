using Domain.Entities.EmissionSources;

namespace Tests.Data.EmissionSources;

public static class PollutantsData
{
    public static Pollutant FirstTestPollutant()
        => Pollutant.New(
            Guid.NewGuid(),
            code: "NOX",
            name: "Nitrogen oxides");
    
    
    public static Pollutant SecondTestPollutant()
        => Pollutant.New(
            Guid.NewGuid(),
            code: "CO2",
            name: "Сarbon dioxide");
}