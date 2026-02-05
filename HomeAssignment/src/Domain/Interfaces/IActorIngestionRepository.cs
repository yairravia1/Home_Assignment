using HomeAssignment.Domain.Models;
using HomeAssignment.Domain.Enums;

namespace HomeAssignment.Domain.Interfaces;

public interface IActorIngestionRepository
{ 
    bool CanConnect();
    HashSet<int> GetAssignedRanks(string source);
    HashSet<string> GetExistingExternalIds(string source, IEnumerable<string> externalIds);
    SaveResult SaveActors(IEnumerable<ActorRecord> actors, SaveBehavior behavior);
}
