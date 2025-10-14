using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using LisoLaser.Backend.Models;
using LisoLaser.Backend.Services.Unobject;
using Microsoft.AspNetCore.Mvc;

namespace LisoLaser.Backend.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/franchises/{franchiseId:int}/schedule")]
public class SchedulesController : ControllerBase
{
    private readonly IUnobjectService _unobject;
    private readonly int _unoFranchiseIdentifier;
    private static readonly JsonSerializerOptions UnoJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SchedulesController(IUnobjectService unobject, IConfiguration cfg)
    {
        _unobject = unobject;
        _unoFranchiseIdentifier = cfg.GetValue<int>("Unobject:FranchiseIdentifier", 2);
    }

    [HttpGet("hours")]
    public async Task<IActionResult> GetHours(int franchiseId, [FromQuery][Required] string? date, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(date))
            return BadRequest(new { message = "Query param 'date' é obrigatório (ex.: dd/MM/yyyy)." });

        var extra = Request.Query
            .Where(kv => !string.Equals(kv.Key, "date", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(k => k.Key, v => v.Value.ToString());

        var json = await _unobject.GetScheduleHoursAsync(franchiseId, date!, extra, ct);
        return Content(json, "application/json");
    }

[HttpPost]
[Consumes("application/json")]
public async Task<IActionResult> Create(int franchiseId, [FromBody] ScheduleCreateRequest body, CancellationToken ct)
{
    if (!ModelState.IsValid)
        return ValidationProblem(ModelState);

    body.FranchiseIdentifier = 2; // fixo

    // if (string.IsNullOrWhiteSpace(body.Email) || !body.Email.Contains("@"))
    //     return BadRequest(new { message = "Campo 'email' é obrigatório e deve ser válido." });


    var unoPayload = new {
        date = body.Date,
        franchiseIdentifier = body.FranchiseIdentifier,
        hour = body.Hour,
        name = body.Name,
        cellPhone = body.CellPhone,
        roomId = body.RoomId
    };

    var json = JsonSerializer.Serialize(unoPayload, UnoJson);
    var result = await _unobject.CreateScheduleAsync(franchiseId, json, ct);

    return Content(result, "application/json");
}

}


    // [HttpPost]
    // [Consumes("application/json")]
    // public async Task<IActionResult> Create(int franchiseId, [FromBody] ScheduleCreateRequest body, CancellationToken ct)
    // {
    //     if (!ModelState.IsValid) return ValidationProblem(ModelState);

    //     // validações leves do seu backend
    //     if (string.IsNullOrWhiteSpace(body.Email) || !body.Email.Contains('@'))
    //         return BadRequest(new { message = "Campo 'email' é obrigatório e deve ser válido." });
    //     if (string.IsNullOrWhiteSpace(body.Date))
    //         return BadRequest(new { message = "Campo 'date' é obrigatório." });
    //     if (string.IsNullOrWhiteSpace(body.Hour) || body.Hour.Length != 5)
    //         return BadRequest(new { message = "Campo 'hour' deve estar no formato HH:mm." });
    //     if (body.RoomId <= 0)
    //         return BadRequest(new { message = "Campo 'roomId' é obrigatório e deve ser > 0." });

    //     // força franchiseIdentifier SEMPRE = 2 (regra de negócio)
    //     body.FranchiseIdentifier = _unoFranchiseIdentifier;

    //     // monte o payload aceito pela UNO (sem Email e sem DealActivityId)
    //     var unoPayload = new
    //     {
    //         date = body.Date,
    //         franchiseIdentifier = body.FranchiseIdentifier, // 2
    //         hour = body.Hour,
    //         name = body.Name,
    //         cellPhone = body.CellPhone,
    //         roomId = body.RoomId
    //     };

    //     var json = JsonSerializer.Serialize(unoPayload, UnoJson);
    //     var result = await _unobject.CreateScheduleAsync(franchiseId, json, ct);
    //     return Content(result, "application/json");
    // }