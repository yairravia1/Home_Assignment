using HomeAssignment.Domain.Interfaces;
using HomeAssignment.Domain.Enums;
using HomeAssignment.Domain.Models;
using HomeAssignment.Domain.Queries;
using HomeAssignment.Infrastructure.Configuration.Settings.MongoSettings;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HomeAssignment.Infrastructure.Database.Repository;

public class MongoActorRepository : IActorRepository, IActorIngestionRepository
{
    private const string CounterCollectionName = "counters";
    private const string CounterId = "actors";

    private readonly IMongoCollection<Actor> _actors;
    private readonly IMongoCollection<BsonDocument> _counterCollection;

    public MongoActorRepository(MongoSettings settings, IMongoClient mongoClient)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new ArgumentException("Mongo connection string is required.", nameof(settings));
        }

        var database = mongoClient.GetDatabase(settings.DatabaseName);
        _actors = database.GetCollection<Actor>(settings.CollectionName);
        _counterCollection = database.GetCollection<BsonDocument>(CounterCollectionName);

        EnsureProviderAwareIndexes();
    }

    private void EnsureProviderAwareIndexes()
    {
        // We previously had a unique index on Rank only. That breaks multi-provider storage in one collection.
        // Here we drop that legacy index (if present) and enforce provider-aware uniqueness instead.
        try
        {
            var existing = _actors.Indexes.List().ToList();
            foreach (var index in existing)
            {
                var isUnique = index.TryGetValue("unique", out var uniqueValue) && uniqueValue.IsBoolean &&
                               uniqueValue.AsBoolean;

                if (!isUnique)
                {
                    continue;
                }

                if (!index.TryGetValue("key", out var keyValue) || !keyValue.IsBsonDocument)
                {
                    continue;
                }

                var key = keyValue.AsBsonDocument;
                var isLegacyRankOnly = key.ElementCount == 1 && key.Contains("Rank");
                if (!isLegacyRankOnly)
                {
                    continue;
                }

                if (index.TryGetValue("name", out var nameValue) && nameValue.IsString)
                {
                    _actors.Indexes.DropOne(nameValue.AsString);
                }
            }
        }
        catch (Exception ex)
        {
            // Index maintenance should never crash the app. Worst case: Mongo will enforce whatever indexes exist.
            Console.WriteLine($"Index maintenance warning: {ex.Message}");
        }

        var sourceRankIndex = new CreateIndexModel<Actor>(
            Builders<Actor>.IndexKeys
                .Ascending(actor => actor.Source)
                .Ascending(actor => actor.Rank),
            new CreateIndexOptions { Unique = true });

        var sourceExternalIdIndex = new CreateIndexModel<Actor>(
            Builders<Actor>.IndexKeys
                .Ascending(actor => actor.Source)
                .Ascending(actor => actor.ExternalId),
            new CreateIndexOptions { Unique = true, Sparse = true });

        _actors.Indexes.CreateOne(sourceRankIndex);
        _actors.Indexes.CreateOne(sourceExternalIdIndex);
    }

    public bool CanConnect()
    {
        try
        {
            _actors.Database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MongoDB connection failed: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Actor>> GetAllActorsAsync(ActorQuery query, int skip, int take)
    {
        var filter = BuildFilter(query);

        return await _actors.Find(filter)
            .SortBy(actorDocument => actorDocument.Id)
            .Skip(skip)
            .Limit(take)
            .ToListAsync();
    }

    public async Task<Actor?> GetActorByIdAsync(int id)
    {
        return await _actors.Find(actorDocument => actorDocument.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Actor?> DeleteActorAsync(int id)
    {
        return await _actors.FindOneAndDeleteAsync(actorDocument => actorDocument.Id == id);
    }

    public async Task<AddActorResult> AddActorAsync(Actor actor)
    {
        var duplicateRank = await IsRankInUseAsync(actor.Source, actor.Rank, 0);
        if (duplicateRank)
        {
            return new AddActorResult { DuplicateRank = true };
        }

        if (actor.Id <= 0)
        {
            actor.Id = GetNextIds(1).First();
        }

        await _actors.InsertOneAsync(actor);
        return new AddActorResult { Actor = actor };
    }

    public async Task<UpdateActorResult> UpdateActorAsync(int id, ActorUpdate update)
    {
        var existingActor = await _actors.Find(actorDocument => actorDocument.Id == id).FirstOrDefaultAsync();

        if (existingActor == null)
        {
            return new UpdateActorResult { NotFound = true };
        }

        var duplicateRank = await IsRankInUseAsync(update.Source, update.Rank, excludedActorId: id);
        if (duplicateRank)
        {
            return new UpdateActorResult { DuplicateRank = true };
        }

        existingActor.Name = update.Name;
        existingActor.Rank = update.Rank;
        existingActor.Source = update.Source;

        var options = new FindOneAndReplaceOptions<Actor>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updatedActor = await _actors.FindOneAndReplaceAsync(
            actorDocument => actorDocument.Id == existingActor.Id,
            existingActor,
            options);

        return new UpdateActorResult { Actor = updatedActor };
    }

    public HashSet<int> GetAssignedRanks(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return new HashSet<int>();
        }

        var filter = Builders<Actor>.Filter.And(
            Builders<Actor>.Filter.Eq(a => a.Source, source),
            Builders<Actor>.Filter.Gt(a => a.Rank, 0));

        var ranks = _actors
            .Distinct(actor => actor.Rank, filter)
            .ToList();

        return ranks.ToHashSet();
    }

    public HashSet<string> GetExistingExternalIds(string source, IEnumerable<string> externalIds)
    {
        var ids = externalIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList()
            ?? new List<string>();

        if (string.IsNullOrWhiteSpace(source) || ids.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var filter = Builders<Actor>.Filter.And(
            Builders<Actor>.Filter.Eq(actor => actor.Source, source),
            Builders<Actor>.Filter.In(actor => actor.ExternalId, ids));

        var existing = _actors
            .Find(filter)
            .Project(actor => actor.ExternalId)
            .ToList();

        return existing
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public SaveResult SaveActors(IEnumerable<ActorRecord> actors, SaveBehavior behavior)
    {
        var actorList = actors?.ToList() ?? new List<ActorRecord>();
        if (actorList.Count == 0)
        {
            return new SaveResult(0, 0, 0);
        }

        var actorDocuments = actorList
            .Where(actor => !string.IsNullOrWhiteSpace(actor.ExternalId))
            .GroupBy(actor => actor.ExternalId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var names = group.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                var rank = group.Select(a => a.Rank).FirstOrDefault(r => r.HasValue);
                var source = group.Select(a => a.Source).LastOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "IMDb";

                return new Actor
                {
                    ExternalId = group.Key,
                    Name = names.LastOrDefault() ?? "",
                    Rank = rank ?? 0,
                    Source = source
                };
            })
            .ToList();

        var existingBySource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var providerSource in actorDocuments
                     .Select(actorDocument => actorDocument.Source)
                     .Where(source => !string.IsNullOrWhiteSpace(source))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var externalIdsForSource = actorDocuments
                .Where(actorDocument => actorDocument.Source.Equals(providerSource, StringComparison.OrdinalIgnoreCase))
                .Select(actorDocument => actorDocument.ExternalId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToList();

            foreach (var existingId in GetExistingExternalIds(providerSource, externalIdsForSource))
            {
                existingBySource.Add($"{providerSource}::{existingId}");
            }
        }

        var actorsToInsert = actorDocuments
            .Where(actorDocument => !existingBySource.Contains($"{actorDocument.Source}::{actorDocument.ExternalId}"))
            .ToList();
        var insertedCount = InsertDocuments(actorsToInsert);

        if (behavior == SaveBehavior.SkipExisting)
        {
            return new SaveResult(actorDocuments.Count, insertedCount, 0);
        }

        var actorsToUpdate = actorDocuments
            .Where(actorDocument => existingBySource.Contains($"{actorDocument.Source}::{actorDocument.ExternalId}"))
            .ToList();
        var modifiedCount = UpdateExistingDocuments(actorsToUpdate);
        return new SaveResult(actorDocuments.Count, insertedCount, modifiedCount);
    }

    private int UpdateExistingDocuments(List<Actor> actorsToUpdate)
    {
        if (actorsToUpdate.Count == 0)
        {
            return 0;
        }

        var updates = actorsToUpdate.Select(actorDocument =>
        {
            var filter = Builders<Actor>.Filter.And(
                Builders<Actor>.Filter.Eq(actor => actor.Source, actorDocument.Source),
                Builders<Actor>.Filter.Eq(actor => actor.ExternalId, actorDocument.ExternalId));

            var update = Builders<Actor>.Update
                .Set(actor => actor.Name, actorDocument.Name)
                .Set(actor => actor.Rank, actorDocument.Rank)
                .Set(actor => actor.Source, actorDocument.Source);

            return new UpdateOneModel<Actor>(filter, update);
        }).ToList();

        var result = ExecuteWithRetry(() => _actors.BulkWrite(updates, new BulkWriteOptions { IsOrdered = false }));
        return result?.ModifiedCount != null ? (int)result.ModifiedCount : 0;
    }

    private int InsertDocuments(List<Actor> actorsToInsert)
    {
        if (actorsToInsert.Count == 0)
        {
            return 0;
        }

        var reservedIds = GetNextIds(actorsToInsert.Count);
        for (var i = 0; i < actorsToInsert.Count; i++)
        {
            actorsToInsert[i].Id = reservedIds[i];
        }

        try
        {
            _actors.InsertMany(actorsToInsert);
            return actorsToInsert.Count;
        }
        catch (MongoConnectionException ex)
        {
            Console.WriteLine($"MongoDB connection dropped: {ex.Message}. Retrying...");
            Thread.Sleep(1000);
            _actors.InsertMany(actorsToInsert);
            return actorsToInsert.Count;
        }
    }

    private List<int> GetNextIds(int count)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", CounterId);
        var update = Builders<BsonDocument>.Update.Inc("seq", count);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var result = _counterCollection.FindOneAndUpdate(filter, update, options);
        var maxId = result.GetValue("seq", 0).ToInt32();
        var startId = maxId - count + 1;
        return Enumerable.Range(startId, count).ToList();
    }

    private static FilterDefinition<Actor> BuildFilter(ActorQuery query)
    {
        var filterBuilder = Builders<Actor>.Filter;
        var filters = new List<FilterDefinition<Actor>>();

        if (!string.IsNullOrWhiteSpace(query.ActorName))
        {
            filters.Add(filterBuilder.Regex(
                actorDocument => actorDocument.Name,
                new BsonRegularExpression(query.ActorName, "i")));
        }

        if (query.MinRank.HasValue)
        {
            filters.Add(filterBuilder.Gte(actorDocument => actorDocument.Rank, query.MinRank.Value));
        }

        if (query.MaxRank.HasValue)
        {
            filters.Add(filterBuilder.Lte(actorDocument => actorDocument.Rank, query.MaxRank.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            filters.Add(filterBuilder.Regex(
                actorDocument => actorDocument.Source,
                new BsonRegularExpression(query.Provider, "i")));
        }

        return filters.Count == 0
            ? FilterDefinition<Actor>.Empty
            : filterBuilder.And(filters);
    }

    private async Task<bool> IsRankInUseAsync(string source, int rank, int excludedActorId)
    {
        var filterBuilder = Builders<Actor>.Filter;
        var sourceFilter = filterBuilder.Eq(actorDocument => actorDocument.Source, source);
        var rankFilter = filterBuilder.Eq(actorDocument => actorDocument.Rank, rank);
        var excludeFilter = filterBuilder.Ne(actorDocument => actorDocument.Id, excludedActorId);
        var combinedFilter = filterBuilder.And(sourceFilter, rankFilter, excludeFilter);

        var count = await _actors.CountDocumentsAsync(combinedFilter);
        return count > 0;
    }

    private static BulkWriteResult<Actor>? ExecuteWithRetry(Func<BulkWriteResult<Actor>> action)
    {
        try
        {
            return action();
        }
        catch (MongoConnectionException ex)
        {
            Console.WriteLine($"MongoDB connection dropped: {ex.Message}. Retrying...");
            Thread.Sleep(1000);
            return action();
        }
    }
}
