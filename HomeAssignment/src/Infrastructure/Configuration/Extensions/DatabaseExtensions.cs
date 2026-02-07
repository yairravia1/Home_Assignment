using HomeAssignment.Domain.Interfaces;
using HomeAssignment.Domain.Models;
using HomeAssignment.Infrastructure.Configuration.Settings.MongoSettings;
using HomeAssignment.Infrastructure.Database.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace HomeAssignment.Infrastructure.Configuration.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddMongoDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ConfigureMongoMappings();

        var mongoSettings = new MongoSettings(
            ConnectionString: configuration.GetRequiredString("MongoSettings:ConnectionString"),
            DatabaseName: configuration.GetRequiredString("MongoSettings:DatabaseName"),
            CollectionName: configuration.GetRequiredString("MongoSettings:CollectionName"));

        services.AddSingleton(mongoSettings);
        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
        
        services.AddScoped<MongoActorRepository>();
        services.AddScoped<IActorRepository>(serviceProvider => serviceProvider.GetRequiredService<MongoActorRepository>());
        services.AddScoped<IActorIngestionRepository>(serviceProvider => serviceProvider.GetRequiredService<MongoActorRepository>());

        return services;
    }

    private static void ConfigureMongoMappings()
    {
        // Backward compatibility: existing documents store the external ID under the "ImdbId" field.
        // Domain model is provider-agnostic, so we map ExternalId <-> ImdbId here (Infrastructure layer).
        if (BsonClassMap.IsClassMapRegistered(typeof(Actor)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<Actor>(classMap =>
        {
            classMap.AutoMap();
            classMap.MapMember(a => a.ExternalId)
                .SetElementName("ImdbId")
                .SetIgnoreIfNull(true);
        });
    }
}
