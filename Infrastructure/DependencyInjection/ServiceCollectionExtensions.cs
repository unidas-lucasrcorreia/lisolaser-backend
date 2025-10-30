using LisoLaser.Backend.Configuration;
using LisoLaser.Backend.Infrastructure.Http;
using LisoLaser.Backend.Services.Cms;
using LisoLaser.Backend.Services.Unobject;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LisoLaser.Backend.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCmsIntegration(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<CmsOptions>(config.GetSection("LisoLaser:Cms"));

            services.AddHttpClient<CmsService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(120);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddHttpMessageHandler(() => new RetryHandler(maxRetries: 3, perTryTimeout: TimeSpan.FromSeconds(4)))
            .AddHttpMessageHandler(sp => new ExternalApiHandler("cms", sp.GetRequiredService<ILogger<ExternalApiHandler>>()));

            return services;
        }

        public static IServiceCollection AddUnobjectIntegration(this IServiceCollection services, IConfiguration config)
        {
            services.AddOptions<UnobjectOptions>()
                .Bind(config.GetSection("LisoLaser:Unobject"))
                .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Missing Unobject BaseUrl")
                .Validate(o => !string.IsNullOrWhiteSpace(o.PublicToken), "Missing Unobject PublicToken")
                .ValidateOnStart();

            services.AddHttpClient<IUnobjectService, UnobjectService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<UnobjectOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(12);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("x-uno-public-token", opts.PublicToken);
            })
            .AddHttpMessageHandler(() => new RetryHandler(maxRetries: 3, perTryTimeout: TimeSpan.FromSeconds(4)))
            .AddHttpMessageHandler(sp => new ExternalApiHandler("unobject", sp.GetRequiredService<ILogger<ExternalApiHandler>>()));

            return services;
        }
    }
}
