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
    /// Converts <paramref name="value"/> measured in <paramref name="fromUnit"/> into the target
    /// <paramref name="canonicalUnit"/>. Supports two paths: linear rescale within the same
    /// dimension, and ppm → mass-concentration via ideal-gas with the pollutant's molar mass.
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
        if (fromUnit.Id == canonicalUnit.Id) return value;

        if (fromUnit.Dimension == canonicalUnit.Dimension)
        {
            return value * fromUnit.ToBaseFactor / canonicalUnit.ToBaseFactor;
        }

        if (IsPpm(fromUnit) && canonicalUnit.Dimension == MeasureUnitDimension.MassConcentration)
        {
            if (molarMass is null or <= 0m)
            {
                throw new UnitConversionException(
                    $"Cannot convert ppm to {canonicalUnit.Symbol}: molar mass is required for this pollutant.");
            }

            // mg/Nm³ at EU STP, then linearly rescale into the canonical unit.
            var mgPerNm3 = value * molarMass.Value / MolarVolumeAtStpLiters;
            return mgPerNm3 / canonicalUnit.ToBaseFactor;
        }

        throw new UnitConversionException(
            $"No conversion path from {fromUnit.Symbol} ({fromUnit.Dimension}) to {canonicalUnit.Symbol} ({canonicalUnit.Dimension}).");
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
