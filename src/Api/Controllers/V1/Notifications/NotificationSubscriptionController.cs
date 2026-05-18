using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Notifications;
using Application.Features.NotificationSubscriptions.Commands;
using Asp.Versioning;
using Infrastructure.Identity.PermissionManager;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Notifications;

[ApiVersion("1")]
[Authorize(ConstantPolicies.DynamicPermission)]
[Route("api/v{version:apiVersion}/notification-subscriptions")]
[ApiController]
public class NotificationSubscriptionController(
    INotificationSubscriptionQueries queries,
    ISender sender) : BaseController
{
    [HttpGet]
    [ProducesOkApiResponseType<IReadOnlyList<NotificationSubscriptionDto>>]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var entities = await queries.GetByUserAsync(UserId, cancellationToken);
        return Ok(entities.Select(NotificationSubscriptionDto.FromDomainModel).ToList());
    }

    [HttpGet("{id:guid}")]
    [ProducesOkApiResponseType<NotificationSubscriptionDto>]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var entity = await queries.GetByIdAsync(id, cancellationToken);
        return entity.Match<ActionResult>(
            s => s.UserId == UserId
                ? Ok(NotificationSubscriptionDto.FromDomainModel(s))
                : Forbid(),
            () => NotFound());
    }

    [HttpPost]
    [ProducesOkApiResponseType<NotificationSubscriptionDto>]
    public async Task<IActionResult> Create(
        [FromBody] CreateNotificationSubscriptionDto body,
        CancellationToken cancellationToken)
    {
        var command = new CreateNotificationSubscriptionCommand
        {
            Channel = body.Channel,
            Email = body.Email,
            WebhookUrl = body.WebhookUrl,
            WebhookSecret = body.WebhookSecret,
            EventTypes = body.EventTypes,
            EmissionSourceIds = body.EmissionSourceIds
        };
        var result = await sender.Send(command, cancellationToken);
        return result.Match(
            s => Ok(NotificationSubscriptionDto.FromDomainModel(s)),
            err => err.ToObjectResult());
    }

    [HttpPut("{id:guid}")]
    [ProducesOkApiResponseType<NotificationSubscriptionDto>]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateNotificationSubscriptionDto body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateNotificationSubscriptionCommand
        {
            Id = id,
            Email = body.Email,
            WebhookUrl = body.WebhookUrl,
            WebhookSecret = body.WebhookSecret,
            EventTypes = body.EventTypes,
            EmissionSourceIds = body.EmissionSourceIds,
            IsEnabled = body.IsEnabled
        };
        var result = await sender.Send(command, cancellationToken);
        return result.Match(
            s => Ok(NotificationSubscriptionDto.FromDomainModel(s)),
            err => err.ToObjectResult());
    }

    [HttpDelete("{id:guid}")]
    [ProducesOkApiResponseType<NotificationSubscriptionDto>]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteNotificationSubscriptionCommand { Id = id };
        var result = await sender.Send(command, cancellationToken);
        return result.Match(
            s => Ok(NotificationSubscriptionDto.FromDomainModel(s)),
            err => err.ToObjectResult());
    }
}
