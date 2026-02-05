# Async Command-Driven API with Message Queue

## Architecture Overview

Your API now follows the **Command-Query Responsibility Segregation (CQRS)** pattern:

- **Queries (GET):** Synchronous, direct database reads
- **Commands (POST/PUT/DELETE):** Asynchronous via message queue

---

## Flow Diagram

```
READ OPERATIONS (Synchronous):
┌────────┐     GET      ┌─────────────┐    Query    ┌─────────┐
│ Client │ ──────────→ │ Controller  │ ─────────→ │ MongoDB │
└────────┘              └─────────────┘              └─────────┘
              ←─────────                   ←─────────
            200 OK + Data                   Results

═══════════════════════════════════════════════════════════════

WRITE OPERATIONS (Asynchronous):
┌────────┐  POST/PUT/DELETE  ┌─────────────┐  Command  ┌──────────┐
│ Client │ ──────────────→  │ Controller  │ ────────→ │ RabbitMQ │
└────────┘                    └─────────────┘            └────┬─────┘
       ↑                                                      │
       │ 202 Accepted + CorrelationId                        │
       │                                                      ↓
                                                    ┌──────────────────┐
                                                    │ Command Handler  │
                                                    │ (Background)     │
                                                    └────┬─────────────┘
                                                         │
                                            Execute     │     Success?
                                                         ↓
                                                    ┌─────────┐
                                                    │ MongoDB │
                                                    └────┬────┘
                                                         │
                                                         ↓
                                                    ┌──────────┐
                                                    │ RabbitMQ │
                                                    │ (Event)  │
                                                    └────┬─────┘
                                                         │
                                                         ↓
                                                 ┌───────────────┐
                                                 │ Event Logger  │
                                                 │ (logs event)  │
                                                 └───────────────┘
```

---

## API Endpoints Explained

### 1. GET /api/actors (Synchronous)

**Request:**
```http
GET /api/actors?ActorName=Morgan&MinRank=1&MaxRank=100
skip: 0
take: 20
```

**Response (200 OK):**
```json
[
  { "id": 1, "name": "Morgan Freeman" },
  { "id": 5, "name": "Morgan Fairchild" }
]
```

**Flow:**
```
Client → Controller → MongoDB → Response (immediate)
```

**No messaging involved** - direct database read.

---

### 2. GET /api/actors/{id} (Synchronous)

**Request:**
```http
GET /api/actors/123
```

**Response (200 OK):**
```json
{
  "id": 123,
  "name": "John Doe",
  "rank": 999,
  "source": "Manual"
}
```

**Flow:**
```
Client → Controller → MongoDB → Response (immediate)
```

**No messaging involved** - direct database read.

---

### 3. POST /api/actors (Asynchronous via Queue)

**Request:**
```http
POST /api/actors
Content-Type: application/json

{
  "name": "John Doe",
  "rank": 999,
  "source": "Manual"
}
```

**Response (202 Accepted):**
```json
{
  "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "message": "Actor creation request accepted and queued for processing."
}
```

**What Happens:**

**Step 1: Controller publishes command** (Lines 53-65)
```csharp
var correlationId = Guid.NewGuid().ToString();  // Unique ID to track this request

var command = new CreateActorCommand(
    actorDto.Name,
    actorDto.Rank,
    actorDto.Source,
    correlationId);

await _messagePublisher.PublishCommandAsync(command);  // Send to RabbitMQ
return Accepted(...);  // Return immediately
```

**Step 2: RabbitMQ receives command**
- Command stored in queue: `actor.commands.create`
- Command is JSON: `{"name":"John Doe","rank":999,"source":"Manual","correlationId":"..."}`

**Step 3: Command handler processes** (Background)
```csharp
await _bus.PubSub.SubscribeAsync<CreateActorCommand>(
    subscriptionId: "actor.commands.create",
    onMessage: async command =>
    {
        // 1. Validate rank
        var addResult = await repository.AddActorAsync(actor);
        
        // 2. If successful, publish event
        if (addResult.Actor != null)
        {
            await publisher.PublishEventAsync(new ActorChangedEvent(...));
        }
    });
```

**Step 4: Event published after successful creation**
- Event goes to queue: `actor.events.log`
- Event logger worker logs it

---

### 4. PUT /api/actors/{id} (Asynchronous via Queue)

**Request:**
```http
PUT /api/actors/123
Content-Type: application/json

{
  "name": "Updated Name",
  "rank": 500,
  "source": "IMDb"
}
```

**Response (202 Accepted):**
```json
{
  "correlationId": "xyz-789-...",
  "message": "Actor update request accepted and queued for processing."
}
```

**Same async flow** as POST, but:
- Command type: `UpdateActorCommand`
- Queue: `actor.commands.update`
- Event type: `ActorChangeType.Updated`

---

### 5. DELETE /api/actors/{id} (Asynchronous via Queue)

**Request:**
```http
DELETE /api/actors/123
```

**Response (202 Accepted):**
```json
{
  "correlationId": "def-456-...",
  "message": "Actor deletion request accepted and queued for processing."
}
```

**Same async flow:**
- Command type: `DeleteActorCommand`
- Queue: `actor.commands.delete`
- Event type: `ActorChangeType.Deleted`

---

## Message Types

### Commands (Intent to Change)

**Commands = "Please do this"**

