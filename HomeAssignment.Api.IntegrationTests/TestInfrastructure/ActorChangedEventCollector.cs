using System.Collections.Concurrent;
using EasyNetQ;
using HomeAssignment.Domain.Events;
using Microsoft.Extensions.Hosting;

namespace HomeAssignment.Api.IntegrationTests.TestInfrastructure;

/// <summary>
/// Captures ActorChangedEvent messages published through EasyNetQ.
/// This lets integration tests assert the system really published an event,
/// not just that MongoDB was updated.
/// </summary>
public sealed class ActorChangedEventCollector : IHostedService
{
    private readonly IBus _bus;
    private readonly ConcurrentQueue<ActorChangedEvent> _events = new();
    private readonly TaskCompletionSource<ActorChangedEvent> _firstCreatedEvent =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private string? _subscriptionId;

    public ActorChangedEventCollector(IBus bus)
    {
        _bus = bus;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Unique subscription id to avoid collisions when tests run in parallel.
        _subscriptionId = $"test.actor.events.{Guid.NewGuid():N}";

        await _bus.PubSub.SubscribeAsync<ActorChangedEvent>(
            subscriptionId: _subscriptionId,
            onMessage: message =>
            {
                _events.Enqueue(message);
                if (message.ChangeType == ActorChangeType.Created)
                {
                    _firstCreatedEvent.TrySetResult(message);
                }
            },
            cancellationToken: cancellationToken);

        // SubscribeAsync returning means the queue is declared and bound.
        // Messages published after this point should be delivered.
        _ready.TrySetResult();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task WaitUntilReadyAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var completed = await Task.WhenAny(_ready.Task, Task.Delay(Timeout.Infinite, cts.Token));
        if (completed != _ready.Task)
        {
            throw new TimeoutException("Timed out waiting for event collector subscription to become ready.");
        }
    }

    public async Task<ActorChangedEvent> WaitForFirstCreatedAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        var completed = await Task.WhenAny(_firstCreatedEvent.Task, Task.Delay(Timeout.Infinite, cts.Token));
        if (completed != _firstCreatedEvent.Task)
        {
            throw new TimeoutException(
                $"Timed out waiting for {nameof(ActorChangedEvent)}(Created). " +
                $"CapturedEvents={_events.Count} SubscriptionId={_subscriptionId ?? "unknown"}");
        }

        return await _firstCreatedEvent.Task;
    }
}

