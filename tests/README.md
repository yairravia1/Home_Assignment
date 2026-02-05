# Test Suite

## Structure

```
tests/
└── HomeAssignment.Api.UnitTests/       # API layer unit tests
    └── Controllers/
        └── ActorControllerTests.cs
```

## Quick Start

### Run all tests:
```bash
dotnet test
```

### Run with detailed output:
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run specific test:
```bash
dotnet test --filter "GetAll_WithNoMatchingActors_ReturnsNotFound"
```

---

## Integration tests

Integration tests live in `HomeAssignment.Api.IntegrationTests/` (solution root).

- They use **Testcontainers**, so **Docker is required** to run them.

---

## Current Test Coverage

### ActorControllerTests - 14 Tests Total

**GetAll endpoint** (4 tests):
- ✅ `GetAll_WithMatchingActors_ReturnsOkWithActorList` - Happy path
- ✅ `GetAll_WithNoMatchingActors_ReturnsNotFound` - Sad path (empty)
- ✅ `GetAll_WithNullResult_ReturnsNotFound` - Null safety
- ✅ `GetAll_WithVariousPagination_NormalizesProperly` - Pagination validation

**GetById endpoint** (3 tests):
- ✅ `GetById_WithExistingActor_ReturnsOkWithFullDetails` - Happy path
- ✅ `GetById_WithNonExistentActor_ReturnsNotFound` - Sad path (not found)
- ✅ `GetById_WithNegativeId_ReturnsBadRequest` - Validation

**Create endpoint** (3 tests):
- ✅ `Create_WithValidData_ReturnsAcceptedWithCorrelationId` - Happy path
- ✅ `Create_PublishesCommand_DoesNotCallRepository` - Async verification
- ✅ `Create_WhenPublisherFails_PropagatesException` - Error handling

**Update endpoint** (2 tests):
- ✅ `Update_WithValidData_ReturnsAcceptedWithCorrelationId` - Happy path
- ✅ `Update_PublishesCommand_DoesNotCallRepository` - Async verification

**Delete endpoint** (2 tests):
- ✅ `Delete_WithValidId_ReturnsAcceptedWithCorrelationId` - Happy path
- ✅ `Delete_PublishesCommand_DoesNotCallRepository` - Async verification

**Coverage:** All endpoints with happy, sad, and critical edge cases

---

## Test Principles

### AAA Pattern
```
Arrange → Act → Assert
```

### Naming Convention
```
[Method]_[Scenario]_[Expected]
```

### Tools
- **xUnit** - Test framework
- **FakeItEasy** - Mocking framework

---

## Adding More Tests

To test other endpoints, follow the pattern in `ActorControllerTests.cs`:

1. Create test fixtures (sample data)
2. Create fakes (IActorRepository, IMessagePublisher)
3. Write tests using AAA pattern
4. Use standard xUnit assertions
5. Verify fake interactions with FakeItEasy

**See the existing tests for patterns and naming conventions.**
