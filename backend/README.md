# FunEvents backend prototype

The API reserves a quantity from a single inventory pool for a session. It does not process payment or issue tickets.

## Development reservation data

In Development, the API applies EF Core migrations and idempotently seeds:

- Event: `EVT-001`
- Session: `SES-001-A`
- User: `USR-042`
- Available tickets: `100`

Set a local channel API key with user secrets (never commit the value):

```sh
dotnet user-secrets init --project backend/FunEvents.Api/FunEvents.Api.csproj
dotnet user-secrets set "PartnerApiKeys:Channels:console" "<local-secret>" --project backend/FunEvents.Api/FunEvents.Api.csproj
```

Run those commands from the repository root.

The API and the reservation expiry worker require the `ConnectionStrings:funevents` connection string. Aspire supplies it to both projects when they are started through the AppHost. For a direct API launch, set `ConnectionStrings__funevents` in the environment; run the worker separately with the same connection string when expiry processing is needed.

The API applies migrations and development seed data at startup. Aspire starts the worker after the API is healthy, so the worker starts after that development initialization has completed.

Start Docker Desktop, configure the channel secret above, then run the API, worker, and PostgreSQL together from the repository root:

```sh
dotnet run --project orchestration/FunEvents.AppHost/FunEvents.AppHost.csproj
```

## Reservation API

- `POST /api/v1/reservations`
- `GET /api/v1/reservations/{reservationCode}`
- `POST /api/v1/reservations/{reservationCode}/cancel`

Reservation creation requires `X-Api-Key` and `Idempotency-Key` headers. The API key selects the channel; reservation reads and cancellations are limited to that channel.
