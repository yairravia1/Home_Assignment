using EasyNetQ;
using HomeAssignment.Domain.Events;
using HomeAssignment.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace HomeAssignment.Infrastructure.Messaging;

public class EasyNetQProducer : IMessagePublisher
{
    private readonly IBus _bus;
    private readonly ILogger<EasyNetQProducer> _logger;

    public EasyNetQProducer(IBus bus, ILogger<EasyNetQProducer> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public async Task PublishEventAsync(
        ActorChangedEvent actorEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _bus.PubSub.PublishAsync(actorEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish actor event for ID {ActorId}", actorEvent.ActorId);
        }
    }

    public async Task PublishCommandAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default) where TCommand : class
    {
        try
        {
            await _bus.PubSub.PublishAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish command of type {CommandType}", typeof(TCommand).Name);
            throw;
        }
    }
}
