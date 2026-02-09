using HomeAssignment.Infrastructure.Configuration.Settings.ScraperSettings;
using HomeAssignment.Infrastructure.Scraper;
using HomeAssignment.Infrastructure.Scraper.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeAssignment.Infrastructure.Configuration.Extensions;

public static class ScraperExtensions
{
    public static IServiceCollection AddScraper(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["ScraperSettings:Provider"] ?? "Imdb";

        var scraperSettings = new ScraperSettings(
            Provider: provider,
            TopChartUrl: configuration.GetRequiredString("ScraperSettings:TopChartUrl"),
            TopChartSimpleUrl: configuration.GetRequiredString("ScraperSettings:TopChartSimpleUrl"),
            RottenTomatoesBestMoviesUrl: configuration["ScraperSettings:RottenTomatoesBestMoviesUrl"] ?? "",
            MovieCount: configuration.GetRequiredInt("ScraperSettings:MovieCount"),
            MaxRank: configuration.GetRequiredInt("ScraperSettings:MaxRank"),
            SourceName: configuration.GetRequiredString("ScraperSettings:SourceName"));

        services.AddSingleton(scraperSettings);

        if (provider.Equals("RottenTomatoes", StringComparison.OrdinalIgnoreCase))
        {
            var bestMoviesUrl = configuration.GetRequiredString("ScraperSettings:RottenTomatoesBestMoviesUrl");
            services.AddSingleton<IActorSourceProvider>(_ => new RottenTomatoesProvider(bestMoviesUrl));
        }
        else
        {
            services.AddSingleton<IActorSourceProvider>(serviceProvider =>
            {
                var settings = serviceProvider.GetRequiredService<ScraperSettings>();
                return new ImdbProvider(settings.TopChartUrl, settings.TopChartSimpleUrl);
            });
        }

        services.AddScoped<ActorSeedService>();

        return services;
    }
}
