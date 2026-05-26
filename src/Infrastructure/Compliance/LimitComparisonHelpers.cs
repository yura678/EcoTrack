using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.Monitoring;

namespace Infrastructure.Compliance;

/// <summary>
/// Shared physics for cross-dimension limit comparisons. Lives outside both the detector
/// (which raises events) and the probe (which confirms still-violating state) so a single
/// derivation answers the same way regardless of caller. The repository's aggregate
/// computations use the same helper to keep installation-level sums consistent with what
/// the per-source detector would emit.
/// </summary>
internal static class LimitComparisonHelpers
{
    /// <summary>
    /// MassConcentration × VolumetricFlow → MassFlow (kg/h, the MassFlow base unit). Returns
    /// null whenever the inputs do not satisfy the derivation preconditions:
    /// limit must target MassFlow, measurement must be MassConcentration, a flow row must
    /// exist for the source, and the flow unit must itself be VolumetricFlow.
    /// </summary>
    internal static decimal? TryDeriveMassFlowKgPerH(
        decimal measurementValue,
        UnitInfo measurementUnit,
        MeasureUnitDimension limitDimension,
        Guid sourceId,
        IReadOnlyDictionary<Guid, FlowReading> flowByKey,
        IReadOnlyDictionary<Guid, UnitInfo> units)
    {
        if (limitDimension != MeasureUnitDimension.MassFlow) return null;
        if (measurementUnit.Dimension != MeasureUnitDimension.MassConcentration) return null;
        if (!flowByKey.TryGetValue(sourceId, out var flow)) return null;
        if (!units.TryGetValue(flow.UnitId, out var flowUnit)) return null;
        if (flowUnit.Dimension != MeasureUnitDimension.VolumetricFlow) return null;

        // (mg/m³ base) × (m³/h base) = mg/h → /1e6 = kg/h
        var concBase = measurementValue * measurementUnit.ToBaseFactor;
        var flowBase = flow.Value * flowUnit.ToBaseFactor;
        return Math.Round((concBase * flowBase) / 1_000_000m, 6);
    }

    /// <summary>
    /// Promotes the lightweight <see cref="UnitInfo"/> read model into full <see cref="MeasureUnit"/>
    /// entities — the form <see cref="UnitConverter.TryToCanonical"/> requires. Shared so the
    /// detector and the probe build identical lookups from the same input dictionary.
    /// </summary>
    internal static Dictionary<Guid, MeasureUnit> BuildUnitEntities(
        IReadOnlyDictionary<Guid, UnitInfo> units) =>
        units.ToDictionary(
            kvp => kvp.Key,
            kvp => MeasureUnit.New(kvp.Key, kvp.Value.Symbol, kvp.Value.Dimension, kvp.Value.ToBaseFactor));
}
