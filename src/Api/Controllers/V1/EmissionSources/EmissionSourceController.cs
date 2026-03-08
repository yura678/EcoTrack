using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Emissions;
using Application.Common.Models;
using Application.Features.EmissionSources.Commands;
using Application.Features.EmissionSources.Commands.Air;
using Application.Features.EmissionSources.Commands.Water;
using Asp.Versioning;
using Infrastructure.Identity.PermissionManager;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.EmissionSources;

[ApiVersion("1")]
[Authorize(ConstantPolicies.DynamicPermission)]

[Route("api/v{version:apiVersion}")]
[ApiController]
public class EmissionSourceController(
    IEmissionSourceQueries emissionSourceQueries,
    ISender sender) : BaseController
{
    [HttpGet("installations/{installationId:guid}/emission-sources")]
    [ProducesOkApiResponseType<PageResult<EmissionSourceDto>>]
    public async Task<IActionResult> GetEmissionSources(
        [FromRoute] Guid installationId,
        [FromQuery] EmissionSourceQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await emissionSourceQueries.GetPagedAsync(
            installationId,
            query.Type,
            query.Page,
            query.PageSize,
            cancellationToken);

        return Ok(new PageResult<EmissionSourceDto>
        {
            Items = result.Items.Select(EmissionSourceDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("emission-sources/{id:guid}")]
    [ProducesOkApiResponseType<EmissionSourceDto>]
    public async Task<IActionResult> GetEmissionSourceById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await emissionSourceQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            e => Ok(EmissionSourceDto.FromDomainModel(e)),
            () => NotFound()
        );
    }

    [HttpPost("installations/{installationId:guid}/emission-sources/air")]
    [ProducesOkApiResponseType<EmissionSourceDto>]
    public async Task<IActionResult> CreateAirEmissionSource(
        [FromRoute] Guid installationId,
        [FromBody] CreateAirEmissionSourceDto request,
        CancellationToken cancellationToken)
    {
        var command = new CreateAirEmissionSourceCommand
        {
            Code = request.Code,
            Height = request.Height,
            Diameter = request.Diameter,
            DesignFlowRate = request.DesignFlowRate,
            InstallationId = installationId,
        };

        var entity = await sender.Send(command, cancellationToken);
        return entity.Match(
            e => Ok(EmissionSourceDto.FromDomainModel(e)),
            e => e.ToObjectResult()
        );
    }

    [HttpPost("installations/{installationId:guid}/emission-sources/water")]
    [ProducesOkApiResponseType<EmissionSourceDto>]
    public async Task<IActionResult> CreateWaterEmissionSource(
        [FromRoute] Guid installationId,
        [FromBody] CreateWaterEmissionSourceDto request,
        CancellationToken cancellationToken)
    {
        var command = new CreateWaterEmissionSourceCommand
        {
            Code = request.Code,
            Receiver = request.Receiver,
            DesignFlowRate = request.DesignFlowRate,
            InstallationId = installationId
        };

        var entity = await sender.Send(command, cancellationToken);
        return entity.Match(
            e => Ok(EmissionSourceDto.FromDomainModel(e)),
            e => e.ToObjectResult()
        );
    }

    [HttpPut("emission-sources/{id:guid}/air")]
    [ProducesOkApiResponseType<EmissionSourceDto>]
    public async Task<IActionResult> UpdateAirEmissionSource(
        [FromRoute] Guid id,
        [FromBody] UpdateAirEmissionSourceDto request,
        CancellationToken cancellationToken)
    {
        var input = new UpdateAirEmissionSourceCommand
        {
            Id = id,
            Height = request.Height,
            Diameter = request.Diameter,
            DesignFlowRate = request.DesignFlowRate
        };

        var updatedEntity = await sender.Send(input, cancellationToken);

        return updatedEntity.Match(
            e => Ok(EmissionSourceDto.FromDomainModel(e)),
            e => e.ToObjectResult());
    }


    [HttpPut("emission-sources/{id:guid}/water")]
    [ProducesOkApiResponseType<EmissionSourceDto>]
    public async Task<IActionResult> UpdateWaterEmissionSource(
        [FromRoute] Guid id,
        [FromBody] UpdateWaterEmissionSourceDto request,
        CancellationToken cancellationToken)
    {
        var input = new UpdateWaterEmissionSourceCommand
        {
            Id = id,
            Receiver = request.Receiver,
            DesignFlowRate = request.DesignFlowRate
        };

        var updatedEntity = await sender.Send(input, cancellationToken);

        return updatedEntity.Match(
            e => Ok(EmissionSourceDto.FromDomainModel(e)),
            e => e.ToObjectResult());
    }


    [HttpDelete("emission-sources/{id:guid}")]
    [ProducesOkApiResponseType<EmissionSourceDto>]
    public async Task<IActionResult> DeleteEmissionSource(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new DeleteEmissionSourceCommand
        {
            Id = id,
        };
        var deletedEntity = await sender.Send(input, cancellationToken);

        return deletedEntity.Match(
            e => Ok(EmissionSourceDto.FromDomainModel(e)),
            e => e.ToObjectResult());
    }
}