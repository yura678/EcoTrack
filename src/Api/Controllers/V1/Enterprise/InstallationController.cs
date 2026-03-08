using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Enterprises;
using Application.Features.Installations.Commands;
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
public class InstallationController(
    IInstallationQueries installationQueries,
    ISender sender) : BaseController
{
    [HttpGet("sites/{siteId:guid}/installations")]
    [ProducesOkApiResponseType<IReadOnlyList<InstallationDto>>]
    public async Task<IActionResult> GetInstallations(
        [FromRoute] Guid siteId,
        CancellationToken cancellationToken)
    {
        var entities =
            await installationQueries.GetBySiteIdAsync(siteId, cancellationToken);

        return Ok(entities.Select(InstallationDto.FromDomainModel).ToList());
    }


    [HttpGet("installations/{id:guid}")]
    [ProducesOkApiResponseType<InstallationDto>]
    public async Task<IActionResult> GetInstallation(
        [FromQuery] bool includeEmissionSources,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = includeEmissionSources
            ? await installationQueries.GetByIdWithEmissionsAsync(id, cancellationToken)
            : await installationQueries.GetByIdAsync(id, cancellationToken);


        return entity.Match<ActionResult>(
            i => Ok(InstallationDto.FromDomainModel(i)),
            () => NotFound());
    }

    [HttpPost("installations")]
    [ProducesOkApiResponseType<InstallationDto>]
    public async Task<IActionResult> CreateInstallation(
        [FromBody] CreateInstallationDto request,
        CancellationToken cancellationToken)
    {
        var input = new CreateInstallationCommand
        {
            Name = request.Name,
            IedCategoryId = request.IedCategoryId,
            SiteId = request.SiteId,
            Status = request.Status
        };

        var newEntity = await sender.Send(input, cancellationToken);

        return newEntity.Match(
            i => Ok(InstallationDto.FromDomainModel(i)),
            e => e.ToObjectResult());
    }

    [HttpPut("installations/{id:guid}")]
    [ProducesOkApiResponseType<InstallationDto>]
    public async Task<IActionResult> UpdateInstallation(
        [FromRoute] Guid id,
        [FromBody] UpdateInstallationDto request,
        CancellationToken cancellationToken)
    {
        var input = new UpdateInstallationCommand
        {
            Id = id,
            Name = request.Name,
            IedCategoryId = request.IedCategoryId
        };

        var updatedEntity = await sender.Send(input, cancellationToken);

        return updatedEntity.Match(
            i => Ok(InstallationDto.FromDomainModel(i)),
            e => e.ToObjectResult());
    }

    [HttpPatch("installations/{id:guid}")]
    [ProducesOkApiResponseType<InstallationDto>]
    public async Task<IActionResult> UpdateInstallationStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateInstallationStatusDto request,
        CancellationToken cancellationToken)
    {
        var input = new UpdateInstallationStatusCommand
        {
            Id = id,
            Status = request.Status,
        };

        var updatedEntity = await sender.Send(input, cancellationToken);

        return updatedEntity.Match(
            i => Ok(InstallationDto.FromDomainModel(i)),
            e => e.ToObjectResult());
    }

    [HttpDelete("installations/{id:guid}")]
    [ProducesOkApiResponseType<InstallationDto>]
    public async Task<IActionResult> DeleteInstallation(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new DeleteInstallationCommand
        {
            Id = id,
        };
        var deletedEntity = await sender.Send(input, cancellationToken);

        return deletedEntity.Match(
            i => Ok(InstallationDto.FromDomainModel(i)),
            e => e.ToObjectResult());
    }
}