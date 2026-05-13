using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;

namespace Tests.Data.EmissionSources;

public static class PollutantsData
{
    public static Pollutant FirstTestPollutant()
        => Pollutant.New(
            Guid.NewGuid(),
            code: "NOX",
            name: "Nitrogen oxides",
            category: PollutantCategory.Gas,
            media: PollutantMedia.Air,
            defaultDimension: MeasureUnitDimension.MassConcentration);


    public static Pollutant SecondTestPollutant()
        => Pollutant.New(
            Guid.NewGuid(),
            code: "CO2",
            name: "Сarbon dioxide",
            category: PollutantCategory.Gas,
            media: PollutantMedia.Air,
            defaultDimension: MeasureUnitDimension.MassConcentration);
}
