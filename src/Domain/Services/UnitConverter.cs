using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;

namespace Domain.Services;

/// <summary>
/// Pure unit conversion against a target pollutant's canonical unit. Used at ingest to normalize
/// every raw sample to one unit per pollutant, so downstream aggregation (CA, materializer,
/// long-window analytics) never has to merge mixed-unit values across time or devices.
/// </summary>
public static class UnitConverter
{
    // EU IED standard (273.15 K, 1.013 bar, dry gas). Used to translate volume ratios (ppm)
    // into mass concentration via mg/Nm³ = ppm × M / 22.414.
    private const decimal MolarVolumeAtStpLiters = 22.414m;

    /// <summary>
    /// Non-throwing conversion — preferred for hot paths (ingest per row, materializer per
    /// bucket). Misconfigured devices can flood batches with thousands of invalid rows; raising
    /// a structured exception per row would stack-trace the CPU into the ground. Failure data
    /// is returned through <paramref name="error"/> for the caller to aggregate.
    /// </summary>
    /// <returns>True on success — <paramref name="result"/> carries the canonical value and
    /// <paramref name="error"/> is null. False on failure — <paramref name="result"/> is 0
    /// (sentinel; do not consume) and <paramref name="error"/> describes why.</returns>
    public static bool TryToCanonical(
        decimal value,
        MeasureUnit fromUnit,
        MeasureUnit canonicalUnit,
        decimal? molarMass,
        out decimal result,
        out string? error)
    {
        if (fromUnit.Id == canonicalUnit.Id)
        {
            result = value;
            error = null;
            return true;
        }

        if (fromUnit.Dimension == canonicalUnit.Dimension)
        {
            result = value * fromUnit.ToBaseFactor / canonicalUnit.ToBaseFactor;
            error = null;
            return true;
        }

        if (IsPpm(fromUnit) && canonicalUnit.Dimension == MeasureUnitDimension.MassConcentration)
        {
            if (molarMass is null or <= 0m)
            {
                result = 0m;
                error = $"Cannot convert ppm to {canonicalUnit.Symbol}: molar mass is required for this pollutant.";
                return false;
            }

            // mg/Nm³ at EU STP, then linearly rescale into the canonical unit.
            var mgPerNm3 = value * molarMass.Value / MolarVolumeAtStpLiters;
            result = mgPerNm3 / canonicalUnit.ToBaseFactor;
            error = null;
            return true;
        }

        result = 0m;
        error = $"No conversion path from {fromUnit.Symbol} ({fromUnit.Dimension}) to {canonicalUnit.Symbol} ({canonicalUnit.Dimension}).";
        return false;
    }

    /// <summary>
    /// Throwing convenience wrapper around <see cref="TryToCanonical"/>. Use only where the
    /// failure is genuinely exceptional (one-off compliance probe, test code). Per-row loops
    /// must call TryToCanonical directly to avoid <see cref="Exception"/> allocation churn.
    /// </summary>
    /// <exception cref="UnitConversionException">
    /// Thrown when the two units cannot be reconciled (incompatible dimensions, ppm→mass without
    /// a molar mass on the pollutant, or any other unsupported pair).
    /// </exception>
    public static decimal ToCanonical(
        decimal value,
        MeasureUnit fromUnit,
        MeasureUnit canonicalUnit,
        decimal? molarMass)
    {
        if (TryToCanonical(value, fromUnit, canonicalUnit, molarMass, out var result, out var error))
        {
            return result;
        }
        throw new UnitConversionException(error!);
    }

    /// <summary>Convenience overload using a Pollutant for canonical unit + molar mass.</summary>
    public static decimal ToCanonical(
        decimal value,
        MeasureUnit fromUnit,
        Pollutant pollutant,
        MeasureUnit canonicalUnit)
    {
        if (pollutant.CanonicalUnitId != canonicalUnit.Id)
        {
            throw new ArgumentException(
                $"canonicalUnit.Id ({canonicalUnit.Id}) does not match pollutant.CanonicalUnitId ({pollutant.CanonicalUnitId}).",
                nameof(canonicalUnit));
        }
        return ToCanonical(value, fromUnit, canonicalUnit, pollutant.MolarMass);
    }

    private static bool IsPpm(MeasureUnit unit) =>
        unit.Dimension == MeasureUnitDimension.Dimensionless &&
        string.Equals(unit.Symbol, "ppm", StringComparison.OrdinalIgnoreCase);
}
