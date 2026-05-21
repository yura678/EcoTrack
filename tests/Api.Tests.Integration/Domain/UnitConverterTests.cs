using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;
using Domain.Services;
using FluentAssertions;

namespace Api.Tests.Integration.Domain;

/// <summary>
/// Pure-logic tests — no DB, no DI. Locks in the two conversion paths the ingest pipeline
/// relies on (linear rescale within a dimension, ppm→mass via molar mass) and the failure
/// modes that should turn into 4xx responses upstream.
/// </summary>
public class UnitConverterTests
{
    private static MeasureUnit MgPerM3()
        => MeasureUnit.New(Guid.NewGuid(), "mg/m³", MeasureUnitDimension.MassConcentration, 1m);

    private static MeasureUnit UgPerM3()
        => MeasureUnit.New(Guid.NewGuid(), "µg/m³", MeasureUnitDimension.MassConcentration, 0.001m);

    private static MeasureUnit GPerM3()
        => MeasureUnit.New(Guid.NewGuid(), "g/m³", MeasureUnitDimension.MassConcentration, 1000m);

    private static MeasureUnit Ppm()
        => MeasureUnit.New(Guid.NewGuid(), "ppm", MeasureUnitDimension.Dimensionless, 1m);

    private static MeasureUnit CubicMetersPerHour()
        => MeasureUnit.New(Guid.NewGuid(), "m³/h", MeasureUnitDimension.VolumetricFlow, 1m);

    [Fact]
    public void IdentityShouldReturnInputUnchanged()
    {
        var mg = MgPerM3();
        var result = UnitConverter.ToCanonical(123.45m, mg, mg, molarMass: null);
        result.Should().Be(123.45m);
    }

    [Fact]
    public void LinearMgToUgShouldScaleByThousand()
    {
        var mg = MgPerM3();
        var ug = UgPerM3();
        var result = UnitConverter.ToCanonical(1.5m, mg, ug, molarMass: null);
        result.Should().Be(1500m);
    }

    [Fact]
    public void LinearUgToMgShouldScaleDownByThousand()
    {
        var mg = MgPerM3();
        var ug = UgPerM3();
        var result = UnitConverter.ToCanonical(2500m, ug, mg, molarMass: null);
        result.Should().Be(2.5m);
    }

    [Fact]
    public void LinearGToMgShouldScaleByThousand()
    {
        var g = GPerM3();
        var mg = MgPerM3();
        var result = UnitConverter.ToCanonical(0.5m, g, mg, molarMass: null);
        result.Should().Be(500m);
    }

    [Fact]
    public void PpmToMgPerM3ForCoShouldMatchEuStpFormula()
    {
        // CO: M = 28.01 g/mol. 100 ppm × 28.01 / 22.414 ≈ 124.9665 mg/m³
        var result = UnitConverter.ToCanonical(100m, Ppm(), MgPerM3(), molarMass: 28.01m);
        result.Should().BeApproximately(124.9665m, 0.001m);
    }

    [Fact]
    public void PpmToMgPerM3ForNo2ShouldMatchEuStpFormula()
    {
        // NO₂: M = 46.0055 g/mol. 100 ppm × 46.0055 / 22.414 ≈ 205.2534 mg/m³
        var result = UnitConverter.ToCanonical(100m, Ppm(), MgPerM3(), molarMass: 46.0055m);
        result.Should().BeApproximately(205.2534m, 0.001m);
    }

    [Fact]
    public void PpmToUgPerM3ShouldScaleByCanonicalUnitFactor()
    {
        // 100 ppm CO → 124.9665 mg/m³ → 124966.5 µg/m³
        var result = UnitConverter.ToCanonical(100m, Ppm(), UgPerM3(), molarMass: 28.01m);
        result.Should().BeApproximately(124966.5m, 1m);
    }

    [Fact]
    public void PpmWithoutMolarMassShouldThrow()
    {
        var act = () => UnitConverter.ToCanonical(100m, Ppm(), MgPerM3(), molarMass: null);
        act.Should().Throw<UnitConversionException>().WithMessage("*molar mass*");
    }

    [Fact]
    public void PpmWithZeroMolarMassShouldThrow()
    {
        var act = () => UnitConverter.ToCanonical(100m, Ppm(), MgPerM3(), molarMass: 0m);
        act.Should().Throw<UnitConversionException>();
    }

    [Fact]
    public void IncompatibleDimensionsShouldThrow()
    {
        var act = () => UnitConverter.ToCanonical(
            1m, CubicMetersPerHour(), MgPerM3(), molarMass: 28m);
        act.Should().Throw<UnitConversionException>().WithMessage("*No conversion path*");
    }

    [Fact]
    public void PollutantOverloadShouldUsePollutantMolarMass()
    {
        var mg = MgPerM3();
        var pollutant = Pollutant.New(
            Guid.NewGuid(), "CO", "Carbon monoxide",
            PollutantCategory.Gas, PollutantMedia.Air,
            MeasureUnitDimension.MassConcentration,
            canonicalUnitId: mg.Id, molarMass: 28.01m);

        var result = UnitConverter.ToCanonical(100m, Ppm(), pollutant, mg);
        result.Should().BeApproximately(124.9665m, 0.001m);
    }

    [Fact]
    public void PollutantOverloadShouldRejectMismatchedCanonicalUnit()
    {
        var mg = MgPerM3();
        var ug = UgPerM3();
        var pollutant = Pollutant.New(
            Guid.NewGuid(), "CO", "Carbon monoxide",
            PollutantCategory.Gas, PollutantMedia.Air,
            MeasureUnitDimension.MassConcentration,
            canonicalUnitId: mg.Id, molarMass: 28.01m);

        var act = () => UnitConverter.ToCanonical(100m, Ppm(), pollutant, ug);
        act.Should().Throw<ArgumentException>().WithMessage("*canonicalUnit.Id*");
    }
}
