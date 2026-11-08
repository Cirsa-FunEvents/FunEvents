# ADR-001: Organize the backend using Clean Architecture

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

The backend contains HTTP endpoints, reservation use cases, domain rules, PostgreSQL persistence, database migrations, and background processing. These concerns change for different reasons and should not be coupled directly to one another. In particular, business and application code should not depend on ASP.NET Core, EF Core, or PostgreSQL.

## Decision

Organize the backend as projects in one solution, with dependencies pointing inward toward the domain:

```mermaid
flowchart LR
    Api[FunEvents.Api]
    Worker[FunEvents.Worker]
    Migrations[FunEvents.Migrations]
    Infrastructure[FunEvents.Infrastructure]
    Application[FunEvents.Application]
    Domain[FunEvents.Domain]

    Api --> Application
    Api --> Infrastructure
    Api --> Migrations
    Worker --> Infrastructure
    Migrations --> Infrastructure
    Infrastructure --> Application
    Infrastructure --> Domain
    Application --> Domain
```

Responsibilities:

- **Domain** contains business concepts and rules. It has no dependency on the other backend projects or infrastructure frameworks.
- **Application** contains use cases and ports, such as reservation commands, queries, handlers, and repository interfaces. It depends on Domain.
- **Infrastructure** implements application ports and contains PostgreSQL, EF Core, and persistence code. It depends on Application and Domain.
- **API** owns HTTP routes, request/response contracts, authentication at the HTTP boundary, and HTTP result mapping. It is a composition root that wires Application to Infrastructure. It references Migrations for development-time database initialization.
- **Worker** hosts background processes and wires them to Infrastructure and application ports.
- **Migrations** owns EF Core migration files and the design-time DbContext factory. It references Infrastructure, where the DbContext is defined.

### Use-case organization and API boundary

- Each application operation is represented by its own use case. Its command or query and handler are grouped in a dedicated use-case folder; result and failure types stay with the use case that defines them.
- Commands represent operations that change state; queries represent operations that return data. This is lightweight command/query separation, not a commitment to full CQRS, separate read models, or separate data stores.
- This separation is not currently strict: reservation lookup can transition an overdue reservation to `Expired` and release inventory. That behavior keeps a read response current, but should be revisited if queries are required to be side-effect-free.
- API routes are organized by feature. Each feature owns its HTTP contracts, request validation, endpoint mappings, and HTTP result mapping.
- An API endpoint binds HTTP input, obtains caller context, invokes an Application use case, and maps the outcome to an HTTP response. It does not call repositories or contain business rules directly.
- HTTP request and response DTOs are distinct from Application commands and queries. Application code does not reference ASP.NET Core types.
- Shared authentication and authorization concerns belong to the API security boundary, not to an individual business feature.

These project boundaries organize code and dependencies; they do not imply that each layer is a separately deployed service. API and Worker are separate executable hosts. They can share application and infrastructure code and use the same database.

## Consequences

- Domain and Application code can be reasoned about without depending on HTTP or database implementation details.
- API and Worker remain composition roots where infrastructure implementations are registered.
- HTTP contracts stay in the API; application commands and queries are separate from those contracts.
- Each API feature and Application use case has a clear place for its own contracts, validation, and outcome mapping. This adds files and folders, but keeps transport concerns and business workflows independently changeable.
- EF Core and PostgreSQL details remain in Infrastructure and Migrations.
- The solution has more projects, references, and dependency-injection wiring than a single-project application.
- API and Worker currently share persistence and database resources. This decision does not introduce separate databases, event sourcing, CQRS infrastructure, or a mediator library.
