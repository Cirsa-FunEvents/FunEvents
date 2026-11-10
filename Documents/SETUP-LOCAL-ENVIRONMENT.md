# Local development setup

This guide covers two ways to run FunEvents locally:

1. Run the API, reservation expiry worker, and PostgreSQL together through .NET Aspire.
2. Run the backend projects directly, with PostgreSQL started separately.

Run commands from the repository root unless a command says otherwise.

## Prerequisites

- .NET 10 SDK, matching the target framework used by the projects.
- Docker Desktop or another Docker-compatible engine, running.
- Aspire CLI, which is installed for this environment. Check it with `aspire --version`.

Check the SDK and Docker installation:

```sh
dotnet --version
docker version
```

## Configure a local partner API key

The API uses a partner API key to identify the sales channel. Store a development key with .NET user-secrets rather than adding it to a committed settings file:

```sh
dotnet user-secrets init --project backend/FunEvents.Api/FunEvents.Api.csproj
dotnet user-secrets set "PartnerApiKeys:Channels:console" "local-console-secret" --project backend/FunEvents.Api/FunEvents.Api.csproj
```

Use the same value in the `X-Api-Key` header when calling reservation endpoints. The configured channel name is `console`.

## Configure the Aspire PostgreSQL password

The AppHost persists PostgreSQL data in a Docker volume and reads the database password from Aspire user secrets. The AppHost launch profile sets both `DOTNET_ENVIRONMENT` and `ASPIRE_ENVIRONMENT` to `Development`, so user secrets load whether the AppHost is started from the terminal or VS Code. Set the secret once before the first Aspire run; it is retained across restarts and is not committed to the repository.

From the repository root, generate and save a local password:

```sh
aspire secret set Parameters:postgres-password "$(openssl rand -base64 32)" \
  --apphost orchestration/FunEvents.AppHost/FunEvents.AppHost.csproj
```

If you already started PostgreSQL before configuring this persistent password, its existing volume may contain a database initialized with a different password. If the local database data is disposable, stop Aspire and remove only the volume attached to this AppHost's PostgreSQL resource in Docker Desktop. On the next run, PostgreSQL creates a fresh database using the configured secret, and the API reapplies migrations and development seed data. If you need to preserve the data, keep the volume and update the PostgreSQL `postgres` role password in the existing database to match the Aspire secret before restarting.

## Run the complete local system with Aspire

Aspire starts PostgreSQL, the Minimal API, and the reservation expiry worker. PostgreSQL data is stored in a Docker volume and its password is stored as a local Aspire secret, so both remain consistent across AppHost restarts. Configure the password as described above. To use `dotnet run`, turn off Aspire's default watch mode for this AppHost only:

```sh
cd orchestration/FunEvents.AppHost
aspire config set features.defaultWatchEnabled false
cd ../..
DOTNET_ENVIRONMENT=Development ASPIRE_ENVIRONMENT=Development dotnet run --project orchestration/FunEvents.AppHost/FunEvents.AppHost.csproj
```

This setting is project-local. With Aspire CLI bundle enabled, `dotnet run` delegates to the CLI; disabling watch mode avoids the CLI's `--no-build` and watch-mode conflict. Alternatively, run directly with the Aspire CLI:

```sh
DOTNET_ENVIRONMENT=Development ASPIRE_ENVIRONMENT=Development aspire run --apphost orchestration/FunEvents.AppHost/FunEvents.AppHost.csproj
```

To debug through the VS Code Aspire extension, open the repository in VS Code and press `F5` with the Aspire launch configuration selected. Aspire's default watch behavior can be controlled with the `features.defaultWatchEnabled` setting ([configuration reference](https://aspire.dev/reference/cli/configuration/)).

On first startup, wait for PostgreSQL to become healthy. The API applies EF Core migrations and seeds development data when running in the Development environment. Aspire waits for the API health check before starting the worker.

The AppHost prints the Aspire Dashboard address and the API endpoint when it starts. Open the dashboard to inspect resource health, logs, and the API URL. The API exposes:

- Health check: `/health`
- OpenAPI document in Development: `/openapi/v1.json`
- Interactive API reference in Development: `/scalar`
- Create reservation: `POST /api/v1/reservations`
- Get reservation: `GET /api/v1/reservations/{reservationCode}`
- Cancel reservation: `POST /api/v1/reservations/{reservationCode}/cancel`
- Process simulated payment: `POST /api/v1/reservations/{reservationCode}/payments`

In Scalar, use **Authorize** to enter the `X-Api-Key` for the `console` channel. Reservation creation and payment processing use `Idempotency-Key`; reuse a key only when retrying the same operation. The fake payment provider randomly returns `Pending`, `Succeeded`, or `Declined`. Success confirms the reservation. A late success that cannot confirm an expired or otherwise unavailable reservation is recorded as `RefundRequired`; no refund is executed.

### Run the console client

The AppHost includes `funevents-console` as an explicit-start resource, so it appears in the Aspire Dashboard but does not start automatically. Start that resource when you want to use the client, then open its **Terminal** view to enter menu choices and reservation values. Aspire supplies the API URL from the running API resource. The API key comes from the console project's User Secrets as described below. Aspire's interactive terminal support requires the configured Aspire 13.6.4 packages and CLI.

