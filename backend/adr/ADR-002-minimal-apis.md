# ADR-002: Use ASP.NET Core Minimal APIs for HTTP endpoints

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

The FunEvents backend exposes HTTP endpoints for the console prototype and partner sales channels. ASP.NET Core supports both controller-based APIs and Minimal APIs. The backend is organized by feature and uses Application use cases behind its HTTP boundary, as described in [ADR-001](ADR-001-clean-architecture.md).

## Decision

Use ASP.NET Core Minimal APIs for the HTTP layer.

- Keep application startup and dependency registration in `Program.cs`.
- Map each API feature through a focused endpoint mapping module, such as `MapReservationEndpoints`.
- Use route groups to apply a feature prefix, OpenAPI tags, and shared endpoint behavior.
- Keep endpoint handlers responsible for HTTP binding and coordination only. Delegate business workflows to Application use cases.
- Keep HTTP contracts, request validation, and HTTP result mapping in the API feature.

## Consequences

- Minimal APIs have less controller and attribute ceremony and make route groups and endpoint behavior explicit.
- Feature-specific endpoint modules keep route definitions out of a growing `Program.cs`.
- The team must maintain its own conventions for endpoint organization, validation, authorization, and OpenAPI metadata instead of relying on controller conventions.
- Choosing Minimal APIs does not move business logic into endpoint handlers or change the Clean Architecture dependency boundaries.
