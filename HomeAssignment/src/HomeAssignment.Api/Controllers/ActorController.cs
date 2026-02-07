using HomeAssignment.Api.Dtos.Actor;
using HomeAssignment.Api.Dtos.Mappers;
using HomeAssignment.Api.Queries;
using HomeAssignment.Domain.Commands;
using HomeAssignment.Domain.Interfaces;
using HomeAssignment.Domain.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeAssignment.Api.Controllers;

[Route("api/actors")]
[ApiController]
public class ActorController : ControllerBase
{
    private const int DefaultSkip = 0;
    private const int DefaultTake = 20;
    private const int MaxTake = 50;

    private readonly IActorRepository _actorRepository;
    private readonly IMessagePublisher _messagePublisher;

    public ActorController(IActorRepository actorRepository, IMessagePublisher messagePublisher)
    {
        _actorRepository = actorRepository;
        _messagePublisher = messagePublisher;
    }

    [HttpGet]
    [Authorize(Policy = "UserAccess")]
    public async Task<IActionResult> GetAll(
        [FromQuery] ActorQueryObject queryObject,
        [FromHeader] int? skip,
        [FromHeader] int? take)
    {
        var actualSkip = Math.Max(0, skip.GetValueOrDefault(DefaultSkip));
        var requestedTake = take.GetValueOrDefault(DefaultTake);
        var actualTake = requestedTake <= 0 ? DefaultTake : Math.Min(requestedTake, MaxTake);

        var actorQuery = new ActorQuery
        {
            ActorName = queryObject.ActorName,
            MinRank = queryObject.MinRank,
            MaxRank = queryObject.MaxRank,
            Provider = queryObject.Provider
        };

        var actorList = await _actorRepository.GetAllActorsAsync(actorQuery, actualSkip, actualTake);
        var actorSummaries = actorList.Select(actorItem => actorItem.ToActorSummaryDto());

        return Ok(actorSummaries);
    }

    [HttpGet("{id:int:min(1)}")]
    [Authorize(Policy = "UserAccess")]
    public async Task<IActionResult> GetById([FromRoute] int id)
    {
        var actor = await _actorRepository.GetActorByIdAsync(id);

        if (actor == null)
        {
            return NotFound($"No actor found with id {id}");
        }

        return Ok(actor.ToActorDto());
    }

    [HttpPost]
    [Authorize(Policy = "AdminAccess")]
    public async Task<IActionResult> Create([FromBody] Dtos.Actor.CreateActorRequestDto request)
    {
        var correlationId = Guid.NewGuid().ToString();
        var command = new CreateActorCommand(
            request.Name,
            request.Rank,
            request.Source,
            correlationId);

        await _messagePublisher.PublishCommandAsync(command);
        return Accepted(new { correlationId, message = "Actor creation request accepted." });
    }

    [HttpPut("{id:int:min(1)}")]
    [Authorize(Policy = "AdminAccess")]
    public async Task<IActionResult> Update(
        [FromRoute] int id,
        [FromBody] Dtos.Actor.UpdateActorRequestDto updateDto)
    {
        var correlationId = Guid.NewGuid().ToString();
        var command = new UpdateActorCommand(
            id,
            updateDto.Name,
            updateDto.Rank,
            updateDto.Source,
            correlationId);

        await _messagePublisher.PublishCommandAsync(command);
        return Accepted(new { correlationId, message = "Actor update request accepted." });
    }

    [HttpDelete("{id:int:min(1)}")]
    [Authorize(Policy = "AdminAccess")]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        var correlationId = Guid.NewGuid().ToString();
        var command = new DeleteActorCommand(id, correlationId);

        await _messagePublisher.PublishCommandAsync(command);
        return Accepted(new { correlationId, message = "Actor deletion request accepted." });
    }
}