To run the client outside Aspire, start the API and run the console in another terminal. For the standalone API launch profile:

```sh
FUN_EVENTS_API_BASE_URL=http://localhost:5053 \
FUN_EVENTS_API_KEY=local-console-secret \
dotnet run --project frontend/console/FunEvents.Console/FunEvents.Console.csproj
```

The client offers create, read, cancel, simulated payment, and exit actions. Create prompts for event, session, user, and quantity; the seeded sample codes are defaults. Read, cancel, and payment prompt for a reservation code. Payment generates an idempotency key; reuse the displayed key when retrying an attempt that remains pending. Each action prints the API result and returns to the menu. The API persists its payment attempt and reservation state in PostgreSQL.

The console client reads its local API URL and key from .NET User Secrets. Set them from the repository root:

```sh
dotnet user-secrets set "FunEvents:ApiBaseUrl" "http://localhost:5053" \
  --project frontend/console/FunEvents.Console/FunEvents.Console.csproj
dotnet user-secrets set "FunEvents:ApiKey" "local-console-secret" \
  --project frontend/console/FunEvents.Console/FunEvents.Console.csproj
```

The API key must match the API's `PartnerApiKeys:Channels:console` value. User Secrets are project-specific, so configure the same value for the API project if needed. The console also accepts `FUN_EVENTS_API_BASE_URL` and `FUN_EVENTS_API_KEY` environment variables, which take precedence over User Secrets. If neither is set, it uses `http://localhost:5053` and `local-console-secret` as local development defaults.

Stop the AppHost with `Ctrl+C`. To delete the persisted PostgreSQL data, remove the Aspire-created Docker volume from Docker Desktop or with Docker CLI after stopping the AppHost. This permanently deletes local database contents.

## Run the backend projects individually

This mode is useful when debugging the API or worker. Start PostgreSQL yourself, then launch the API and worker in separate terminals.

### 1. Start PostgreSQL

```sh
docker run --name funevents-postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=funevents \
  -p 5432:5432 \
  -v funevents-postgres-data:/var/lib/postgresql/data \
  -d postgres:17
```

If the container already exists, start it with `docker start funevents-postgres`. The corresponding connection string is:

```text
Host=localhost;Port=5432;Database=funevents;Username=postgres;Password=postgres
```

### 2. Run the API

In a terminal from the repository root:

```sh
export ConnectionStrings__funevents='Host=localhost;Port=5432;Database=funevents;Username=postgres;Password=postgres'
dotnet run --project backend/FunEvents.Api/FunEvents.Api.csproj --launch-profile http
```

The checked-in `http` launch profile sets `ASPNETCORE_ENVIRONMENT=Development` and listens at `http://localhost:5053`. The API reads the database connection string from Aspire automatically when hosted by AppHost. In Development, API startup applies migrations and idempotently adds this sample catalog. Re-running the API does not reset inventory already consumed by reservations.

| Event | Session | Sales | Initial inventory |
| --- | --- | --- | ---: |
| `EVT-001` FunEvents Demo Concert | `SES-001-A` | Open | 100 |
| `EVT-001` FunEvents Demo Concert | `SES-001-B` | Open | 80 |
| `EVT-002` The Midnight Players: Live Theatre | `SES-002-A` | Open | 60 |
| `EVT-002` The Midnight Players: Live Theatre | `SES-002-B` | Closed | 60 |
| `EVT-003` City Lights Comedy Night | `SES-003-A` | Open | 40 |
| `EVT-003` City Lights Comedy Night | `SES-003-B` | Open | 40 |
| `EVT-004` Family Fun Festival | `SES-004-A` | Open | 150 |

Seeded users: `USR-042`, `USR-043`, `USR-100`, and `USR-200`. Sessions start between 14 and 60 days after seeding.

### 3. Run the expiry worker

In a second terminal, use the same connection string:

```sh
export ConnectionStrings__funevents='Host=localhost;Port=5432;Database=funevents;Username=postgres;Password=postgres'
dotnet run --project backend/FunEvents.Worker/FunEvents.Worker.csproj
```

The worker periodically expires due reservations and releases their inventory. It is a separate process from the API; the API can be run by itself for endpoint debugging, but background expiry will not run until the worker is started.

## Call the API

For example, create a reservation using the seeded event, session, and user. Replace the API key below if you configured a different development secret:

```sh
curl -i http://localhost:5053/api/v1/reservations \
  -H 'Content-Type: application/json' \
  -H 'X-Api-Key: local-console-secret' \
  -H 'Idempotency-Key: local-demo-001' \
  -d '{"eventCode":"EVT-001","sessionCode":"SES-001-A","userCode":"USR-042","quantity":2}'
```

Use the returned reservation code to retrieve, cancel, or make a simulated payment for the reservation. Reservation and payment operations are scoped to the channel selected by the API key. The payment API creates a pending attempt before calling the fake provider, then saves the payment result and any reservation confirmation together in one database transaction.

## Stop local services

- Stop API, worker, or Aspire with `Ctrl+C` in their terminals.
- Stop PostgreSQL with `docker stop funevents-postgres` when it was started directly.
- The named Docker volume keeps direct PostgreSQL data between container runs. Remove it only when you intentionally want a clean local database: `docker volume rm funevents-postgres-data`.
