using Domain.Entities.Monitoring;

namespace Tests.Data.Monitoring;

public static class MeasureUnitsData
{
    public static MeasureUnit MgPerM3()
        => MeasureUnit.New(
            Guid.NewGuid(),
            symbol: "mg/m3",
            dimension: MeasureUnitDimension.MassConcentration,
            toBaseFactor: 1m);

    public static MeasureUnit GPerM3()
        => MeasureUnit.New(
            Guid.NewGuid(),
            symbol: "g/m3",
            dimension: MeasureUnitDimension.MassConcentration,
            toBaseFactor: 1000m);

    public static MeasureUnit UgPerM3()
        => MeasureUnit.New(
            Guid.NewGuid(),
            symbol: "µg/m3",
            dimension: MeasureUnitDimension.MassConcentration,
            toBaseFactor: 0.001m);
}