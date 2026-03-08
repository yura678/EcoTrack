using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Application.Common.Interfaces.Queries.Enterprises;
using Asp.Versioning;
using Infrastructure.Identity.PermissionManager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Enterprise;

[ApiVersion("1")]
[Authorize(ConstantPolicies.DynamicPermission)]
[Route("api/v{version:apiVersion}/iedCategories")]
[ApiController]
public class IedCategoryController(
    IIedCategoryQueries iedCategoryQueries) : BaseController
{
    [HttpGet("{id:guid}")]
    [ProducesOkApiResponseType<IedCategoryDto>]
    public async Task<IActionResult> GetIedCategoryById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await iedCategoryQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            c => Ok(IedCategoryDto.FromDomainModel(c)),
            () => NotFound()
        );
    }

    [HttpGet]
    [ProducesOkApiResponseType<IReadOnlyList<IedCategoryDto>>]
    public async Task<IActionResult> GetIedCategories(
        CancellationToken cancellationToken)
    {
        var entities = await iedCategoryQueries.GetAllAsync(cancellationToken);

        return Ok(entities.Select(IedCategoryDto.FromDomainModel).ToList());
    }
}