using Microsoft.AspNetCore.Mvc;
using LisoLaser.Backend.Services.Unobject;

namespace LisoLaser.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FranchisesController : ControllerBase
{
    private readonly IUnobjectService _unobject;

    public FranchisesController(IUnobjectService unobject) => _unobject = unobject;

    [HttpGet]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var json = await _unobject.GetPublicFranchisesRawAsync(ct);
        return Content(json, "application/json");
    }
}
