# Restaurant Seating API

ASP.NET Core / .NET 8 API for seating groups in a restaurant. Uses EF Core 8 and SQL Server, with transactional queue processing and tests against a real database.

## Run with Docker

Requires Docker Compose. SQL Server runs as `linux/amd64`; on Apple Silicon this uses emulation. Allow enough Docker memory for SQL Server (at least 2 GB for the database, preferably 4 GB or more for the complete stack).

```bash
cp .env.example .env
# Edit SQL_SERVER_PASSWORD in .env before the first start.
docker compose up --build -d
```

If `.env` already exists, keep its password: changing the environment variable does not change the login in an existing database volume.

- Swagger: http://localhost:5080/swagger
- Readiness: http://localhost:5080/health
- SQL Server for Rider: `localhost,14333`, SQL authentication, user `sa`, password from `.env`, trust the local server certificate.

Compose waits for SQL Server's health check. The API applies EF migrations on startup and seeds five tables with capacities **2, 3, 4, 5, 6** (IDs 1–5). Subsequent starts preserve visits and seating. Ports can be changed in `.env`.

```bash
docker compose logs api
docker compose stop
# Remove containers/network while keeping the database volume:
docker compose down
```

`docker compose down -v` also deletes the database volume and all visits. Use it only when you intentionally want a fresh demo database.

The development setup uses SQL Server Developer edition, an `sa` login, and a trusted local certificate for reproducibility. Production deployment should use a restricted application login, a trusted certificate, and migrations applied as a separate deployment step. Swagger is enabled only in Development.

## Run the API in Rider

Install/select a .NET 8 SDK and open `RestaurantSeating.sln`. `global.json` allows newer .NET 8 feature bands. On macOS, if `dotnet` resolves to another installation, point Rider's .NET CLI path to the installation that contains .NET 8 (for example `/usr/local/share/dotnet/dotnet`). For a terminal using that installation:

```bash
export PATH="/usr/local/share/dotnet:$PATH"
```

Start just the database:

```bash
docker compose up -d db
```

Configure the API connection string using Rider environment variables (`ConnectionStrings__Restaurant`) or user secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Restaurant" \
  'Server=localhost,14333;Database=RestaurantSeating;User Id=sa;Password=YOUR_LOCAL_PASSWORD;Encrypt=true;TrustServerCertificate=true' \
  --project src/Api

dotnet run --project src/Api
```

Use the password from `.env`. The `Api` launch profile enables Development and migration-on-startup and listens on port 5080. Stop the Compose API first if it already occupies that port: `docker compose stop api`.

## HTTP contract

JSON enum values are returned as names. Timestamps use UTC. Group IDs are UUIDs, and `arrivalOrder` is the persisted order of registration.

| Method | Route | Result |
|---|---|---|
| POST | `/api/groups` | Register `{ "size": 2 }`; `201 Created`, `Location` header and group |
| GET | `/api/groups/{id}` | Group, including its current state and timestamps |
| GET | `/api/groups?status=Waiting&offset=0&limit=50` | Ordered page `{ items, offset, limit }`; status optional, limit 1–100 |
| POST | `/api/groups/{id}/leave` | Leave the queue or finish a seated visit; `200` and updated group |
| GET | `/api/tables` | Tables with capacity, occupied seats and available seats |
| GET | `/health` | `200` if the database is reachable, otherwise `503` |

Example group response:

```json
{
  "id": "cb18d8e5-f056-475c-b96c-93ff9fc33bfc",
  "size": 2,
  "status": "Seated",
  "arrivalOrder": 1,
  "tableId": 1,
  "arrivedAt": "2026-09-13T12:00:00+00:00",
  "seatedAt": "2026-09-13T12:00:00+00:00",
  "leftAt": null
}
```

No seats available is a successful registration with `status: "Waiting"`, not an HTTP error. Group states are `Waiting → Seated → Completed` or `Waiting → Left`. A completed visit retains its table ID for history but no longer occupies seats. Repeating `leave` returns the same terminal state and timestamp.

Invalid input returns `400`; an unknown group returns `404`. Lock contention exceeding five seconds returns `503` with `Retry-After: 1`. Errors use `application/problem+json`; validation errors also contain an `errors` dictionary. Unexpected failures return a generic `500` without SQL details and are logged server-side. Route values that do not match the UUID route constraint return `404`.

```bash
curl -i -X POST http://localhost:5080/api/groups \
  -H 'Content-Type: application/json' -d '{"size":6}'
# On a fresh database, a second size-6 group waits until the first leaves.
curl -i -X POST http://localhost:5080/api/groups \
  -H 'Content-Type: application/json' -d '{"size":6}'
