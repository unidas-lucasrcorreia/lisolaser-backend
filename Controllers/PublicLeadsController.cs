using System.Text.Json;
using System.Text.Json.Serialization;
using LisoLaser.Backend.Models;
using LisoLaser.Backend.Services.Unobject;
using Microsoft.AspNetCore.Mvc;

namespace LisoLaser.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class LeadsController : ControllerBase
    {
        private readonly IUnobjectService _unobject;

        private static readonly JsonSerializerOptions UnoJson = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public LeadsController(IUnobjectService unobject) => _unobject = unobject;

        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> Create([FromBody] PublicLeadRequest body, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var normalized = new PublicLeadRequest
            {
                FranchiseId      = body.FranchiseId,
                Name             = body.Name,
                CellPhone        = body.CellPhone,
                Email            = string.IsNullOrWhiteSpace(body.Email) ? null : body.Email,
                Observation      = string.IsNullOrWhiteSpace(body.Observation) ? null : body.Observation,
                Origin           = string.IsNullOrWhiteSpace(body.Origin) ? null : body.Origin,
                CampaignSlug     = string.IsNullOrWhiteSpace(body.CampaignSlug) ? null : body.CampaignSlug,
                AdCampaignName   = string.IsNullOrWhiteSpace(body.AdCampaignName) ? null : body.AdCampaignName,
                AdSetName        = string.IsNullOrWhiteSpace(body.AdSetName) ? null : body.AdSetName,
                AdName           = string.IsNullOrWhiteSpace(body.AdName) ? null : body.AdName,
                FacebookSourceId = string.IsNullOrWhiteSpace(body.FacebookSourceId) ? null : body.FacebookSourceId,
                FacebookWaclId   = string.IsNullOrWhiteSpace(body.FacebookWaclId) ? null : body.FacebookWaclId,
                Rating           = body.Rating,
                RecentCheckDays  = body.RecentCheckDays,
                Bot              = body.Bot
            };

            var json = JsonSerializer.Serialize(normalized, UnoJson);
            var result = await _unobject.CreateLeadAsync(json, ct);
            return Content(result, "application/json");
        }
    }
}
