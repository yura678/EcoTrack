using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Models;
using Application.Features.Permits.Commands;
using Asp.Versioning;
using Infrastructure.Identity.PermissionManager;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Enterprise;

[ApiVersion("1")]
[Authorize(ConstantPolicies.DynamicPermission)]
[Route("api/v{version:apiVersion}")]
[ApiController]
public class PermitController(
    IPermitQueries permitQueries,
    ISender sender) : BaseController
{
    [HttpGet("installations/{installationId:guid}/permits")]
    [ProducesOkApiResponseType<PageResult<PermitDto>>]
    public async Task<IActionResult> GetPermits(
        [FromRoute] Guid installationId,
        [FromQuery] PermitQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await permitQueries.GetPagedAsync(installationId,
            query.From, query.To, query.Page, query.PageSize,
            cancellationToken);

        return Ok(new PageResult<PermitDto>
        {
            Items = result.Items.Select(PermitDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }


    [HttpGet("permits/{id:guid}")]
    [ProducesOkApiResponseType<PermitDto>]
    public async Task<IActionResult> GetPermit(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await permitQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            p => Ok(PermitDto.FromDomainModel(p)),
            () => NotFound());
    }

    [HttpPost("installations/{installationId:guid}/permits")]
    [ProducesOkApiResponseType<PermitDto>]
    public async Task<ActionResult<PermitDto>> CreatePermit(
        [FromRoute] Guid installationId,
        [FromBody] CreatePermitDto request,
        CancellationToken cancellationToken)
    {
        var input = new CreatePermitCommand
        {
            InstallationId = installationId,
            Number = request.Number,
            PermitType = request.PermitType,
            IssuedAt = request.IssuedAt,
            ValidUntil = request.ValidUntil,
            Authority = request.Authority,
            Notes = request.Notes,
            EmissionLimits = request.EmissionLimits!
                .Select(x => new EmissionLimitCommandDto(
                    x.Value,
                    x.Period,
                    x.UnitId,
                    x.PollutantId,
                    x.EmissionSourceId,
                    x.ValidFrom,
                    x.ValidTo
                ))
                .ToList()
        };

        var newEntity = await sender.Send(input, cancellationToken);

        return newEntity.Match(
            p => Ok(PermitDto.FromDomainModel(p)),
            e => e.ToObjectResult());
    }

    [HttpPut("permits/{id:guid}")]
    [ProducesOkApiResponseType<PermitDto>]
    public async Task<IActionResult> UpdatePermit(
        [FromRoute] Guid id,
        [FromBody] UpdatePermitDto request,
        CancellationToken cancellationToken)
    {
        var input = new UpdatePermitCommand
        {
            Id = id,
            Number = request.Number,
            PermitType = request.PermitType,
            IssuedAt = request.IssuedAt,
            ValidUntil = request.ValidUntil,
            Authority = request.Authority,
            Notes = request.Notes,
            EmissionLimits = request.EmissionLimits!
                .Select(x => new UpdateEmissionLimitCommandDto(
                    x.Id,
                    x.Value,
                    x.Period,
                    x.UnitId,
                    x.PollutantId,
                    x.EmissionSourceId,
                    x.ValidFrom,
                    x.ValidTo
                ))
                .ToList()
        };
        var updatedEntity = await sender.Send(input, cancellationToken);

        return updatedEntity.Match(
            p => Ok(PermitDto.FromDomainModel(p)),
            e => e.ToObjectResult());
    }

    [HttpPatch("permits/{id:guid}/archive")]
    [ProducesOkApiResponseType<PermitDto>]
    public async Task<IActionResult> ArchivePermit(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new ArchivePermitCommand
        {
            Id = id
        };

        var archivedEntity = await sender.Send(input, cancellationToken);

        return archivedEntity.Match(
            m => Ok(PermitDto.FromDomainModel(m)),
            e => e.ToObjectResult());
    }

    [HttpPatch("permits/{id:guid}/activate")]
    [ProducesOkApiResponseType<PermitDto>]
    public async Task<IActionResult> ActivatePermit(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new ActivatePermitCommand
        {
            Id = id
        };

        var activatedEntity = await sender.Send(input, cancellationToken);

        return activatedEntity.Match(
            p => Ok(PermitDto.FromDomainModel(p)),
            e => e.ToObjectResult());
    }

    [HttpDelete("permits/{id:guid}")]
    [ProducesOkApiResponseType<PermitDto>]
    public async Task<IActionResult> DeletePermit(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new DeletePermitCommand
        {
            Id = id
        };

        var deletedEntity = await sender.Send(input, cancellationToken);

        return deletedEntity.Match(
            p => Ok(PermitDto.FromDomainModel(p)),
            e => e.ToObjectResult());
    }

    [HttpPatch("permits/{id:guid}/revoke")]
    [ProducesOkApiResponseType<PermitDto>]
    public async Task<IActionResult> RevokePermit(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new RevokePermitCommand
        {
            Id = id
        };

        var revokedEntity = await sender.Send(input, cancellationToken);

        return revokedEntity.Match(
            p => Ok(PermitDto.FromDomainModel(p)),
            e => e.ToObjectResult());
    }
}