curl 'http://localhost:5080/api/groups?status=Waiting'
curl http://localhost:5080/api/tables
# Replace GROUP_ID with the first response's ID.
curl -X POST http://localhost:5080/api/groups/GROUP_ID/leave
```

A sample request file is available in `docs/restaurant.http`.

## Seating decisions

For each waiting group in ascending `ArrivalOrder`:

1. Find tables with enough free seats for the entire group.
2. Prefer completely empty tables, even over an exact fit at a shared table.
3. Within that category, choose the smallest remaining space after seating (the smallest suitable capacity for an empty table).
4. Break ties by table ID.
5. If no table fits, keep the group waiting and continue with later groups.

Every new assignment immediately updates the working occupancy before evaluating the next group. Existing seated groups are never moved. Group departure and queue reassignment happen synchronously in the same transaction, so no background worker is needed. A group leaving the queue is retained in history and ignored by the allocator.

Assumptions: one restaurant, a fixed table inventory, immutable group sizes, and one visit per group ID. The initial inventory is defined by the migration; a later inventory change would require an explicit migration and operational handling. Table administration, authentication, reservations, and manual seating are not implemented.

The specified bypass policy can keep a large group waiting indefinitely while smaller groups consume available seats. No reservation/aging rule is added because it would change that policy. Pagination describes the current state and is not a frozen snapshot of a changing queue.

## Transactions and concurrency

Every arrival and departure begins an explicit SQL transaction, then acquires the same **SQL Server application lock** using `sp_getapplock` (`Exclusive`, owner `Transaction`). Only then is active state loaded. The lock remains held through registration, allocation, saving and commit; disposal without commit rolls everything back and releases the lock.

`ArrivalOrder` is an identity column generated **inside this lock**. For concurrent requests, arrival means database registration order, not client clock time or HTTP connection order. Sequence gaps after rollback are harmless. Failed transactions do not leave a registered group behind.

All application writers must use this protocol. It works across API instances connected to the same database; it is not an in-process mutex. Foreign keys and check constraints protect group size, table capacity and state consistency. Cross-row occupancy is enforced by the allocator under the database lock, not by a misleading single-row check constraint.

Writes for this single restaurant are serialized intentionally. The queue is loaded in full, while completed visits are excluded. The straightforward allocator costs approximately `O(G × T log T)` for G waiting groups and T tables. This is appropriate for the small restaurant scope; a high-throughput multi-restaurant system would need restaurant-scoped locks and a more specialized queue implementation.

No automatic database retries are configured: blindly replaying an arrival after an uncertain commit can create a duplicate visit. `POST /api/groups` creates a new visit each time; request deduplication/idempotency keys are not implemented. A confirmed `503` lock failure occurs before registration and may be retried. Repeated departure is idempotent.

Startup migration execution is enabled explicitly for local/Compose runs only. Multiple API instances should start against an already-migrated schema; concurrent migration orchestration is not part of the application lock protocol.

## Solution structure

- **BusinessLogic**: domain models and transitions, the pure seating allocator, actions, and narrow persistence interfaces. No EF or ASP.NET dependencies.
- **Infrastructure**: EF mapping, migrations, SQL lock, transactional store and read queries. Depends on BusinessLogic.
- **Api**: controllers, request validation, ProblemDetails, Swagger and composition root. References BusinessLogic and Infrastructure for DI registration.
- **BusinessLogic.Unit**: allocation and state transition tests.
- **Api.Integration**: real HTTP pipeline with `WebApplicationFactory` and real SQL Server, including concurrent operations and injected persistence failures.

Specific actions and a restaurant-scoped transaction interface keep business orchestration testable without a generic repository or a messaging framework. EF maps the domain objects through fluent configuration; persistence attributes do not leak into BusinessLogic.

## Tests and verification

Run the full suite in Docker (requires `.env`):

```bash
docker compose --profile tests run --build --rm tests
```

The test service starts SQL Server as needed, applies the real migrations to a uniquely named `RestaurantSeatingTests_<UUID>` database and removes that database afterwards. It never deletes the application's `RestaurantSeating` database. Tests use SQL credentials with permission to create/drop a database. The existing SQL data volume remains intact.

For local unit tests:

```bash
dotnet test tests/BusinessLogic.Unit
```

For all tests locally, configure `RESTAURANT_TEST_CONNECTION_STRING` with a connection string to the SQL Server (same credentials as above; `Database=master` is sufficient), then run:

```bash
dotnet test RestaurantSeating.sln
dotnet format RestaurantSeating.sln --verify-no-changes
dotnet build RestaurantSeating.sln -c Release
```

Integration tests fail with a setup message if no SQL Server connection is configured; they are not silently skipped or substituted with an in-memory provider. Scenarios cover queue order/bypass, empty-table priority, capacity under contention, repeated departure, persistence across app instances, lock timeout, validation, database constraints and rollback of both initial registration and departure/reassignment.

For schema changes, restore the local EF tool and generate a migration:

```bash
dotnet tool restore
dotnet ef migrations add YourChange --project src/Infrastructure --startup-project src/Api
```

The design-time factory uses `ConnectionStrings__Restaurant` when set. Its fallback supports generating migrations without credentials; applying migrations requires a real connection string.
