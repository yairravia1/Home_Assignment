using EasyNetQ;
using HomeAssignment.Domain.Events;

namespace HomeAssignment.Api.BackgroundServices;

public class ActorIngestionWorker : BackgroundService
{
    private readonly IBus _bus;
    private readonly ILogger<ActorIngestionWorker> _logger;

    public ActorIngestionWorker(IBus bus, ILogger<ActorIngestionWorker> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bus.PubSub.SubscribeAsync<ActorChangedEvent>(
            subscriptionId: "actor.events.log",
            onMessage: (ActorChangedEvent message) =>
            {
                _logger.LogInformation(
                    "Event received: Actor {ActorId} was {ChangeType} at {Timestamp}",
                    message.ActorId,
                    message.ChangeType,
                    message.OccurredAt);
            },
            cancellationToken: stoppingToken);
    }
}

