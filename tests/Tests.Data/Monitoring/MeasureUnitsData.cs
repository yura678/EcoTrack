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

    public static MeasureUnit KgPerHour()
        => MeasureUnit.New(
            Guid.NewGuid(),
            symbol: "kg/hr-test",
            dimension: MeasureUnitDimension.MassFlow,
            toBaseFactor: 1m);

    public static MeasureUnit Celsius()
        => MeasureUnit.New(
            Guid.NewGuid(),
            symbol: "C-test",
            dimension: MeasureUnitDimension.Temperature,
            toBaseFactor: 1m);

    public static MeasureUnit Percent()
        => MeasureUnit.New(
            Guid.NewGuid(),
            symbol: "%-test",
            dimension: MeasureUnitDimension.Dimensionless,
            toBaseFactor: 1m);
}