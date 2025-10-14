using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using LisoLaser.Backend.Services.Cms;
using LisoLaser.Backend.Services.Unobject;

namespace LisoLaser.Backend.Controllers;

public sealed class ResolveReferencesRequest
{
    public List<string> Ids { get; set; } = new();
    public bool ResolveAssetUrls { get; set; } = true;
    public bool Flatten { get; set; } = true;
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CmsController : ControllerBase
{
    private readonly CmsService _cmsService;
    private readonly IUnobjectService _unobjectService;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan UnoFranchisesTtl = TimeSpan.FromMinutes(10);

    public CmsController(CmsService cmsService, IUnobjectService unobjectService, IMemoryCache cache)
    {
        _cmsService = cmsService;
        _unobjectService = unobjectService;
        _cache = cache;
    }

    [HttpGet("{schema}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string schema, CancellationToken ct)
    {
        var result = await _cmsService.GetContentAsync(schema, ct);
        return Content(result ?? "{}", "application/json");
    }

    [HttpGet("unidade/getByUnoId/{externalId}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUnidadeByExternalId(string externalId, CancellationToken ct)
    {
        var item = await _cmsService.GetUnidadeByExternalIdAsync(externalId, ct);
        return item is null ? NotFound() : Content(item, "application/json");
    }


  // CmsController.cs

[HttpGet("unidades")]
[ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
public async Task<IActionResult> GetAllUnidades(
    [FromQuery] int? page = null,
    [FromQuery] int? pageSize = null,
    [FromQuery] bool onlyWithUno = false,
    [FromQuery] string? search = null, // 👈 NOVO
    CancellationToken ct = default)
{
    HashSet<string>? allowedIds = null;
    Dictionary<string, (double? lat, double? lon)>? latLonByExternalId = null;

    if (onlyWithUno)
    {
        const string cacheKey = "uno:franchises:raw";
        string unoJson = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = UnoFranchisesTtl;
            return await _unobjectService.GetPublicFranchisesRawAsync(ct);
        }) ?? "{}";

        using var doc = JsonDocument.Parse(unoJson);
        if (doc.RootElement.TryGetProperty("franchises", out var franchises))
        {
            allowedIds = new HashSet<string>();
            latLonByExternalId = new Dictionary<string, (double? lat, double? lon)>();

            foreach (var f in franchises.EnumerateArray())
            {
                if (!f.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number)
                    continue;

                var extId = idEl.GetInt32().ToString();
                allowedIds.Add(extId);

                double? lat = null, lon = null;
                if (f.TryGetProperty("address", out var addr))
                {
                    if (addr.TryGetProperty("latitude", out var latEl) && latEl.ValueKind == JsonValueKind.Number)
                        lat = latEl.GetDouble();
                    if (addr.TryGetProperty("longitude", out var lonEl) && lonEl.ValueKind == JsonValueKind.Number)
                        lon = lonEl.GetDouble();
                }
                latLonByExternalId[extId] = (lat, lon);
            }
        }
        else
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                title = "Erro ao consultar UNO",
                detail = "Payload sem 'franchises'."
            });
        }
    }

    var json = await _cmsService.GetAllUnidadesAsync(page, pageSize, allowedIds, latLonByExternalId, search, ct);
    return Content(json, "application/json");
}


    [HttpGet("blog/posts")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlogPosts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (page <= 0 || pageSize <= 0)
            return BadRequest("page e pageSize devem ser > 0.");

        var json = await _cmsService.GetBlogPostsAsync(page, pageSize, search, ct);
        return Content(json, "application/json");
    }

    [HttpGet("blog/posts/{slug}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogPostBySlug(string slug, CancellationToken ct = default)
    {
        var json = await _cmsService.GetBlogPostBySlugAsync(slug, ct);
        return json is null ? NotFound() : Content(json, "application/json");
    }

    // POST /api/cms/{schema}/resolve?dataOnly=true
    [HttpPost("{schema}/resolve")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResolveBySchema(
        string schema,
        [FromBody] ResolveReferencesRequest body,
        [FromQuery] bool dataOnly = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(schema))
            return BadRequest("Schema é obrigatório.");
        if (body?.Ids == null || body.Ids.Count == 0)
            return Ok("[]");

        var json = dataOnly
            ? await _cmsService.GetContentDataByIdsAsync(schema, body.Ids, body.ResolveAssetUrls, body.Flatten, ct)
            : await _cmsService.GetContentByIdsAsync(schema, body.Ids, body.ResolveAssetUrls, body.Flatten, ct);

        return Content(json, "application/json");
    }

// POST /api/cms/{schema}/resolve/data
[HttpPost("{schema}/resolve/data")]
public async Task<IActionResult> ResolveDataOnly(
    string schema,
    [FromBody] ResolveReferencesRequest body,
    CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(schema))
        return BadRequest("Schema é obrigatório.");
    if (body?.Ids == null || body.Ids.Count == 0)
        return Ok("[]");

    var json = await _cmsService.GetContentDataByIdsAsync(schema, body.Ids, body.ResolveAssetUrls, body.Flatten, ct);
    return Content(json, "application/json");
}


[HttpPost("cache/clear")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
public IActionResult ClearCache([FromServices] IMemoryCache cache)
{
    if (cache is MemoryCache mem)
    {
        mem.Compact(1.0);
    }

    return NoContent();
}


}
