using System.Text.Json;
using LisoLaser.Backend.Infrastructure.Errors;
using Microsoft.Extensions.Logging;

namespace LisoLaser.Backend.Infrastructure.Http
{
    public sealed class ExternalApiHandler : DelegatingHandler
    {
        private readonly string _serviceName;
        private readonly ILogger<ExternalApiHandler> _logger;

        public ExternalApiHandler(string serviceName, ILogger<ExternalApiHandler> logger)
        {
            _serviceName = serviceName;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var resp = await base.SendAsync(request, ct);

            if (!resp.IsSuccessStatusCode)
            {
                string? body = null;
                string? upstreamMessage = null;

                try
                {
                    body = await resp.Content.ReadAsStringAsync(ct);

                    // Tenta extrair { "message": "..." } (padrão UNO)
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            if (doc.RootElement.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                            {
                                upstreamMessage = msgProp.GetString();
                            }
                        }
                        catch
                        {
                            // se não for JSON válido, ignora
                        }
                    }
                }
                catch { /* ignore leitura de corpo */ }

                _logger.LogWarning("{Service} returned {Status} for {Url}. Body={Body}",
                    _serviceName, (int)resp.StatusCode, request.RequestUri, body);

                throw new ExternalApiException(
                    service: _serviceName,
                    statusCode: resp.StatusCode,
                    responseBody: body,
                    upstreamMessage: upstreamMessage,
                    message: $"Erro ao chamar {_serviceName}: {(int)resp.StatusCode} {resp.ReasonPhrase}"
                );
            }

            return resp;
        }
    }
}
