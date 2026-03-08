using System.Text.Json.Serialization;
using Application.Models.EmissionSources;
using Domain.Entities.EmissionSources;

namespace Api.Dtos;

public record EmissionSourceQueryDto(
    EmissionSourceType? Type,
    int Page = 1,
    int PageSize = 20);

[JsonDerivedType(typeof(AirEmissionSourceDto), "air")]
[JsonDerivedType(typeof(WaterEmissionSourceDto), "water")]
public abstract record EmissionSourceDto(
    Guid Id,
    Guid InstallationId,
    string Code,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static EmissionSourceDto FromDomainModel(EmissionSource emissionSource)
    {
        return emissionSource switch
        {
            AirEmissionSource air => AirEmissionSourceDto.FromDomainModel(air),
            WaterEmissionSource water => WaterEmissionSourceDto.FromDomainModel(water),

            _ => throw new InvalidOperationException(
                $"Unsupported emission source type: {emissionSource.GetType().Name}")
        };
    }
}

public record CreateAirEmissionSourceDto(
    string Code,
    double Height,
    double Diameter,
    double DesignFlowRate);

public record UpdateAirEmissionSourceDto(
    double Height,
    double Diameter,
    double DesignFlowRate);

public record AirEmissionSourceDto(
    Guid Id,
    Guid InstallationId,
    string Code,
    double Height,
    double Diameter,
    double DesignFlowRate,
    DateTime CreatedAt,
    DateTime? UpdatedAt) : EmissionSourceDto(Id, InstallationId, Code, CreatedAt, UpdatedAt)
{
    public static AirEmissionSourceDto FromDomainModel(AirEmissionSource airEmissionSource)
    {
        return new AirEmissionSourceDto(
            airEmissionSource.Id,
            airEmissionSource.InstallationId,
            airEmissionSource.Code,
            airEmissionSource.Height,
            airEmissionSource.Diameter,
            airEmissionSource.DesignFlowRate,
            airEmissionSource.CreatedAt,
            airEmissionSource.UpdatedAt
        );
    }
}

public record CreateWaterEmissionSourceDto(
    string Code,
    string Receiver,
    double DesignFlowRate);

public record UpdateWaterEmissionSourceDto(
    string Receiver,
    double DesignFlowRate);

public record WaterEmissionSourceDto(
    Guid Id,
    Guid InstallationId,
    string Code,
    string Receiver,
    double DesignFlowRate,
    DateTime CreatedAt,
    DateTime? UpdatedAt) : EmissionSourceDto(Id, InstallationId, Code, CreatedAt, UpdatedAt)
{
    public static WaterEmissionSourceDto FromDomainModel(WaterEmissionSource waterEmissionSource)
    {
        return new WaterEmissionSourceDto(
            waterEmissionSource.Id,
            waterEmissionSource.InstallationId,
            waterEmissionSource.Code,
            waterEmissionSource.Receiver,
            waterEmissionSource.DesignFlowRate,
            waterEmissionSource.CreatedAt,
            waterEmissionSource.UpdatedAt
        );
    }
}