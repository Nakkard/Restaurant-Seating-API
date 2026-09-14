using Api.Contracts;
using BusinessLogic.Actions;
using BusinessLogic.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/groups")]
public sealed class GroupsController(
    ArriveGroupAction arriveGroup,
    LeaveGroupAction leaveGroup,
    QueryRestaurantAction queryRestaurant) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(GroupResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GroupResult>> Arrive(ArriveGroupRequest request, CancellationToken cancellationToken)
    {
        var group = await arriveGroup.Make(request.Size, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = group.Id }, group);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GroupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupResult>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await queryRestaurant.GetGroup(id, cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(GroupPage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GroupPage>> List(
        CancellationToken cancellationToken, [FromQuery] GroupStatus? status = null,
        [FromQuery] int offset = 0, [FromQuery] int limit = 50) =>
        Ok(await queryRestaurant.GetGroups(status, offset, limit, cancellationToken));

    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(typeof(GroupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GroupResult>> Leave(Guid id, CancellationToken cancellationToken) =>
        Ok(await leaveGroup.Make(id, cancellationToken));
}
