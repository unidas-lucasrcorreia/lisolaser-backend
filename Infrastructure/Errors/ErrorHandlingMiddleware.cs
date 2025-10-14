using System.Text.Json;

namespace LisoLaser.Backend.Infrastructure.Errors;

public sealed class ErrorHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ExternalApiException ex)
        {
            await HandleExternalApiExceptionAsync(context, ex);
        }
        catch (TaskCanceledException ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout em integração externa. TraceId={TraceId}", context.TraceIdentifier);
            await WriteProblemAsync(context, 504, "Timeout em integração externa", "external_api_timeout");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request cancelado pelo cliente. TraceId={TraceId}", context.TraceIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro interno não tratado. TraceId={TraceId}", context.TraceIdentifier);
            await WriteProblemAsync(context, 500, "Erro interno", "internal_error");
        }
    }

    private async Task HandleExternalApiExceptionAsync(HttpContext context, ExternalApiException ex)
    {
        var upstreamStatus = (int)ex.StatusCode;
        var clientStatus = upstreamStatus is >= 400 and < 500 ? upstreamStatus : 502;

        var code = GetErrorCode(clientStatus);
        var detail = ex.UpstreamMessage ?? "Erro em integração externa";

        _logger.LogError(ex, "Erro de integração externa: {Service} -> {UpstreamStatus} (como {ClientStatus}). TraceId={TraceId}",
            ex.Service, upstreamStatus, clientStatus, context.TraceIdentifier);

        await WriteProblemAsync(context, clientStatus, detail, code, new Dictionary<string, object?>
        {
            ["service"] = ex.Service,
            ["upstreamStatus"] = upstreamStatus,
            ["upstreamBody"] = ex.ResponseBody
        });

    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string detail, string code, IDictionary<string, object?>? extra = null)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new Dictionary<string, object?>
        {
            ["title"] = GetTitle(statusCode),
            ["status"] = statusCode,
            ["detail"] = detail,
            ["code"] = code,
            ["traceId"] = context.TraceIdentifier,
        };

        if (extra != null)
        {
            foreach (var (key, value) in extra)
                problem[key] = value;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOpts));
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        400 => "Requisição inválida",
        401 => "Não autorizado",
        403 => "Proibido",
        404 => "Não encontrado",
        429 => "Muitas requisições",
        502 => "Erro em integração externa",
        504 => "Timeout em integração externa",
        _ => "Erro"
    };

    private static string GetErrorCode(int statusCode) => statusCode switch
    {
        400 => "bad_request",
        401 => "unauthorized",
        403 => "forbidden",
        404 => "not_found",
        429 => "too_many_requests",
        _ => "external_api_error"
    };
}
