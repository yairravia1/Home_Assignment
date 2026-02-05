using HomeAssignment.Domain.Enums;
using HomeAssignment.Domain.Interfaces;
using HomeAssignment.Domain.Models;

namespace HomeAssignment.Api.IntegrationTests.TestInfrastructure;

/// <summary>
/// Prevents the app's startup seeding from scraping IMDb during integration tests.
/// Program.cs always calls app.SeedDatabase(); and the seeder checks CanConnect()
/// before scraping. Returning false makes seeding a no-op while the rest of the
/// app can still use the real Mongo repository through IActorRepository.
/// </summary>
public sealed class NoOpActorIngestionRepository : IActorIngestionRepository
{
    public bool CanConnect() => false;

    public HashSet<int> GetAssignedRanks(string source) => new();

    public HashSet<string> GetExistingExternalIds(string source, IEnumerable<string> externalIds) =>
        new(StringComparer.OrdinalIgnoreCase);

    public SaveResult SaveActors(IEnumerable<ActorRecord> actors, SaveBehavior behavior) =>
        new(Attempted: 0, Inserted: 0, Modified: 0);
}

