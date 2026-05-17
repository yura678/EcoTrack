namespace Domain.Entities.Monitoring;

/// <summary>
/// Compatibility rules between LimitType and MeasureUnitDimension.
/// A Concentration limit must use a MassConcentration unit (e.g. mg/m³); a MassFlow limit must
/// use a MassFlow unit (e.g. kg/h). AnnualLoad accepts both — regulators express annual caps
/// either as a concentration averaged over the year ("&lt;200 mg/m³ annual avg") or as a total
/// mass flow over the year ("&lt;50 t/yr"). The t/yr unit is seeded as MassFlow with the
/// appropriate kg/h conversion factor.
/// </summary>
public static class LimitTypeDimensionRules
{
    public static IReadOnlyCollection<MeasureUnitDimension> AllowedDimensions(LimitType type) =>
        type switch
        {
            LimitType.Concentration => [MeasureUnitDimension.MassConcentration],
            LimitType.MassFlow => [MeasureUnitDimension.MassFlow],
            LimitType.AnnualLoad =>
                [MeasureUnitDimension.MassConcentration, MeasureUnitDimension.MassFlow],
            _ => []
        };

    public static bool IsCompatible(LimitType type, MeasureUnitDimension dimension) =>
        AllowedDimensions(type).Contains(dimension);
}
