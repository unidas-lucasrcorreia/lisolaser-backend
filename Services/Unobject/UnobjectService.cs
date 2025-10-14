using System.Web;

namespace LisoLaser.Backend.Services.Unobject
{
    public sealed class UnobjectService : IUnobjectService
    {
        private readonly HttpClient _http;
        public UnobjectService(HttpClient http) => _http = http;

        public async Task<string> GetPublicFranchisesRawAsync(CancellationToken ct = default)
        {
            using var resp = await _http.GetAsync("franchises", ct);
            return await resp.Content.ReadAsStringAsync(ct);
        }

        public async Task<string> CreateLeadAsync(string jsonBody, CancellationToken ct = default)
        {
            using var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync("lead", content, ct);
            return await resp.Content.ReadAsStringAsync(ct);
        }

        // GET /v1/public/budget-schedule/{id}/hours?date=dd/MM/yyyy&franchiseIdentifier={id}&...
        public async Task<string> GetScheduleHoursAsync(
            int franchiseId,
            string date,
            IDictionary<string, string?>? extraQuery = null,
            CancellationToken ct = default)
        {
            var path = $"budget-schedule/{franchiseId}/hours";

            var qs = HttpUtility.ParseQueryString(string.Empty);
            qs["date"] = date; // dd/MM/yyyy
            qs["franchiseIdentifier"] = "2";

            if (extraQuery != null)
            {
                foreach (var kv in extraQuery)
                    if (!string.IsNullOrWhiteSpace(kv.Value))
                        qs[kv.Key] = kv.Value;
            }

            var uri = new UriBuilder(new Uri(_http.BaseAddress!, path)) { Query = qs.ToString()! }.Uri;
            using var resp = await _http.GetAsync(uri, ct);
            return await resp.Content.ReadAsStringAsync(ct);
        }

        // POST /v1/public/budget-schedule/{id}/create
        public async Task<string> CreateScheduleAsync(int franchiseId, string jsonBody, CancellationToken ct = default)
        {
            using var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync($"budget-schedule/{franchiseId}/create", content, ct);
            return await resp.Content.ReadAsStringAsync(ct);
        }
    }
}
