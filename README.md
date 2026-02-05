## HomeAssignment (Deployment / Tester Guide)

This repository contains a small .NET 8 solution with a standalone API project:

- `HomeAssignment/src/HomeAssignment.Api` (ASP.NET Core Web API)
- `HomeAssignment/src/Domain`
- `HomeAssignment/src/Infrastructure`

The API uses:

- **MongoDB** for persistence
- **RabbitMQ** for async command handling (CQRS-style writes)

### Prerequisites

- **.NET SDK 8** (`dotnet --version`)
- **Docker** (recommended to run the app; required for integration tests)

### Quick start (run the API locally)

Start dependencies (Mongo + RabbitMQ):

```bash
docker compose up -d
```

Run the API:

```bash
dotnet run --project "HomeAssignment/src/HomeAssignment.Api/HomeAssignment.Api.csproj"
```

Swagger:

- `http://localhost:5293/swagger`
- `https://localhost:7289/swagger`

If HTTPS fails due to dev certificates, run:

```bash
dotnet dev-certs https --trust
```

RabbitMQ Management UI:

- URL: `http://localhost:15672`
- User/Pass: `guest` / `guest`

### Build + test (what a tester should run)

From the repo root:

```bash
dotnet restore "HomeAssignment.sln"
dotnet build "HomeAssignment.sln" -c Release
dotnet test "HomeAssignment.sln" -c Release
```

Notes:

- **Unit tests** run without external services.
- **Integration tests** require **Docker**, because they use **Testcontainers** to spin up MongoDB + RabbitMQ.

### Configuration (override via environment variables)

The API reads settings from `appsettings.json`. You can override them with env vars:

- `MongoSettings__ConnectionString`
- `MongoSettings__DatabaseName`
- `MongoSettings__CollectionName`
- `Messaging__RabbitMq__ConnectionString` (EasyNetQ format, e.g. `host=localhost;username=guest;password=guest`)
- `JwtSettings__SecretKey` (demo key in repo; replace if needed)

### Common commands

Stop dependencies:

```bash
docker compose down
```

Remove volumes (reset Mongo data):

```bash
docker compose down -v
```

