using HomeAssignment.Api.Dtos.Actor;

namespace HomeAssignment.Api.Dtos.Mappers;

public static class ActorMappers
{
    public static ActorSummaryDto ToActorSummaryDto(this Domain.Models.Actor actorModel)
    {
        return new ActorSummaryDto
        {
            Id = actorModel.Id,
            Name = actorModel.Name
        };
    }

    public static ActorDto ToActorDto(this Domain.Models.Actor actorModel)
    {
        return new ActorDto
        {
            Id = actorModel.Id,
            Name = actorModel.Name,
            Rank = actorModel.Rank,
            Source = actorModel.Source
        };
    }
}

