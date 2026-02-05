using FakeItEasy;
using HomeAssignment.Api.Controllers;
using HomeAssignment.Api.Dtos.Actor;
using HomeAssignment.Api.Queries;
using HomeAssignment.Domain.Commands;
using HomeAssignment.Domain.Interfaces;
using HomeAssignment.Domain.Models;
using HomeAssignment.Domain.Queries;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HomeAssignment.Api.Tests.Controllers;

public class ActorControllerTests
{
    private readonly IActorRepository _actorRepository;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ActorController _actorController;

    public ActorControllerTests()
    {
        _actorRepository = A.Fake<IActorRepository>();
        _messagePublisher = A.Fake<IMessagePublisher>();
        _actorController = new ActorController(_actorRepository, _messagePublisher);
    }
    
    [Theory]
    [InlineData(null, null, 0, 20)]      
    [InlineData(-5, -5, 0, 20)]           
    [InlineData(0, 1000, 0, 50)]          
    [InlineData(5, 10, 5, 10)]            
    public async Task GetAll_PaginationLogic_PassesCorrectValuesToRepo(
        int? skipInput, int? takeInput, int expectedSkip, int expectedTake)
    {
        // Arrange
        var query = new ActorQueryObject();
        A.CallTo(() => _actorRepository.GetAllActorsAsync(A<ActorQuery>._, A<int>._, A<int>._))
            .Returns(new List<Actor>()); // Return empty to ensure execution completes

        // Act
        await _actorController.GetAll(query, skipInput, takeInput);

        // Assert
        A.CallTo(() => _actorRepository.GetAllActorsAsync(
                A<ActorQuery>.That.Matches(q =>
                    q.ActorName == query.ActorName &&
                    q.MinRank == query.MinRank &&
                    q.MaxRank == query.MaxRank &&
                    q.Provider == query.Provider),
                expectedSkip,
                expectedTake))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetAll_ActorsFound_ReturnsOkWithMappedDtos()
    {
        // Arrange
        var actors = new List<Actor>
        {
            new Actor { Id = 1, Name = "Leonardo", Rank = 10 },
            new Actor { Id = 2, Name = "Brad", Rank = 9 }
        };

        A.CallTo(() => _actorRepository.GetAllActorsAsync(A<ActorQuery>._, A<int>._, A<int>._))
            .Returns(actors);

        // Act
        var result = await _actorController.GetAll(new ActorQueryObject(), 0, 20);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnActors = Assert.IsAssignableFrom<IEnumerable<ActorSummaryDto>>(okResult.Value);

        var actorDtos = returnActors.ToList();
        Assert.Equal(2, actorDtos.Count());
        // Verify mapping occurred correctly
        Assert.Contains(actorDtos, a => a.Name == "Leonardo");
    }

    [Fact]
    public async Task GetById_ActorFound_ReturnsOk()
    {
        // Arrange
        var actor = new Actor { Id = 1, Name = "Leo" };
        A.CallTo(() => _actorRepository.GetActorByIdAsync(1)).Returns(actor);

        // Act
        var result = await _actorController.GetById(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ActorDto>(okResult.Value);
        Assert.Equal("Leo", dto.Name);
    }

    [Fact]
    public async Task GetById_ActorNotFound_ReturnsNotFound()
    {
        // Arrange
        A.CallTo(() => _actorRepository.GetActorByIdAsync(1)).Returns((Actor?)null);

        // Act
        var result = await _actorController.GetById(1);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Create_ValidDto_PublishesCommandAndReturnsCorrelationId()
    {
        // Arrange
        var requestDto = new CreateActorRequestDto { Name = "Momoa", Rank = 5, Source = "IMDB" };

        // Act
        var result = await _actorController.Create(requestDto);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        
        // IMPROVEMENT: Use Reflection or dynamic to check the Anonymous Object returned
        // The controller returns: new { correlationId, message }
        var responseValue = acceptedResult.Value;
        var correlationIdProp = responseValue.GetType().GetProperty("correlationId");
        
        Assert.NotNull(correlationIdProp);
        var correlationIdValue = correlationIdProp.GetValue(responseValue) as string;
        Assert.False(string.IsNullOrEmpty(correlationIdValue));

        // Verify Publisher Call
        A.CallTo(() => _messagePublisher.PublishCommandAsync(
            A<CreateActorCommand>.That.Matches(c =>
                c.Name == "Momoa" &&
                c.CorrelationId == correlationIdValue // Ensure ID matches what was returned
            ), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Update_ValidDto_PublishesCommand()
    {
        // Arrange
        var updateDto = new UpdateActorRequestDto { Name = "Updated", Rank = 1, Source = "Wiki" };

        // Act
        var result = await _actorController.Update(10, updateDto);

        // Assert
        Assert.IsType<AcceptedResult>(result);

        A.CallTo(() => _messagePublisher.PublishCommandAsync(
            A<UpdateActorCommand>.That.Matches(c => c.ActorId == 10 && c.Name == "Updated"),
            A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Delete_ValidId_PublishesCommand()
    {
        // Act
        var result = await _actorController.Delete(55);

        // Assert
        Assert.IsType<AcceptedResult>(result);

        A.CallTo(() => _messagePublisher.PublishCommandAsync(
            A<DeleteActorCommand>.That.Matches(c => c.ActorId == 55),
            A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}