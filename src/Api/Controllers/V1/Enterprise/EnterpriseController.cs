using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Models;
using Application.Features.Enterprises.Commands;
using Application.Features.Enterprises.Commands.Approve;
using Application.Features.Enterprises.Commands.Reject;
using Asp.Versioning;
using Domain.Entities.Enterprises;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Enterprise;

[ApiVersion("1")]
[Authorize(Roles = "superAdmin")]
[ApiController]
[Route("api/v{version:apiVersion}/enterprises")]
public class EnterpriseController(
    IEnterpriseQueries enterpriseQueries,
    ISender sender) : BaseController
{
    [HttpGet]
    [ProducesOkApiResponseType<PageResult<EnterpriseDto>>]
    public async Task<IActionResult> GetEnterprises(
        [FromQuery] EnterpriseQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await enterpriseQueries.GetPagedAsync(
            query.Page,
            query.PageSize,
            cancellationToken);

        return Ok(new PageResult<EnterpriseDto>
        {
            Items = result.Items.Select(EnterpriseDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesOkApiResponseType<EnterpriseDto>]
    public async Task<IActionResult> GetEnterprise(
        [FromQuery] bool includeSites,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = includeSites
            ? await enterpriseQueries.GetByIdWithSitesAsync(id, cancellationToken)
            : await enterpriseQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            e => Ok(EnterpriseDto.FromDomainModel(e)),
            () => NotFound());
    }

    [HttpGet("{edrpou}")]
    [ProducesOkApiResponseType<EnterpriseDto>]
    public async Task<IActionResult> GetEnterpriseByEdrpou(
        [FromRoute] string edrpou,
        CancellationToken cancellationToken)
    {
        var entity = await enterpriseQueries.GetByEdrpouAsync(edrpou, cancellationToken);

        return entity.Match<ActionResult>(
            e => Ok(EnterpriseDto.FromDomainModel(e)),
            () => NotFound());
    }

    [HttpPost]
    [ProducesOkApiResponseType<EnterpriseDto>]
    public async Task<IActionResult> CreateEnterprise(
        [FromBody] CreateEnterpriseDto request, CancellationToken cancellationToken)
    {
        var input = new CreateEnterpriseCommand
        {
            Name = request.Name,
            Edrpou = request.Edrpou,
            RiskGroup = request.RiskGroup,
            Address = request.Address,
            SectorId = request.SectorId,
        };

        var newEntity = await sender.Send(input, cancellationToken);

        return newEntity.Match(
            e => Ok(EnterpriseDto.FromDomainModel(e)),
            e => e.ToObjectResult());
    }

    [HttpPut("{id:guid}")]
    [ProducesOkApiResponseType<EnterpriseDto>]
    public async Task<IActionResult> UpdateEnterprise(
        [FromBody] UpdateEnterpriseDto request, [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new UpdateEnterpriseCommand
        {
            Id = id,
            Name = request.Name,
            RiskGroup = request.RiskGroup,
            Address = request.Address,
            SectorId = request.SectorId,
        };
        var updatedEntity = await sender.Send(input, cancellationToken);

        return updatedEntity.Match(
            e => Ok(EnterpriseDto.FromDomainModel(e)),
            e => e.ToObjectResult());
    }

    [HttpDelete("{id:guid}")]
    [ProducesOkApiResponseType<EnterpriseDto>]
    public async Task<IActionResult> DeleteEnterprise(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new DeleteEnterpriseCommand
        {
            Id = id,
        };
        var deletedEntity = await sender.Send(input, cancellationToken);

        return deletedEntity.Match(
            e => Ok(EnterpriseDto.FromDomainModel(e)),
            e => e.ToObjectResult());
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesOkApiResponseType<EnterpriseDto>]
    public async Task<IActionResult> RestoreEnterprise(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RestoreEnterpriseCommand(id), cancellationToken);
        return result.Match(
            e => Ok(EnterpriseDto.FromDomainModel(e)),
            e => e.ToObjectResult());
    }

    // ─── SuperAdmin approval workflow ─────────────────────────────────────────────

    [HttpGet("pending")]
    [ProducesOkApiResponseType<PageResult<EnterpriseDto>>]
    public async Task<IActionResult> GetPendingEnterprises(
        [FromQuery] EnterpriseQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await enterpriseQueries.GetByStatusPagedAsync(
            EnterpriseStatus.Pending, query.Page, query.PageSize, cancellationToken);

        return Ok(new PageResult<EnterpriseDto>
        {
            Items = result.Items.Select(EnterpriseDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesOkApiResponseType<EnterpriseDto>]
    public async Task<IActionResult> ApproveEnterprise(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ApproveEnterpriseCommand(id), cancellationToken);
        return result.Match(
            e => Ok(EnterpriseDto.FromDomainModel(e)),
            e => e.ToObjectResult());
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesOkApiResponseType<EnterpriseDto>]
    public async Task<IActionResult> RejectEnterprise(
        [FromRoute] Guid id,
        [FromBody] RejectEnterpriseDto request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RejectEnterpriseCommand(id, request.Reason), cancellationToken);
        return result.Match(
            e => Ok(EnterpriseDto.FromDomainModel(e)),
            e => e.ToObjectResult());
    }
}