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

    public static Pollutant WithO2Reference(decimal o2Ref)
        => Pollutant.New(
            Guid.NewGuid(),
            code: "NOX-O2",
            name: "Test NOX with O2 reference",
            category: PollutantCategory.Gas,
            media: PollutantMedia.Air,
            defaultDimension: MeasureUnitDimension.MassConcentration,
            defaultO2Reference: o2Ref);
}
