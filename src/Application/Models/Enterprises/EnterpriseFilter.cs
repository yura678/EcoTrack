using Domain.Entities.Enterprises;

namespace Application.Models.Enterprises;

public record EnterpriseFilter(
    string? Name,
    string? Edrpou,
    RiskGroup? RiskGroup);