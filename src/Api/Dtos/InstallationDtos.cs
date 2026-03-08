using Domain.Entities.Enterprises;

namespace Api.Dtos;

public record CreateInstallationDto(
    string Name,
    Guid IedCategoryId,
    Guid SiteId,
    InstallationStatus Status);


public record UpdateInstallationDto(
    string Name,
    Guid IedCategoryId);

public record UpdateInstallationStatusDto(
    InstallationStatus Status);

  
public record InstallationDto(
    Guid Id,
    string Name,
    Guid IedCategoryId,
    IedCategoryDto? Category,
    Guid SiteId,
    InstallationStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<EmissionSourceDto>? Emissions
)
{
    public static InstallationDto FromDomainModel(Installation installation)
    {
        return new InstallationDto(
            installation.Id,
            installation.Name,
            installation.IedCategoryId,
            installation.IedCategory is not null
            ? IedCategoryDto.FromDomainModel(installation.IedCategory) 
            : null,
            installation.SiteId,
            installation.Status,
            installation.CreatedAt,
            installation.UpdatedAt,
            installation.EmissionSources?.Select(EmissionSourceDto.FromDomainModel).ToList()
        );
    }
}

public record IedCategoryDto(Guid Id, string Code, string? Description)
{
    public static IedCategoryDto FromDomainModel(IedCategory iedCategory)
    {
        return new IedCategoryDto(
            iedCategory.Id,
            iedCategory.Code,
            iedCategory.Description
        );
    }
}