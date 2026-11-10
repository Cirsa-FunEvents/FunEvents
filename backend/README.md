# FunEvents backend prototype

The backend contains the Minimal API, application and domain logic, infrastructure, migrations, and reservation expiry worker. It reserves inventory and includes a simulated payment provider that can leave an attempt pending, decline it, or succeed. A successful payment confirms the reservation; if the reservation hold has expired before confirmation, the payment is marked `RefundRequired`. This prototype does not issue tickets or execute refunds.

For prerequisites, local API key configuration, Aspire orchestration, standalone PostgreSQL/API/worker startup, sample requests, and shutdown steps, see [Local development setup](../Documents/SETUP-LOCAL-ENVIRONMENT.md).

Reservation endpoints:

- `POST /api/v1/reservations`
- `GET /api/v1/reservations/{reservationCode}`
- `POST /api/v1/reservations/{reservationCode}/cancel`
- `POST /api/v1/reservations/{reservationCode}/payments`
