using EasyNetQ;
using HomeAssignment.Domain.Interfaces;
using HomeAssignment.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeAssignment.Infrastructure.Configuration.Extensions;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredString("Messaging:RabbitMq:ConnectionString");

        services.AddEasyNetQ(connectionString);
        services.AddScoped<IMessagePublisher, EasyNetQProducer>();

        return services;
    }
}
