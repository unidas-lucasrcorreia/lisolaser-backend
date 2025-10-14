using System.Net;

namespace LisoLaser.Backend.Infrastructure.Errors
{
    public class ExternalApiException : Exception
    {
        public string Service { get; }
        public HttpStatusCode StatusCode { get; }
        public string? ResponseBody { get; }
        public string? UpstreamMessage { get; } // mensagem amigável (UNO: { "message": "..." })

        public ExternalApiException(
            string service,
            HttpStatusCode statusCode,
            string? responseBody,
            string? upstreamMessage = null,
            string? message = null,
            Exception? innerException = null)
            : base(message ?? $"Erro na integração externa: {service}", innerException)
        {
            Service = service;
            StatusCode = statusCode;
            ResponseBody = responseBody;
            UpstreamMessage = upstreamMessage;
        }
    }
}
