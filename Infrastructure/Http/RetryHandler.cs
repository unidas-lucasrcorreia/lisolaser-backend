using System.Net;
using System.Net.Http;

namespace LisoLaser.Backend.Infrastructure.Http
{
    /// <summary>
    /// Retry simples (exponencial com jitter) e timeout por tentativa, sem Polly.
    /// </summary>
    public sealed class RetryHandler : DelegatingHandler
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _perTryTimeout;
        private readonly Func<int, TimeSpan> _delayFactory;

        public RetryHandler(int maxRetries = 3, TimeSpan? perTryTimeout = null, Func<int, TimeSpan>? delayFactory = null)
        {
            _maxRetries = Math.Max(0, maxRetries);
            _perTryTimeout = perTryTimeout ?? TimeSpan.FromSeconds(4);
            _delayFactory = delayFactory ?? (attempt =>
            {
                var baseMs = Math.Pow(2, attempt - 1) * 200; // 200, 400, 800...
                var jitter = Random.Shared.Next(0, 120);
                return TimeSpan.FromMilliseconds(baseMs + jitter);
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var attempt = 0;

            while (true)
            {
                attempt++;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_perTryTimeout);

                try
                {
                    var response = await base.SendAsync(request, cts.Token);

                    if (!ShouldRetry(response) || attempt > _maxRetries)
                        return response;
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    if (attempt > _maxRetries) throw;
                }
                catch (HttpRequestException)
                {
                    if (attempt > _maxRetries) throw;
                }

                await Task.Delay(_delayFactory(attempt), ct);
            }
        }

        private static bool ShouldRetry(HttpResponseMessage resp)
        {
            if ((int)resp.StatusCode < 400) return false;
            return resp.StatusCode == HttpStatusCode.TooManyRequests
                || resp.StatusCode == HttpStatusCode.RequestTimeout
                || ((int)resp.StatusCode >= 500 && (int)resp.StatusCode <= 599);
        }
    }
}
