# FunEvents backend prototype

The backend contains the Minimal API, application and domain logic, infrastructure, migrations, and reservation expiry worker. The API reserves tickets from the inventory pool; it does not process payment or issue tickets.

For prerequisites, local API key configuration, Aspire orchestration, standalone PostgreSQL/API/worker startup, sample requests, and shutdown steps, see [Local development setup](../Documents/SETUP-LOCAL-ENVIRONMENT.md).

Reservation endpoints:

- `POST /api/v1/reservations`
- `GET /api/v1/reservations/{reservationCode}`
- `POST /api/v1/reservations/{reservationCode}/cancel`
