using System.Net;
using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Enterprises;
using Application.Features.Sites.Commands;
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
public class SiteController(
    ISiteQueries siteQueries,
    ISender sender,
    ICurrentUserService currentUserService) : BaseController
{
    [HttpGet("enterprises/{enterpriseId:guid}/sites")]
    [ProducesOkApiResponseType<IReadOnlyList<SiteDto>>]
    public async Task<IActionResult> GetEnterpriseSites(
        [FromRoute] Guid enterpriseId,
        CancellationToken cancellationToken)
    {
        var entities =
            await siteQueries.GetByEnterpriseIdAsync(enterpriseId, cancellationToken);

        return Ok(entities.Select(SiteDto.FromDomainModel).ToList());
    }
    

    [HttpGet("sites/{id:guid}")]
    [ProducesOkApiResponseType<SiteDto>]
    public async Task<IActionResult> GetSite(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await siteQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            s => Ok(SiteDto.FromDomainModel(s)),
            () => NotFound());
    }

    [HttpPost("sites")]
    [ProducesOkApiResponseType<SiteDto>]
    public async Task<IActionResult> CreateSite(
        [FromBody] CreateSiteDto request,
        CancellationToken cancellationToken)
    {
        var input = new CreateSiteCommand
        {
            Name = request.Name,
            Address = request.Address,
            SanitaryZoneRadius = request.SanitaryZoneRadius,
            EnterpriseId = request.EnterpriseId,
        };

        var newEntity = await sender.Send(input, cancellationToken);

        return newEntity.Match(
            s => Ok(SiteDto.FromDomainModel(s)),
            e => e.ToObjectResult());
    }

    [HttpPut("sites/{id:guid}")]
    [ProducesOkApiResponseType<SiteDto>]
    public async Task<IActionResult> UpdateSite(
        [FromBody] UpdateSiteDto request, [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new UpdateSiteCommand
        {
            Id = id,
            Name = request.Name,
            Address = request.Address,
            SanitaryZoneRadius = request.SanitaryZoneRadius,
        };
        var updatedEntity = await sender.Send(input, cancellationToken);

        return updatedEntity.Match(
            s => Ok(SiteDto.FromDomainModel(s)),
            e => e.ToObjectResult());
    }

    [HttpDelete("sites/{id:guid}")]
    [ProducesOkApiResponseType<SiteDto>]
    public async Task<IActionResult> DeleteSite(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new DeleteSiteCommand
        {
            Id = id,
        };
        var deletedEntity = await sender.Send(input, cancellationToken);

        return deletedEntity.Match(
            s => Ok(SiteDto.FromDomainModel(s)),
            e => e.ToObjectResult());
    }
}