using System.Net;
using System.Net.Http.Json;
using HomeAssignment.Api.IntegrationTests.TestInfrastructure;
using HomeAssignment.Api.Dtos.Actor;
using HomeAssignment.Domain.Events;
using HomeAssignment.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;

namespace HomeAssignment.Api.IntegrationTests.FullFlow;

/// <summary>
/// A "must-have" integration test: it proves the full write flow works.
/// 
/// Flow under test (real system components):
/// 1) HTTP POST /api/actors
/// 2) Controller publishes CreateActorCommand to RabbitMQ (EasyNetQ PubSub)
/// 3) ActorCommandHandler consumes command and writes to MongoDB
/// 4) ActorCommandHandler publishes ActorChangedEvent(Created)
/// 5) HTTP GET /api/actors can read the created actor back from MongoDB
/// 
/// Testcontainers provides real, disposable MongoDB + RabbitMQ instances.
/// </summary>
public sealed class ActorsFullFlowTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3-management").Build();

    private FullFlowWebApplicationFactory? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        // 1) Start real dependencies (isolated per test run)
        await _mongo.StartAsync();
        await _rabbit.StartAsync();

        // 2) Boot the real API with configuration pointing to those containers
        //    EasyNetQ accepts multiple formats; we normalize to the "host=...;username=...;password=..." style.
        var easyNetQConnectionString = NormalizeEasyNetQConnectionString(_rabbit.GetConnectionString());

        _factory = new FullFlowWebApplicationFactory(
            mongoConnectionString: _mongo.GetConnectionString(),
            rabbitMqConnectionString: easyNetQConnectionString);

        _client = _factory.CreateClient();

        // 3) Wait until our event collector is actually subscribed.
        // PubSub is non-durable: if we publish before subscribing, we'd miss the event.
        var collector = _factory.Services.GetRequiredService<ActorChangedEventCollector>();
        await collector.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();

        await _rabbit.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task CreateActor_FullFlow_PersistsToMongo_PublishesEvent_AndIsReadableViaGetAll()
    {
        // Arrange
        var (factory, client) = GetInitialized();
        // Keep under 20 chars due to CreateActorRequestDto validation.
        var uniqueName = $"Momoa-{Guid.NewGuid():N}"[..14];
        var uniqueRank = await GetUnusedRankAsync(
            mongoConnectionString: _mongo.GetConnectionString(),
            databaseName: "imdb",
            collectionName: "actors",
            minRank: 1,
            maxRank: 2000);

        var request = new CreateActorRequestDto
        {
            Name = uniqueName,
            Rank = uniqueRank,
            Source = "IMDb"
        };

        // Act (HTTP write -> queue)
        var postResponse = await client.PostAsJsonAsync("/api/actors", request);

        // Immediately accepted (async processing)
        Assert.Equal(HttpStatusCode.Accepted, postResponse.StatusCode);
        
        // Assert #1: MongoDB was updated (command handler -> DB)
        var insertedActor = await WaitForActorInMongoAsync(
            mongoConnectionString: _mongo.GetConnectionString(),
            databaseName: "imdb",
            collectionName: "actors",
            actorName: request.Name,
            timeout: TimeSpan.FromSeconds(15));

        Assert.NotNull(insertedActor);
        Assert.True(insertedActor.Id > 0);
        Assert.Equal(request.Name, insertedActor.Name);
        Assert.Equal(request.Rank, insertedActor.Rank);
        Assert.Equal(request.Source, insertedActor.Source);

        // Assert #2: an event was published (queue -> event)
        var eventCollector = factory.Services.GetRequiredService<ActorChangedEventCollector>();
        var createdEvent = await eventCollector.WaitForFirstCreatedAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(ActorChangeType.Created, createdEvent.ChangeType);
        Assert.Equal(request.Name, createdEvent.Actor.Name);
        Assert.Equal(request.Rank, createdEvent.Actor.Rank);
        Assert.Equal(request.Source, createdEvent.Actor.Source);

        
        // Assert #3: the API can read it back (HTTP read -> DB)
        var getResponse = await client.GetAsync("/api/actors");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var dtos = await getResponse.Content.ReadFromJsonAsync<List<ActorSummaryDto>>();
        Assert.NotNull(dtos);
        Assert.Contains(dtos, a => a.Name == request.Name);
    }

    [Fact]
    public async Task CreateActor_DuplicateRank_DoesNotInsertSecondActor()
    {
        // Arrange
        var (_, client) = GetInitialized();
        var uniqueRank = await GetUnusedRankAsync(
            mongoConnectionString: _mongo.GetConnectionString(),
            databaseName: "imdb",
            collectionName: "actors",
            minRank: 1,
            maxRank: 2000);

        var firstRequest = new CreateActorRequestDto
        {
            Name = $"Momoa-{Guid.NewGuid():N}"[..14],
            Rank = uniqueRank,
            Source = "IMDb"
        };

        var secondRequest = new CreateActorRequestDto
        {
            Name = $"Keanu-{Guid.NewGuid():N}"[..14],
            Rank = uniqueRank, // same rank on purpose
            Source = "IMDb"
        };

        // Act
        var firstResponse = await client.PostAsJsonAsync("/api/actors", firstRequest);
        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);

        var firstActor = await WaitForActorInMongoAsync(
            mongoConnectionString: _mongo.GetConnectionString(),
            databaseName: "imdb",
            collectionName: "actors",
            actorName: firstRequest.Name,
            timeout: TimeSpan.FromSeconds(15));
        Assert.NotNull(firstActor);

        var secondResponse = await client.PostAsJsonAsync("/api/actors", secondRequest);
        Assert.Equal(HttpStatusCode.Accepted, secondResponse.StatusCode);

        // Assert: rank remains unique in the database.
        var count = await WaitForActorCountByRankAsync(
            mongoConnectionString: _mongo.GetConnectionString(),
            databaseName: "imdb",
            collectionName: "actors",
            rank: uniqueRank,
            timeout: TimeSpan.FromSeconds(10));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UpdateActor_NotFound_DoesNotModifyExistingActor()
    {
        // Arrange
        var (_, client) = GetInitialized();
        var uniqueRank = await GetUnusedRankAsync(
            mongoConnectionString: _mongo.GetConnectionString(),
            databaseName: "imdb",
            collectionName: "actors",
            minRank: 1,
            maxRank: 2000);

        var createRequest = new CreateActorRequestDto
        {
            Name = $"Momoa-{Guid.NewGuid():N}"[..14],
            Rank = uniqueRank,
            Source = "IMDb"
        };

        var createResponse = await client.PostAsJsonAsync("/api/actors", createRequest);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        var insertedActor = await WaitForActorInMongoAsync(
            mongoConnectionString: _mongo.GetConnectionString(),
            databaseName: "imdb",
            collectionName: "actors",
            actorName: createRequest.Name,
            timeout: TimeSpan.FromSeconds(15));
        Assert.NotNull(insertedActor);

        var missingId = insertedActor!.Id + 10000;
        var updateRequest = new UpdateActorRequestDto
        {
            Name = "SHOULD_NOT_APPLY",
            Rank = uniqueRank,
            Source = "IMDb"
        };

        // Act
        var updateResponse = await client.PutAsJsonAsync($"/api/actors/{missingId}", updateRequest);
        Assert.Equal(HttpStatusCode.Accepted, updateResponse.StatusCode);

        // Assert: original actor stays unchanged.
        await Task.Delay(500);
        var refreshed = await GetActorByIdAsync(
            mongoConnectionString: _mongo.GetConnectionString(),
            databaseName: "imdb",
            collectionName: "actors",
            actorId: insertedActor.Id);

        Assert.NotNull(refreshed);
        Assert.Equal(createRequest.Name, refreshed!.Name);
    }

    private (FullFlowWebApplicationFactory Factory, HttpClient Client) GetInitialized()
    {
        if (_factory is null || _client is null)
        {
            throw new InvalidOperationException("Test was not initialized. InitializeAsync must run first.");
        }

        return (_factory, _client);
    }

    private static async Task<Actor?> WaitForActorInMongoAsync(
        string mongoConnectionString,
        string databaseName,
        string collectionName,
        string actorName,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        var client = new MongoClient(mongoConnectionString);
        var collection = client.GetDatabase(databaseName).GetCollection<Actor>(collectionName);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var actor = await collection.Find(a => a.Name == actorName).FirstOrDefaultAsync();
            if (actor != null)
            {
                return actor;
            }

            await Task.Delay(250);
        }

        return null;
    }

    private static async Task<int> GetUnusedRankAsync(
        string mongoConnectionString,
        string databaseName,
        string collectionName,
        int minRank,
        int maxRank)
    {
        var client = new MongoClient(mongoConnectionString);
        var collection = client.GetDatabase(databaseName).GetCollection<Actor>(collectionName);

        var usedRanks = await collection
            .Distinct(a => a.Rank, Builders<Actor>.Filter.Gte(a => a.Rank, minRank))
            .ToListAsync();

        var used = usedRanks.ToHashSet();
        for (var candidate = maxRank; candidate >= minRank; candidate--)
        {
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No unused rank available in the requested range.");
    }

    private static async Task<int> WaitForActorCountByRankAsync(
        string mongoConnectionString,
        string databaseName,
        string collectionName,
        int rank,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        var client = new MongoClient(mongoConnectionString);
        var collection = client.GetDatabase(databaseName).GetCollection<Actor>(collectionName);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var count = await collection.CountDocumentsAsync(actor => actor.Rank == rank);
            if (count >= 1)
            {
                return (int)count;
            }

            await Task.Delay(250);
        }

        return 0;
    }

    private static async Task<Actor?> GetActorByIdAsync(
        string mongoConnectionString,
        string databaseName,
        string collectionName,
        int actorId)
    {
        var client = new MongoClient(mongoConnectionString);
        var collection = client.GetDatabase(databaseName).GetCollection<Actor>(collectionName);
        return await collection.Find(actor => actor.Id == actorId).FirstOrDefaultAsync();
    }
    
    private static string NormalizeEasyNetQConnectionString(string rabbitMqContainerConnectionString)
    {
        // Testcontainers RabbitMQ module returns an AMQP URI (e.g. amqp://guest:guest@localhost:32768).
        // EasyNetQ can work with AMQP URIs, but its most common format is:
        // "host=localhost:32768;username=guest;password=guest".
        if (!Uri.TryCreate(rabbitMqContainerConnectionString, UriKind.Absolute, out var uri))
        {
            return rabbitMqContainerConnectionString;
        }

        var username = "guest";
        var password = "guest";

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                username = parts[0];
            }
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                password = parts[1];
            }
        }

        return $"host={uri.Host}:{uri.Port};username={username};password={password}";
    }
}