```csharp
// CreateActorCommand
{
  "name": "John Doe",
  "rank": 999,
  "source": "Manual",
  "correlationId": "abc-123"
}

// UpdateActorCommand
{
  "actorId": 123,
  "name": "Updated",
  "rank": 500,
  "source": "IMDb",
  "correlationId": "def-456"
}

// DeleteActorCommand
{
  "actorId": 123,
  "correlationId": "ghi-789"
}
```

**Queues:**
- `actor.commands.create`
- `actor.commands.update`
- `actor.commands.delete`

**Processed by:** `ActorCommandHandler`

---

### Events (Something Happened)

**Events = "This already happened"**

```json
{
  "actorId": 123,
  "changeType": "Created",
  "actor": {
    "id": 123,
    "name": "John Doe",
    "rank": 999,
    "source": "Manual"
  },
  "occurredAt": "2026-01-28T15:30:00Z"
}
```

**Queue:** `actor.events.log`

**Processed by:** `ActorIngestionWorker` (just logs currently)

---

## RabbitMQ Queue Structure

After running the app, you'll see **4 queues** in RabbitMQ Management UI:

| Queue Name | Purpose | Consumer |
|------------|---------|----------|
| `actor.commands.create` | Create actor commands | `ActorCommandHandler` |
| `actor.commands.update` | Update actor commands | `ActorCommandHandler` |
| `actor.commands.delete` | Delete actor commands | `ActorCommandHandler` |
| `actor.events.log` | Actor change events (notifications) | `ActorIngestionWorker` |

---

## Benefits of This Architecture

### 1. **Non-Blocking API**
- Client gets immediate response (202 Accepted)
- Database operations happen in background
- Better performance under load

### 2. **Resilience**
- If database is slow, requests don't timeout
- Queue buffers commands during high traffic
- Commands processed at sustainable rate

### 3. **Scalability**
- Run multiple command handler instances (load balancing)
- Queue acts as traffic buffer
- Can process 1000s of commands per second

### 4. **Retry Logic**
- If command fails, it stays in queue
- Can implement retry policies
- Dead-letter queue for permanent failures

### 5. **Audit Trail**
- CorrelationId tracks each request
- Can correlate command → event → outcome
- Full traceability

---

## Trade-Offs

### ✅ Pros
- Faster API responses (non-blocking)
- Better scalability
- Built-in retry mechanism
- Traffic buffering
- Clear command/event separation

### ⚠️ Cons
- **Eventual consistency:** Client doesn't know immediately if command succeeded
- Need to handle command failures (dead-letter queue)
- More complex error reporting (can't return validation errors immediately)
- Client must poll or use webhooks to know outcome

---

## Client Experience

### Before (Synchronous):
```bash
POST /api/actors
↓ waits 200ms
← 201 Created (actor was saved, here's the ID)
```

Client knows immediately if it worked.

### After (Asynchronous):
```bash
POST /api/actors
↓ waits 5ms
← 202 Accepted (request received, here's a correlationId to track it)
```

Client needs to:
- Poll GET /api/actors/{id} later, or
- Listen for events, or
- Check logs for correlationId

---

## Tracking Command Outcomes

### Option 1: Polling (Simple)
```bash
# 1. Submit command
POST /api/actors
→ { "correlationId": "abc-123" }

# 2. Wait a bit
sleep 1

# 3. Query for actor (if you know the ID somehow)
GET /api/actors
```

### Option 2: Event Subscription (Better)
Client subscribes to same RabbitMQ event queue and listens for their correlationId.

### Option 3: Status Endpoint (Best for clients)
You could add:
```csharp
GET /api/commands/{correlationId}/status
→ { "status": "Completed", "actorId": 123 }
```

This would require storing command status in a separate collection.

---

## When to Use Each Pattern

### Use Synchronous (Your Old Way)
- Low traffic APIs
- Client needs immediate feedback
- Simple error handling
- Commands rarely fail

### Use Async with Queue (Your New Way)
- High traffic APIs
- Commands can take time (10s of seconds)
- Need traffic buffering
- Horizontal scaling important
- Okay with eventual consistency

---

## Testing the New Flow

### 1. Start app and dependencies
```bash
docker run -d --name mongodb -p 27017:27017 -e MONGO_INITDB_ROOT_USERNAME=admin -e MONGO_INITDB_ROOT_PASSWORD=admin mongo
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
dotnet run
```

### 2. Create actor (async)
```bash
curl -X POST https://localhost:5001/api/actors \
  -H "Content-Type: application/json" \
  -d '{"name":"Test Actor","rank":888,"source":"Manual"}' -k
```

**Response:**
```json
{
  "correlationId": "abc-123-...",
  "message": "Actor creation request accepted and queued for processing."
}
```

### 3. Check RabbitMQ
Open `http://localhost:15672` → Queues → `actor.commands.create`
- You'll see message count go up then down (as it's processed)

### 4. Check logs
Console will show:
```
Actor created: Id=165 Name=Test Actor CorrelationId=abc-123
Event received: Actor 165 was Created at 2026-01-28...
```

### 5. Verify in database
```bash
curl https://localhost:5001/api/actors/165 -k
```

Actor should exist now!

---

## Summary

**Your API now uses a two-queue system:**

1. **Command Queues** → Receive mutation requests (Create/Update/Delete)
2. **Event Queue** → Notify about completed changes

**Benefits:**
- API is non-blocking (fast 202 responses)
- Commands processed reliably in background
- Events notify interested parties
- Scalable and resilient

**Your choice makes sense** for systems where eventual consistency is acceptable and you need better throughput!
