using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record PermitQueryDto(
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20);

public record CreatePermitDto(
    string Number,
    PermitType PermitType,
    string Authority,
    string? Notes,
    DateTime IssuedAt,
    DateTime ValidUntil,
    IReadOnlyList<CreateEmissionLimitDto>? EmissionLimits);

public record UpdatePermitDto(
    string Number,
    PermitType PermitType,
    string Authority,
    string? Notes,
    DateTime IssuedAt,
    DateTime ValidUntil,
    IReadOnlyList<UpdateEmissionLimitDto>? EmissionLimits);

public record CreateEmissionLimitDto(
    decimal Value,
    AveragingWindow Period,
    Guid PollutantId,
    Guid EmissionSourceId,
    Guid UnitId,
    DateTime? ValidFrom,
    DateTime? ValidTo);

public record UpdateEmissionLimitDto(
    Guid? Id,
    decimal Value,
    AveragingWindow Period,
    Guid PollutantId,
    Guid EmissionSourceId,
    Guid UnitId,
    DateTime? ValidFrom,
    DateTime? ValidTo);

public record PermitDto(
    Guid Id,
    PermitStatus Status,
    Guid InstallationId,
    InstallationDto? InstallationDto,
    string Number,
    PermitType PermitType,
    string Authority,
    string? Notes,
    DateTime IssuedAt,
    DateTime ValidUntil,
    IReadOnlyList<EmissionLimitDto>? EmissionLimits)
{
    public static PermitDto FromDomainModel(Permit permit)
    {
        return new PermitDto(
            permit.Id,
            permit.PermitStatus,
            permit.InstallationId,
            permit.Installation != null
                ? InstallationDto.FromDomainModel(permit.Installation)
                : null,
            permit.Number,
            permit.PermitType,
            permit.Authority,
            permit.Notes,
            permit.IssuedAt,
            permit.ValidUntil,
            permit.EmissionLimits?.Select(EmissionLimitDto.FromDomainModel).ToList()
        );
    }
}

public record EmissionLimitDto(
    Guid Id,
    decimal Value,
    AveragingWindow Period,
    Guid PermitId,
    Guid UnitId, 
    MeasureUnitDto? Unit,
    Guid PollutantId,
    PollutantDto? Pollutant,
    Guid EmissionSourceId,
    EmissionSourceDto? EmissionSource,
    DateTime? ValidFrom,
    DateTime? ValidTo)
{
    public static EmissionLimitDto FromDomainModel(EmissionLimit emissionLimit)
    {
        return new EmissionLimitDto(
            emissionLimit.Id,
            emissionLimit.Value,
            emissionLimit.Period,
            emissionLimit.PermitId,
            emissionLimit.UnitId,
            emissionLimit.Unit is not null
                ? MeasureUnitDto.FromDomainModel(emissionLimit.Unit)
                : null,
            emissionLimit.PollutantId,
            emissionLimit.Pollutant is not null
                ? PollutantDto.FromDomainModel(emissionLimit.Pollutant)
                : null,
            emissionLimit.EmissionSourceId,
            emissionLimit.EmissionSource is not null
                ? EmissionSourceDto.FromDomainModel(emissionLimit.EmissionSource)
                : null,
          
            emissionLimit.ValidFrom,
            emissionLimit.ValidTo
        );
    }
}