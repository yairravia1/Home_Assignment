using HomeAssignment.Infrastructure.Scraper;

namespace HomeAssignment.Api.Configuration.Extensions;

public static class DataSeedingExtensions
{
    public static WebApplication SeedDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        try
        {
            var seeder = scope.ServiceProvider.GetRequiredService<ActorSeedService>();
            seeder.SeedTopChartActors();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Database seeding failed");
        }

        return app;
    }
}

