using Domain.Entities.Enterprises;

namespace Api.Dtos;

public record CreateSiteDto(
    string Name,
    string Address,
    int? SanitaryZoneRadius,
    Guid EnterpriseId);

public record UpdateSiteDto(
    string Name,
    string Address,
    int? SanitaryZoneRadius);

public record SiteDto(
    Guid Id,
    string Name,
    string Address,
    int? SanitaryZoneRadius,
    Guid EnterpriseId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<InstallationDto>? Installations)
{
    public static SiteDto FromDomainModel(Site site)
    {
        return new SiteDto(
            site.Id,
            site.Name,
            site.Address,
            site.SanitaryZoneRadius,
            site.EnterpriseId,
            site.CreatedAt,
            site.UpdatedAt,
            site.Installations?.Select(InstallationDto.FromDomainModel).ToList()
        );
    }
}