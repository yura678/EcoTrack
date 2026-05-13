using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record PollutantDto(
    Guid Id,
    string Code,
    string Name,
    string? CasNumber,
    PollutantCategory Category,
    PollutantMedia Media,
    MeasureUnitDimension DefaultDimension,
    decimal? DefaultO2Reference,
    decimal? EprtrThresholdKgYear,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static PollutantDto FromDomainModel(Pollutant pollutant)
    {
        return new PollutantDto(
            pollutant.Id,
            pollutant.Code,
            pollutant.Name,
            pollutant.CasNumber,
            pollutant.Category,
            pollutant.Media,
            pollutant.DefaultDimension,
            pollutant.DefaultO2Reference,
            pollutant.EprtrThresholdKgYear,
            pollutant.CreatedAt,
            pollutant.UpdatedAt
        );
    }
}

public record CreatePollutantDto(
    string Code,
    string Name,
    string? CasNumber,
    PollutantCategory Category,
    PollutantMedia Media,
    MeasureUnitDimension DefaultDimension,
    decimal? DefaultO2Reference,
    decimal? EprtrThresholdKgYear);

public record UpdatePollutantDto(
    string Code,
    string Name,
    string? CasNumber,
    PollutantCategory Category,
    PollutantMedia Media,
    MeasureUnitDimension DefaultDimension,
    decimal? DefaultO2Reference,
    decimal? EprtrThresholdKgYear);
