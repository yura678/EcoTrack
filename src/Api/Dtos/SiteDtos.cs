using Domain.Entities.Enterprises;

namespace Api.Dtos;

public record CreateSiteDto(
    string Name,
    string Address,
    int? SanitaryZoneRadius,
    Guid EnterpriseId,
    double? Latitude,
    double? Longitude,
    double? Elevation);

public record UpdateSiteDto(
    string Name,
    string Address,
    int? SanitaryZoneRadius,
    double? Latitude,
    double? Longitude,
    double? Elevation);

public record SiteDto(
    Guid Id,
    string Name,
    string Address,
    int? SanitaryZoneRadius,
    double? Latitude,
    double? Longitude,
    double? Elevation,
    Guid EnterpriseId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt,
    IReadOnlyCollection<InstallationDto>? Installations)
{
    public static SiteDto FromDomainModel(Site site)
    {
        return new SiteDto(
            site.Id,
            site.Name,
            site.Address,
            site.SanitaryZoneRadius,
            site.Location?.Y,
            site.Location?.X,
            site.Elevation,
            site.EnterpriseId,
            site.CreatedAt,
            site.UpdatedAt,
            site.DeletedAt,
            site.Installations?.Select(InstallationDto.FromDomainModel).ToList()
        );
    }
}
