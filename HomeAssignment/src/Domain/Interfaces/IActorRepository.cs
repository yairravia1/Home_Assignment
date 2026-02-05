using HomeAssignment.Domain.Models;
using HomeAssignment.Domain.Queries;

namespace HomeAssignment.Domain.Interfaces;

public interface IActorRepository
{
    Task<List<Actor>> GetAllActorsAsync(ActorQuery query, int skip, int take);
    Task<Actor?> GetActorByIdAsync(int id);
    Task<Actor?> DeleteActorAsync(int id);
    Task<AddActorResult> AddActorAsync(Actor actor);
    Task<UpdateActorResult> UpdateActorAsync(int id, ActorUpdate update);
}
