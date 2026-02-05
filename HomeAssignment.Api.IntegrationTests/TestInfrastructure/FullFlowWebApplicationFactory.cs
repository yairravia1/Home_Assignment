using HomeAssignment.Domain.Interfaces;
using EasyNetQ;
using HomeAssignment.Infrastructure.Configuration.Settings.MongoSettings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;


namespace HomeAssignment.Api.IntegrationTests.TestInfrastructure;

public sealed class FullFlowWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _mongoConnectionString;
    private readonly string _rabbitMqConnectionString;

    public FullFlowWebApplicationFactory(string mongoConnectionString, string rabbitMqConnectionString)
    {
        _mongoConnectionString = mongoConnectionString;
        _rabbitMqConnectionString = rabbitMqConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        
        builder.ConfigureTestServices(services =>
        {
            // If any background service throws, don't bring down the whole host.
            // This makes tests less flaky when infrastructure is still warming up.
            services.Configure<HostOptions>(options =>
            {
                options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
            });

            // We don't need the "log events" worker for full-flow tests.
            // It can fail if RabbitMQ isn't ready and may stop the host.
            RemoveHostedService<HomeAssignment.Api.BackgroundServices.ActorIngestionWorker>(services);

            // Force MongoDB to use the Testcontainer connection string.
            services.RemoveAll<MongoSettings>();
            services.RemoveAll<IMongoClient>();
            services.AddSingleton(new MongoSettings(
                ConnectionString: _mongoConnectionString,
                DatabaseName: "imdb",
                CollectionName: "actors"));
            services.AddSingleton<IMongoClient>(sp =>
            {
                var settings = sp.GetRequiredService<MongoSettings>();
                return new MongoClient(settings.ConnectionString);
            });

            // Force EasyNetQ to use the Testcontainer RabbitMQ connection string.
            // We remove existing bus registrations and re-register.
            services.RemoveAll<IBus>();
            services.AddEasyNetQ(_rabbitMqConnectionString);

            // -----------------------------
            // AUTH: Replace JWT with a tiny test scheme.
            // -----------------------------
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = AdminUserTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = AdminUserTestAuthHandler.SchemeName;
                    options.DefaultScheme = AdminUserTestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, AdminUserTestAuthHandler>(
                    AdminUserTestAuthHandler.SchemeName,
                    _ => { });

            // -----------------------------
            // SEEDING: Prevent IMDb scraping at startup.
            // -----------------------------
            services.RemoveAll<IActorIngestionRepository>();
            services.AddSingleton<IActorIngestionRepository, NoOpActorIngestionRepository>();

            // -----------------------------
            // OBSERVABILITY FOR TESTS: capture published events.
            // -----------------------------
            services.TryAddSingleton<ActorChangedEventCollector>();
            services.AddHostedService(sp => sp.GetRequiredService<ActorChangedEventCollector>());
        });
    }

    private static void RemoveHostedService<THostedService>(IServiceCollection services)
        where THostedService : class, IHostedService
    {
        var descriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(THostedService))
            .ToList();

        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }
}

