using System.Text.Json.Serialization;
using Application.Models.EmissionSources;
using Domain.Entities.EmissionSources;

namespace Api.Dtos;

public record EmissionSourceQueryDto(
    EmissionSourceType? Type,
    int Page = 1,
    int PageSize = 20);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "medium")]
[JsonDerivedType(typeof(AirEmissionSourceDto), "Air")]
[JsonDerivedType(typeof(WaterEmissionSourceDto), "Water")]
public abstract record EmissionSourceDto(
    Guid Id,
    Guid InstallationId,
    string Code,
    double Latitude,
    double Longitude,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt)
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
    double Latitude,
    double Longitude,
    double Height,
    double Diameter,
    double DesignFlowRate);

public record UpdateAirEmissionSourceDto(
    string Code,
    double Latitude,
    double Longitude,
    double Height,
    double Diameter,
    double DesignFlowRate);

public record AirEmissionSourceDto(
    Guid Id,
    Guid InstallationId,
    string Code,
    double Latitude,
    double Longitude,
    double Height,
    double Diameter,
    double DesignFlowRate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt) : EmissionSourceDto(Id, InstallationId, Code, Latitude, Longitude, CreatedAt, UpdatedAt, DeletedAt)
{
    public static AirEmissionSourceDto FromDomainModel(AirEmissionSource airEmissionSource)
    {
        return new AirEmissionSourceDto(
            airEmissionSource.Id,
            airEmissionSource.InstallationId,
            airEmissionSource.Code,
            airEmissionSource.Location.Y,
            airEmissionSource.Location.X,
            airEmissionSource.Height,
            airEmissionSource.Diameter,
            airEmissionSource.DesignFlowRate,
            airEmissionSource.CreatedAt,
            airEmissionSource.UpdatedAt,
            airEmissionSource.DeletedAt
        );
    }
}

public record CreateWaterEmissionSourceDto(
    string Code,
    double Latitude,
    double Longitude,
    string Receiver,
    double DesignFlowRate);

public record UpdateWaterEmissionSourceDto(
    string Code,
    double Latitude,
    double Longitude,
    string Receiver,
    double DesignFlowRate);

public record WaterEmissionSourceDto(
    Guid Id,
    Guid InstallationId,
    string Code,
    double Latitude,
    double Longitude,
    string Receiver,
    double DesignFlowRate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt) : EmissionSourceDto(Id, InstallationId, Code, Latitude, Longitude, CreatedAt, UpdatedAt, DeletedAt)
{
    public static WaterEmissionSourceDto FromDomainModel(WaterEmissionSource waterEmissionSource)
    {
        return new WaterEmissionSourceDto(
            waterEmissionSource.Id,
            waterEmissionSource.InstallationId,
            waterEmissionSource.Code,
            waterEmissionSource.Location.Y,
            waterEmissionSource.Location.X,
            waterEmissionSource.Receiver,
            waterEmissionSource.DesignFlowRate,
            waterEmissionSource.CreatedAt,
            waterEmissionSource.UpdatedAt,
            waterEmissionSource.DeletedAt
        );
    }
}
