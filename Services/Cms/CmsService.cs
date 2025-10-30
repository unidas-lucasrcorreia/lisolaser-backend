using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using LisoLaser.Backend.Configuration;
using LisoLaser.Backend.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace LisoLaser.Backend.Services.Cms;

public class CmsService
{
    private readonly HttpClient _httpClient;
    private readonly CmsOptions _options;
    private readonly ILogger<CmsService> _logger;
    private readonly IMemoryCache _cache;

    private string? _accessToken;
    private const string BlogSchema = "blog";

    // TTL único para todos os caches (exceto autenticação, que NÃO é cacheada)
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public CmsService(
        HttpClient httpClient,
        IOptions<CmsOptions> options,
        IMemoryCache cache,
        ILogger<CmsService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _cache = cache;
    }

    private async Task AuthenticateAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_accessToken)) return;

        var cacheKey = $"squidex:accessToken";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
        {
            _accessToken = cached;
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/identity-server/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = "squidex-api"
            })
        };

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<TokenResponse>(json);
        _accessToken = result?.AccessToken;

        _cache.Set(cacheKey, _accessToken, CacheTtl);
    }

    public async Task<string?> GetContentAsync(string schema, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);

        var cacheKey = $"squidex:{schema}:latest";
        if (_cache.TryGetValue(cacheKey, out string? cachedLatest) && cachedLatest is not null)
            return cachedLatest;

        // Pega só 1 item, ordenado pelo mais recente
        var url =
            $"{_options.BaseUrl}/api/content/{_options.AppName}/{schema}" +
            $"?$top=1&$orderby=lastModified%20desc";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("X-Flatten", "true");
        // Opcional: resolve URLs de assets (imagens/arquivos)
        request.Headers.Add("X-Resolve-Urls", "*");

        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            return null;

        var first = items[0];
        if (first.TryGetProperty("data", out var data))
        {
            var json = data.GetRawText();
            _cache.Set(cacheKey, json, CacheTtl);
            return json;
        }

        return null;
    }

    public async Task<string> GetContentByIdsAsync(
        string schema,
        IEnumerable<string> ids,
        bool resolveAssetUrls = true,
        bool flatten = true,
        CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);

        var idArray = ids?.Where(s => !string.IsNullOrWhiteSpace(s))
                          .Distinct(StringComparer.Ordinal)
                          .ToArray() ?? Array.Empty<string>();

        // Nada para buscar
        if (idArray.Length == 0)
            return "[]";

        // Cache por schema + conjunto de IDs
        var cacheKey = $"squidex:{schema}:byIds:{string.Join(",", idArray.OrderBy(x => x, StringComparer.Ordinal))}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        var url = $"{_options.BaseUrl}/api/content/{_options.AppName}/{schema}/query";

        var bodyObj = new
        {
            ids = idArray,
            take = idArray.Length
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(bodyObj),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        if (flatten) req.Headers.Add("X-Flatten", "true");
        if (resolveAssetUrls) req.Headers.Add("X-Resolve-Urls", "*");

        using var res = await _httpClient.SendAsync(req, ct);
        var payload = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Squidex {(int)res.StatusCode} {res.ReasonPhrase}: {payload}");

        // Retornar somente o array de items (mais prático pro front)
        using var doc = JsonDocument.Parse(payload);
        var items = doc.RootElement.GetProperty("items");

        var json = items.GetRawText();
        _cache.Set(cacheKey, json, CacheTtl);
        return json;
    }

    public async Task<string> GetContentDataByIdsAsync(
        string schema,
        IEnumerable<string> ids,
        bool resolveAssetUrls = true,
        bool flatten = true,
        CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);

        var idArray = ids?.Where(s => !string.IsNullOrWhiteSpace(s))
                          .Distinct(StringComparer.Ordinal)
                          .ToArray() ?? Array.Empty<string>();

        if (idArray.Length == 0)
            return "[]";

        var cacheKey = $"squidex:{schema}:byIds:dataOnly:{string.Join(",", idArray.OrderBy(x => x, StringComparer.Ordinal))}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        var url = $"{_options.BaseUrl}/api/content/{_options.AppName}/{schema}/query";

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { ids = idArray, take = idArray.Length }),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        if (flatten) req.Headers.Add("X-Flatten", "true");
        if (resolveAssetUrls) req.Headers.Add("X-Resolve-Urls", "*");

        using var res = await _httpClient.SendAsync(req, ct);
        var payload = await res.Content.ReadAsStringAsync(ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(payload);
        var items = doc.RootElement.GetProperty("items");

        // Projeta só o "data" de cada item
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("data", out var dataEl))
                {
                    dataEl.WriteTo(writer);
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();
        }

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        _cache.Set(cacheKey, json, CacheTtl);
        return json;
    }

    private async Task<string> FetchSquidexQueryPageAsync(
        int skip,
        int take,
        string? fullText,
        HashSet<string>? allowedExternalIds,
        CancellationToken ct)
    {
        var cacheKey =
            $"squidex:unidade:query:skip={skip}:take={take}:q={fullText ?? ""}:ids=" +
            (allowedExternalIds is null ? "-" : string.Join(",", allowedExternalIds.OrderBy(x => x, StringComparer.Ordinal)));

        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        var andFilters = new List<object>();

        if (allowedExternalIds is { Count: > 0 })
        {
            andFilters.Add(new
            {
                path = "data/externalId/iv",
                op = "in",
                value = allowedExternalIds.ToArray()
            });
        }

        var qObj = new
        {
            fullText = string.IsNullOrWhiteSpace(fullText) ? null : fullText,
            take = take,
            skip = skip,
            sort = new[] { new { path = "lastModified", order = "descending" } },
            filter = new { and = andFilters }
        };

        var qJson = JsonSerializer.Serialize(
            qObj,
            new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }
        );

        var qParam = Uri.EscapeDataString(qJson);
        var url = $"{_options.BaseUrl}/api/content/{_options.AppName}/unidade?q={qParam}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        req.Headers.Add("X-Flatten", "true");
        req.Headers.Add("X-Resolve-Urls", "*");

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Squidex {(int)res.StatusCode} {res.ReasonPhrase}: {body}");

        _cache.Set(cacheKey, body, CacheTtl);
        return body;
    }

    public async Task<string?> GetUnidadeByExternalIdAsync(string externalId, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);

        var cacheKey = $"squidex:unidade:byExternalId:{externalId}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        var filter = Uri.EscapeDataString($"data/externalId/iv eq '{externalId}'");
        var url = $"{_options.BaseUrl}/api/content/{_options.AppName}/unidade?$filter={filter}&$top=1";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        req.Headers.Add("X-Flatten", "true");

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            return null;

        var json = items[0].GetRawText();
        _cache.Set(cacheKey, json, CacheTtl);
        return json;
    }

    public async Task<string> GetAllUnidadesAsync(
        int? page,
        int? pageSize,
        HashSet<string>? allowedExternalIds,
        Dictionary<string, (double? lat, double? lon)>? latLonByExternalId,
        string? search,
        CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);

        if (page.HasValue != pageSize.HasValue)
            throw new ArgumentException("Informe page e pageSize juntos, ou nenhum.");
        if (page is <= 0 || pageSize is <= 0)
            throw new ArgumentOutOfRangeException(nameof(page), "page e pageSize devem ser > 0.");

        // Quando há 'search', SEMPRE usamos o endpoint com 'q=' (paginação necessária)
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Garantir paginação para search
            var p = page ?? 1;
            var ps = pageSize ?? 10;

            var skip = (p - 1) * ps;
            var payload = await FetchSquidexQueryPageAsync(skip, ps, search, allowedExternalIds, ct);

            using var doc = JsonDocument.Parse(payload);
            var total = doc.RootElement.GetProperty("total").GetInt32();
            var rawItems = doc.RootElement.GetProperty("items").EnumerateArray().Select(i => i.Clone()).ToList();

            if (latLonByExternalId is { Count: > 0 })
                rawItems = EnrichWithUnoLatLon(rawItems, latLonByExternalId);

            var wrapped = new
            {
                total,
                page = p,
                pageSize = ps,
                totalPages = (int)Math.Ceiling(total / (double)ps),
                items = rawItems
            };
            return JsonSerializer.Serialize(wrapped);
        }

        // ===== SEM SEARCH (comportamento anterior) =====

        // Filtro UNO por externalIds (sem search)
        if (allowedExternalIds is { Count: > 0 })
        {
            var allFiltered = await FetchByExternalIdsAsync(allowedExternalIds, batchSize: 50, ct);

            if (latLonByExternalId is { Count: > 0 })
                allFiltered = EnrichWithUnoLatLon(allFiltered, latLonByExternalId);

            if (page.HasValue && pageSize.HasValue)
            {
                var total = allFiltered.Count;
                var totalPages = (int)Math.Ceiling(total / (double)pageSize.Value);
                var skip = (page.Value - 1) * pageSize.Value;
                var pageItems = allFiltered.Skip(skip).Take(pageSize.Value).ToList();

                var wrapped = new
                {
                    total,
                    page = page.Value,
                    pageSize = pageSize.Value,
                    totalPages,
                    items = pageItems
                };
                return JsonSerializer.Serialize(wrapped);
            }

            return SerializeArray(allFiltered);
        }

        // Paginação simples sem search
        if (page.HasValue && pageSize.HasValue)
        {
            var skip = (page.Value - 1) * pageSize.Value;
            var payload = await FetchSquidexPageAsync(skip, pageSize.Value, ct);

            if (latLonByExternalId is { Count: > 0 })
            {
                using var doc = JsonDocument.Parse(payload);
                var total = doc.RootElement.GetProperty("total").GetInt32();
                var rawItems = doc.RootElement.GetProperty("items").EnumerateArray().Select(i => i.Clone()).ToList();

                rawItems = EnrichWithUnoLatLon(rawItems, latLonByExternalId);

                var wrapped = new
                {
                    total,
                    page = page.Value,
                    pageSize = pageSize.Value,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize.Value),
                    items = rawItems
                };
                return JsonSerializer.Serialize(wrapped);
            }
            return payload;
        }
        else
        {
            var all = await FetchAllUnidadesFlattenAsync(ct);
            if (latLonByExternalId is { Count: > 0 })
                all = EnrichWithUnoLatLon(all, latLonByExternalId);

            return SerializeArray(all);
        }
    }

    private async Task<string> FetchSquidexPageAsync(int skip, int top, CancellationToken ct)
    {
        var cacheKey = $"squidex:unidade:page:skip={skip}:top={top}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        var url = $"{_options.BaseUrl}/api/content/{_options.AppName}/unidade?$top={top}&$skip={skip}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        req.Headers.Add("X-Flatten", "true");

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Squidex {(int)res.StatusCode} {res.ReasonPhrase}: {body}");

        _cache.Set(cacheKey, body, CacheTtl);
        return body;
    }

    private async Task<List<JsonElement>> FetchAllUnidadesFlattenAsync(CancellationToken ct)
    {
        var items = new List<JsonElement>();

        var firstPayload = await FetchSquidexPageAsync(skip: 0, top: 200, ct);
        using (var firstDoc = JsonDocument.Parse(firstPayload))
        {
            var root = firstDoc.RootElement;
            var totalAll = root.GetProperty("total").GetInt32();
            var firstItems = root.GetProperty("items");

            foreach (var it in firstItems.EnumerateArray())
                items.Add(it.Clone());

            var top = 200;
            var totalPages = (int)Math.Ceiling(totalAll / (double)top);

            var tasks = new List<Task<string>>();
            for (var p = 1; p < totalPages; p++)
            {
                var skip = p * top;
                tasks.Add(FetchSquidexPageAsync(skip, top, ct));
            }
            var pages = await Task.WhenAll(tasks);

            foreach (var payload in pages)
            {
                using var doc = JsonDocument.Parse(payload);
                var pageItems = doc.RootElement.GetProperty("items");
                foreach (var it in pageItems.EnumerateArray())
                    items.Add(it.Clone());
            }
        }

        return items;
    }

    private async Task<List<JsonElement>> FetchByExternalIdsAsync(IEnumerable<string> ids, int batchSize, CancellationToken ct)
    {
        var batches = ids
            .Distinct(StringComparer.Ordinal)
            .Chunk(batchSize)
            .ToArray();

        var tasks = batches.Select(b => FetchChunkAsync(b, ct));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }

    private async Task<List<JsonElement>> FetchChunkAsync(IEnumerable<string> chunk, CancellationToken ct)
    {
        var idsArr = chunk.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var cacheKey = $"squidex:unidade:chunk:{string.Join(",", idsArr)}";

        if (_cache.TryGetValue(cacheKey, out List<JsonElement>? cached) && cached is not null)
            return cached.Select(e => e.Clone()).ToList(); // clone pra segurança

        var idsSql = string.Join(",", idsArr.Select(id => $"'{id}'")); // '8944','8883',...
        var filter = Uri.EscapeDataString($"data/externalId/iv in ({idsSql})");
        var url = $"{_options.BaseUrl}/api/content/{_options.AppName}/unidade?$filter={filter}&$top=200";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        req.Headers.Add("X-Flatten", "true");

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Squidex {(int)res.StatusCode} {res.ReasonPhrase}: {body}");

        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("items").EnumerateArray().Select(i => i.Clone()).ToList();

        _cache.Set(cacheKey, items, CacheTtl);
        return items;
    }

    private static List<JsonElement> EnrichWithUnoLatLon(
        List<JsonElement> items,
        Dictionary<string, (double? lat, double? lon)> latLonByExternalId)
    {
        var enriched = new List<JsonElement>(items.Count);

        foreach (var it in items)
        {
            try
            {
                var node = JsonNode.Parse(it.GetRawText()) as JsonObject;
                var ext = node?["data"]?["externalId"]?.GetValue<string>();

                if (ext != null && latLonByExternalId.TryGetValue(ext, out var pair))
                {
                    var data = node!["data"] as JsonObject ?? new JsonObject();
                    node!["data"] = data;

                    var addr = data["address"] as JsonObject ?? new JsonObject();
                    data["address"] = addr;

                    if (pair.lat.HasValue) addr["latitude"] = pair.lat.Value;
                    if (pair.lon.HasValue) addr["longitude"] = pair.lon.Value;
                }

                using var tmpDoc = JsonDocument.Parse(node!.ToJsonString());
                enriched.Add(tmpDoc.RootElement.Clone());
            }
            catch
            {
                enriched.Add(it);
            }
        }

        return enriched;
    }

    private static string SerializeArray(List<JsonElement> elements)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        foreach (var e in elements) e.WriteTo(writer);
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public async Task<string> GetBlogPostsAsync(int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);

        var cacheKey = $"squidex:{BlogSchema}:list:p={page}:ps={pageSize}:q={search ?? ""}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        var skip = (page - 1) * pageSize;

        var qObj = new
        {
            fullText = string.IsNullOrWhiteSpace(search) ? null : search,
            take = pageSize,
            skip,
            sort = new[] { new { path = "lastModified", order = "descending" } },
            filter = new { and = Array.Empty<object>() }
        };

        var qJson = JsonSerializer.Serialize(qObj, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        var qParam = Uri.EscapeDataString(qJson);

        var url = $"{_options.BaseUrl}/api/content/{_options.AppName}/{BlogSchema}?q={qParam}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        req.Headers.Add("X-Flatten", "true");

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var total = root.GetProperty("total").GetInt32();
        var items = root.GetProperty("items");

        var flats = new List<JsonElement>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("data", out var data))
                flats.Add(data.Clone());
        }

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("total", total);
            writer.WriteNumber("page", page);
            writer.WriteNumber("pageSize", pageSize);
            writer.WriteNumber("totalPages", totalPages);
            writer.WritePropertyName("items");
            writer.WriteStartArray();
            foreach (var f in flats) f.WriteTo(writer);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        _cache.Set(cacheKey, json, CacheTtl);
        return json;
    }

    public async Task<string?> GetBlogPostBySlugAsync(string slug, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);

        var cacheKey = $"squidex:{BlogSchema}:slug:{slug}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        var filter = Uri.EscapeDataString($"data/slug/iv eq '{slug}'");
        var url =
            $"{_options.BaseUrl}/api/content/{_options.AppName}/{BlogSchema}" +
            $"?$filter={filter}&$top=1";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        req.Headers.Add("X-Flatten", "true");

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            return null;

        // devolve apenas o "data"
        var first = items[0];
        if (first.TryGetProperty("data", out var data))
        {
            var json = data.GetRawText();
            _cache.Set(cacheKey, json, CacheTtl);
            return json;
        }

        return null;
    }
}
