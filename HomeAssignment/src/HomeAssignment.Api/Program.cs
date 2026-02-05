using HomeAssignment.Api.Configuration.Extensions;
using HomeAssignment.Infrastructure.Configuration.Extensions;
using HomeAssignment.Api.BackgroundServices;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddInputValidation();
        builder.Services.AddSwaggerConfiguration();
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddMongoDatabase(builder.Configuration);
        builder.Services.AddMessaging(builder.Configuration);
        builder.Services.AddScraper(builder.Configuration);
        builder.Services.AddHostedService<ActorCommandHandler>();
        builder.Services.AddHostedService<ActorIngestionWorker>();

        var app = builder.Build();

        app.SeedDatabase();
        app.UseSwaggerConfiguration();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}

