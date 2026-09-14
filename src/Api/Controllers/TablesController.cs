using BusinessLogic.Actions;
using BusinessLogic.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/tables")]
public sealed class TablesController(QueryRestaurantAction queryRestaurant) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TableResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TableResult>>> Get(CancellationToken cancellationToken) =>
        Ok(await queryRestaurant.GetTables(cancellationToken));
}
