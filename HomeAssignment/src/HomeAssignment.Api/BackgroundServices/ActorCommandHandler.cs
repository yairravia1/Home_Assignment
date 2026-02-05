using EasyNetQ;
using HomeAssignment.Domain.Commands;
using HomeAssignment.Domain.Events;
using HomeAssignment.Domain.Interfaces;
using HomeAssignment.Domain.Models;

namespace HomeAssignment.Api.BackgroundServices;

public class ActorCommandHandler : IHostedService
{
    private readonly IBus _bus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ActorCommandHandler> _logger;

    public ActorCommandHandler(
        IBus bus,
        IServiceProvider serviceProvider,
        ILogger<ActorCommandHandler> logger)
    {
        _bus = bus;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SubscribeToCreateCommands(cancellationToken);
        await SubscribeToUpdateCommands(cancellationToken);
        await SubscribeToDeleteCommands(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SubscribeToCreateCommands(CancellationToken cancellationToken)
    {
        await _bus.PubSub.SubscribeAsync<CreateActorCommand>(
            subscriptionId: "actor.commands.create",
            onMessage: async (CreateActorCommand command) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IActorRepository>();
                var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

                try
                {
                    var actor = new Actor
                    {
                        Name = command.Name,
                        Rank = command.Rank,
                        Source = command.Source
                    };

                    var addResult = await repository.AddActorAsync(actor);
                    if (addResult.DuplicateRank)
                    {
                        _logger.LogWarning(
                            "Create command rejected: Duplicate rank {Rank}. CorrelationId={CorrelationId}",
                            command.Rank,
                            command.CorrelationId);
                        return;
                    }

                    if (addResult.Actor == null)
                    {
                        return;
                    }

                    var evt = new ActorChangedEvent(
                        addResult.Actor.Id,
                        ActorChangeType.Created,
                        new ActorSnapshot(
                            addResult.Actor.Id,
                            addResult.Actor.Name,
                            addResult.Actor.Rank,
                            addResult.Actor.Source),
                        DateTimeOffset.UtcNow);

                    await publisher.PublishEventAsync(evt, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process CreateActorCommand. CorrelationId={CorrelationId}",
                        command.CorrelationId);
                }
            },
            cancellationToken: cancellationToken);
    }

    private async Task SubscribeToUpdateCommands(CancellationToken cancellationToken)
    {
        await _bus.PubSub.SubscribeAsync<UpdateActorCommand>(
            subscriptionId: "actor.commands.update",
            onMessage: async (UpdateActorCommand command) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IActorRepository>();
                var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

                try
                {
                    var update = new ActorUpdate(
                        Name: command.Name,
                        Rank: command.Rank,
                        Source: command.Source);

                    var updateResult = await repository.UpdateActorAsync(command.ActorId, update);
                    if (updateResult.NotFound || updateResult.DuplicateRank || updateResult.Actor == null)
                    {
                        return;
                    }

                    var evt = new ActorChangedEvent(
                        updateResult.Actor.Id,
                        ActorChangeType.Updated,
                        new ActorSnapshot(
                            updateResult.Actor.Id,
                            updateResult.Actor.Name,
                            updateResult.Actor.Rank,
                            updateResult.Actor.Source),
                        DateTimeOffset.UtcNow);

                    await publisher.PublishEventAsync(evt, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process UpdateActorCommand. ActorId={ActorId} CorrelationId={CorrelationId}",
                        command.ActorId,
                        command.CorrelationId);
                }
            },
            cancellationToken: cancellationToken);
    }

    private async Task SubscribeToDeleteCommands(CancellationToken cancellationToken)
    {
        await _bus.PubSub.SubscribeAsync<DeleteActorCommand>(
            subscriptionId: "actor.commands.delete",
            onMessage: async (DeleteActorCommand command) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IActorRepository>();
                var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

                try
                {
                    var deletedActor = await repository.DeleteActorAsync(command.ActorId);
                    if (deletedActor == null)
                    {
                        return;
                    }

                    var evt = new ActorChangedEvent(
                        deletedActor.Id,
                        ActorChangeType.Deleted,
                        new ActorSnapshot(
                            deletedActor.Id,
                            deletedActor.Name,
                            deletedActor.Rank,
                            deletedActor.Source),
                        DateTimeOffset.UtcNow);

                    await publisher.PublishEventAsync(evt, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process DeleteActorCommand. ActorId={ActorId} CorrelationId={CorrelationId}",
                        command.ActorId,
                        command.CorrelationId);
                }
            },
            cancellationToken: cancellationToken);
    }
}

