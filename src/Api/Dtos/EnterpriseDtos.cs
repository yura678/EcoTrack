using Domain.Entities.Enterprises;

namespace Api.Dtos;

public record EnterpriseQueryDto(
    int Page = 1,
    int PageSize = 20);

public record CreateEnterpriseDto(
    string Name,
    string Edrpou,
    RiskGroup RiskGroup,
    string Address,
    Guid SectorId);

public record UpdateEnterpriseDto(
    string Name,
    RiskGroup RiskGroup,
    string Address,
    Guid SectorId);

public record EnterpriseDto(
    Guid Id,
    string Name,
    string Edrpou,
    RiskGroup RiskGroup,
    string Address,
    Guid SectorId,
    SectorDto? Sector,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<SiteDto>? Sites)
{
    public static EnterpriseDto FromDomainModel(Enterprise enterprise)
    {
        return new EnterpriseDto(
            Id: enterprise.Id,
            Name: enterprise.Name,
            Edrpou: enterprise.Edrpou,
            RiskGroup: enterprise.RiskGroup,
            Address: enterprise.Address,
            SectorId: enterprise.SectorId,
            Sector: enterprise.Sector is not null ? SectorDto.FromDomainModel(enterprise.Sector) : null,
            CreatedAt: enterprise.CreatedAt,
            UpdatedAt: enterprise.UpdatedAt,
            Sites: enterprise.Sites?.Select(SiteDto.FromDomainModel).ToList()
        );
    }
}

public record SectorDto(
    Guid Id,
    string Name,
    string Code)
{
    public static SectorDto FromDomainModel(Sector sector)
    {
        return new SectorDto(
            Id: sector.Id,
            Name: sector.Name,
            Code: sector.Code
        );
    }
}