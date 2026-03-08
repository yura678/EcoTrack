using Domain.Entities.EmissionSources;

namespace Api.Dtos;

public record PollutantDto(
    Guid Id,
    string Code,
    string Name,
    DateTime CreatedAt)
{
    public static PollutantDto FromDomainModel(Pollutant pollutant)
    {
        return new PollutantDto(
            pollutant.Id,
            pollutant.Code,
            pollutant.Name,
            pollutant.CreatedAt
        );
    }
}