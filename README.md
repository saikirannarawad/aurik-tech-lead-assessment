# Aurik Equipment Monitoring

A backend-first take-home submission for the Aurik Technologies Founding Tech Lead assessment. The system ingests inconsistent vendor payloads (PulseForge, ThermexWatch, MaintaFlow) from an industrial monitoring platform, normalizes them into a canonical schema, processes them asynchronously with retry and dead-letter handling, and serves an explainable machine-level operational attention view through clean backend APIs. A minimal React dashboard consumes those APIs for visual review and includes a built-in "Vendor Service" page so reviewers can fire pre-built test scenarios without leaving the browser.

## Stack

- **Backend:** .NET 8 (LTS), C#, ASP.NET Core Web API
- **Persistence:** MongoDB 7
- **Async:** in-process `BackgroundService` + `System.Threading.Channels` (bounded)
- **Frontend:** React 18 + Vite + TypeScript + TanStack Query
- **Tests:** xUnit + FluentAssertions + Moq + EphemeralMongo (real Mongo in tests)
- **Containerization:** Docker Compose (MongoDB + API)

## Repository layout

```
Aurik-Equipment-Monitoring/
├── backend/
│   ├── Aurik.Monitoring.sln
│   ├── Dockerfile
│   ├── seed/                                       # CSV + JSON samples copied from assessment assets
│   ├── src/
│   │   ├── Aurik.Monitoring.Domain/                # Entities, enums, value objects (no infra deps)
│   │   ├── Aurik.Monitoring.Application/           # Use cases, normalizers, factory, derived view, worker
│   │   ├── Aurik.Monitoring.Infrastructure/        # MongoDB context, repositories, seeders
│   │   └── Aurik.Monitoring.Api/                   # Controllers, middleware, DI composition, Scalar+OpenAPI
│   └── tests/
│       ├── Aurik.Monitoring.UnitTests/             # Normalizers, factory, derived-status, idempotency
│       └── Aurik.Monitoring.IntegrationTests/      # API endpoint tests against ephemeral MongoDB
├── frontend/                                       # Vite + React + TS dashboard
│   └── src/
│       ├── pages/                                  # Dashboard, MachineDetail, Vendor Service, Ingestion Status
│       ├── vendor/templates.ts                     # 30 pre-built test scenarios (10 per vendor)
│       ├── api/client.ts                           # Typed API client
│       └── components/                             # Small shared UI (StatusBadge, etc.)
├── docs/
│   ├── architecture.md                             # Architecture overview + Mermaid diagrams
│   ├── design-note.md                              # Short design note (decisions + trade-offs)
│   └── aurik-monitoring.postman_collection.json    # Postman collection
├── scripts/
│   └── load-samples.sh                             # Pushes the provided sample payloads to the running API
└── docker-compose.yml                              # MongoDB + API
```

## Quick start (Docker — recommended)

```bash
cd Aurik-Equipment-Monitoring
docker compose up --build
```

- API: <http://localhost:8080>
- API explorer (Scalar UI): <http://localhost:8080/scalar/v1>  ← visit this in your browser
- OpenAPI JSON (raw): <http://localhost:8080/swagger/v1/swagger.json>
- MongoDB: `mongodb://localhost:27017`

On startup, the API:
1. Creates Mongo indexes (unique idempotency keys for raw payloads + normalized events).
2. Seeds reference data from `backend/seed/asset_reference.csv` and `line_reference.csv`.

To load sample vendor payloads against the running API:

```bash
# from repo root, with API up on localhost:8080
./scripts/load-samples.sh
```

## Quick start (local — requires .NET 8 SDK + MongoDB)

```bash
# 1. Start MongoDB (any way you like — Docker is easiest)
docker run -d --name aurik-mongo -p 27017:27017 mongo:7

# 2. Run the API
cd backend
dotnet run --project src/Aurik.Monitoring.Api
# API listens on http://localhost:5080 by default for the Development profile
```

## Run the frontend

```bash
cd frontend
npm install
npm run dev          # http://localhost:5173 — proxies /api to localhost:8080
```

The frontend exposes four read-friendly pages:

| Page | URL | What it does |
| --- | --- | --- |
| **Dashboard** | `/dashboard` | Plant + line summary with status counters and the full machine list |
| **Machine Detail** | `/machines/:id` | Per-machine view: derived status, reason codes, source event refs, and recent normalized events |
| **Vendor Service** | `/vendor` | Pick vendor → pick scenario (30 pre-built scenarios, grouped by category) → edit JSON → POST. Lets reviewers drive the system from the browser instead of curl |
| **Ingestion Status** | `/ingestion` | Auto-refreshing list of recent payloads + dead-letter queue inspection |

The **Vendor Service** page also has a "Use current timestamp" toggle so payloads land inside the 48-hour attention window even when the original sample data is months old.

## How to demo end-to-end

Three equivalent ways to push payloads through the pipeline, in order of effort:

1. **Vendor Service page (easiest)** — open <http://localhost:5173/vendor>, pick a vendor and scenario, click Send. The page bundles 30 pre-built scenarios across the three vendors (nominal, warnings, critical, recovery, multi-record batches, edge cases). Keep "Use current timestamp" ON so the events land inside the attention window and the dashboard lights up.
2. **Scripted bulk load** — `./scripts/load-samples.sh` POSTs every JSON file in `backend/seed/vendor_api_samples/` to the running API (useful for quickly populating multiple vendors at once).
3. **Scalar UI / Postman / curl** — for editing payloads by hand and inspecting raw responses.

To **trigger the dead-letter queue** deterministically, send any body that is valid JSON but not a valid vendor payload (e.g., the literal `null`, a JSON string, or an array). The normalizer fails with a non-retryable error and the payload appears on the Ingestion Status page within ~1 second.

## Running tests

```bash
cd backend
dotnet test                                           # unit + integration

# Unit tests only:
dotnet test tests/Aurik.Monitoring.UnitTests
```

Integration tests use **EphemeralMongo** (a real `mongod` started per test fixture), so they exercise the entire stack including index creation and concurrency behavior. No external services are required.

## API contract

A Postman collection lives at `docs/aurik-monitoring.postman_collection.json` with sample requests for every endpoint. A modern interactive API explorer (**Scalar**) is available at `/scalar/v1`, backed by the auto-generated OpenAPI document at `/swagger/v1/swagger.json`.

JSON output uses **`snake_case`** (`machine_id`, `plant_id`, `latest_relevant_event_time`, ...) to match the conceptual schema in the brief. Enums are serialized as strings.

### Endpoint summary

| Method | Path | Purpose | Auth |
| --- | --- | --- | --- |
| POST | `/api/ingestion/pulseforge` | Ingest a PulseForge batch | API Key |
| POST | `/api/ingestion/thermexwatch` | Ingest a ThermexWatch batch | API Key |
| POST | `/api/ingestion/maintaflow` | Ingest a MaintaFlow batch | API Key |
| GET | `/api/processing/status/{rawPayloadId}` | Status of one ingested payload | — |
| GET | `/api/processing/recent` | Recent payloads across vendors | — |
| GET | `/api/processing/dead-letters` | Dead-lettered payloads | — |
| GET | `/api/machines` | Machine views with filters (`plantId`, `lineId`, `status`, `minAttention`) | — |
| GET | `/api/machines/{id}/view` | Single machine operational view | — |
| GET | `/api/machines/{id}/events` | Recent normalized events for a machine | — |
| GET | `/api/plants` | Plants + lines from reference data | — |
| GET | `/api/plants/{id}/summary` | Plant operational summary | — |
| GET | `/api/plants/{plantId}/lines/{lineId}/summary` | Line summary scoped to a plant | — |
| GET | `/api/health` | Liveness probe | — |

### Authentication

Ingestion endpoints require an `X-Vendor-Api-Key` header. Keys are configured in `appsettings.json` (or env vars `Auth__VendorApiKeys__<Vendor>` for Docker). The middleware matches the URL-segment vendor against the configured key with constant-time comparison.

### Idempotency

Each ingestion call accepts an optional `X-Idempotency-Key` header. When absent, the server derives one from `SHA-256(vendor || body)`. Replays of the same body return `200 OK` with `duplicate: true` rather than re-queueing. Inside the worker, normalized events are deduped at insert time using the unique `(vendor, vendor_event_id)` index.

## Architecture overview

```
┌─────────────────┐     ┌──────────────────┐     ┌────────────────────────┐
│ External Vendor │ ──> │  Ingestion API   │ ──> │ raw_payloads (Mongo)   │
│  (3 schemas)    │     │  /api/ingestion  │     │  (idempotency-key UQ)  │
└─────────────────┘     └──────────────────┘     └────────────────────────┘
                                  │
                                  ▼ enqueue (Channel)
                       ┌────────────────────────┐
                       │ NormalizationWorker    │
                       │ (BackgroundService)    │
                       │  • factory → vendor    │
                       │  • retry w/ backoff    │
                       │  • dead-letter         │
                       └────────────────────────┘
                                  │
                ┌─────────────────┴─────────────────┐
                ▼                                   ▼
   ┌──────────────────────────┐      ┌──────────────────────────────┐
   │ normalized_events        │      │ machine_views                │
   │ (canonical schema)       │      │ (derived attention view —    │
   │ unique (vendor, eventId) │      │ optimistic concurrency)      │
   └──────────────────────────┘      └──────────────────────────────┘
                                                  ▲
                                                  │
                                       ┌──────────┴───────────┐
                                       │ Output APIs:         │
                                       │  machines, plants,   │
                                       │  processing status   │
                                       └──────────────────────┘
```

A more detailed diagram with a Mermaid source is in [`docs/architecture.md`](docs/architecture.md).

### Layers (Clean Architecture)

- **Domain** — POCO entities + enums + unit conversions. No framework or persistence dependencies.
- **Application** — Use cases (`IngestionService`, `PayloadProcessor`), normalizer interfaces, factory, derived-status calculator, async worker. Defines persistence *interfaces*; doesn't know Mongo exists.
- **Infrastructure** — MongoDB context, repository implementations, CSV reference-data seeder, BSON class maps. Wired in via `AddInfrastructure(...)`.
- **Api** — HTTP controllers, API-key middleware, error middleware, OpenAPI generation (Swashbuckle) + Scalar UI rendering, DI composition root in `Program.cs`.

### Design patterns at a glance

- **Factory pattern** — `INormalizerFactory` selects the right normalizer for a vendor; adding a new vendor is one class + one DI line.
- **Strategy pattern** — each `INormalizer` is an interchangeable strategy for converting a vendor's payload to canonical events.
- **Repository pattern** — `IRawPayloadRepository`, `INormalizedEventRepository`, `IMachineViewRepository`, etc. Application code never touches Mongo APIs.
- **Hosted-service worker / producer-consumer** — `IProcessingQueue` (channels) decouples accept-path from processing.
- **Optimistic concurrency** — `MachineViewRepository.UpsertWithVersionAsync` guards against lost updates during concurrent recomputes.

### SOLID

- **S** — each class has one responsibility (a normalizer, the calculator, a repository, the worker).
- **O** — adding a new vendor doesn't touch existing normalizers; reason codes are open-ended strings.
- **L** — `INormalizer` substitutability is enforced by tests.
- **I** — repositories are split per aggregate; `INormalizer` returns the exact shape callers need.
- **D** — Application depends on interfaces; Infrastructure provides implementations registered at the composition root.

### ACID and MongoDB

MongoDB single-document operations are ACID by definition. Where the workflow needs cross-document consistency we use:

- **Unique indexes** for natural-key dedupe (raw_payloads idempotency key, normalized_events `(vendor, vendor_event_id)`).
- **Optimistic concurrency tokens** for `machine_views` so concurrent normalizations cannot lose updates.
- **Unordered bulk inserts** that tolerate duplicate-key errors as a no-op, returning only the truly-inserted count.

Multi-document transactions are available in Mongo replica sets if a future requirement spans multiple aggregates atomically — the current scope doesn't need them.

## Derived attention rules (explainable)

The `DerivedStatusCalculator` walks recent events and applies deterministic rules. Each contributing rule emits a stable reason code so downstream consumers can explain *why* a machine needs attention. The full table is in [`docs/design-note.md`](docs/design-note.md).

| Rule | Reason code | Bumps attention to |
| --- | --- | --- |
| Vibration > rated max | `VIBRATION_OVER_THRESHOLD` | High |
| Temperature > rated max | `TEMPERATURE_OVER_THRESHOLD` | High |
| Power deviates ≥30% from baseline | `POWER_ANOMALY` | Moderate |
| Sensor health < 0.80 | `SENSOR_HEALTH_LOW` | Low |
| Maintenance overdue or ≥60d since service | `MAINTENANCE_OVERDUE` | High |
| Maintenance due_soon | `MAINTENANCE_DUE_SOON` | Low |
| Inspection defect | `INSPECTION_DEFECT` | Moderate |
| Vendor severity `critical` / `high` | `VENDOR_REPORTED_CRITICAL` / `_HIGH` | (preserves vendor severity) |
| Last event > 48h ago | `STALE_SIGNAL` (forces `Stale` status) | Low |
| Recovery signal after mild concerns | `RECOVERY_OBSERVED` | (may downgrade) |

## Assumptions

1. **Single tenant** per deployment — `plant_id` is informational; no multi-tenant isolation.
2. **Vendor confidence < 0.50** is treated as a low-trust source but is not rejected.
3. **`g` → `mm/s`** vibration conversion assumes a 10 Hz reference frequency, since the vendor does not transmit one. See `UnitConversions.GravityToMmPerSec`.
4. **MaintaFlow timestamps** ("yyyy/MM/dd HH:mm:ss") have no zone — treated as UTC.
5. **Idempotency** without a caller-supplied key uses `SHA-256(vendor || body)`. Sites that legitimately re-send identical content within a second should pass `X-Idempotency-Key` explicitly.
6. **Attention window** is 24 hours; **staleness** triggers at 48 hours. Both are constants on `DerivedStatusCalculator` and can be moved to options.

## Trade-offs

- **In-process queue vs. broker.** Channels keep the demo simple and observable; one host is the only consumer. A broker would be needed for horizontal scale; the producer-consumer contract is interface-driven, so swapping in RabbitMQ/Kafka is a single implementation change.
- **MongoDB vs. SQL.** Vendor payload shapes vary; a document store keeps the raw form trivially. The derived view is small and queried by indexed fields, so SQL would also have been viable.
- **Sync auth vs. signed webhooks.** API keys are good enough for an assessment; production should layer in HMAC payload signatures and replay protection.
- **Single host worker.** No leader election. If two API instances run, both consume their own local queues; the dedup index keeps storage consistent but distributes work non-deterministically. For prod, externalize the queue.
- **Reference-data seeding on startup.** Idempotent (upserts) and small, so safe. For larger datasets this belongs in a migration tool.

## Limitations

- No metrics/tracing wiring; logs only. (OpenTelemetry would slot in at `Program.cs`.)
- No rate limiting on ingestion endpoints.
- DLQ replay is read-only — there is no admin endpoint to re-queue. (Designed for; not implemented for scope.)
- Frontend is intentionally small per the brief's "do not build a large frontend" guidance — three read-only pages plus one developer-facing **Vendor Service** page that posts to the existing ingestion endpoints. No styling library, no authentication on the read views.

## What I'd do next for production

1. **Externalize the queue** (RabbitMQ / Azure Service Bus / SQS) so the worker scales horizontally and survives API restarts mid-batch.
2. **OpenTelemetry** end-to-end: trace propagation from ingestion → worker → repository → response.
3. **Schema versioning** per vendor via a discriminator field; current normalizers tolerate extras but not breaking changes.
4. **Rate limiting & request signing** on ingestion endpoints.
5. **Audit log** for derived-status changes (which event flipped the machine into Critical?).
6. **Admin endpoints** for DLQ replay and manual reprocessing.
7. **Authorization on read endpoints** (currently open) once the downstream consumer model is known.
8. **CI**: GitHub Actions for build + test + Docker push.

## AI Usage Disclosure

I used Claude (Anthropic) as a coding assistant while building this project.

**Where AI helped:**
- Writing repetitive boilerplate (project files, DI registrations, controller skeletons).
- Cleaning up the README and code comments.
- Suggesting test cases for the derived-status rules.

**What I decided myself:**
- The overall architecture — Clean Architecture with Domain, Application, Infrastructure, and API layers.
- Using the Factory pattern so new vendors plug in with minimal changes.
- Running async work in-process with a Channels-backed worker instead of adding a message broker.
- How idempotency and concurrency are handled.
- Which MongoDB indexes to create.
- The rules and reason codes that decide if a machine needs attention.

I can walk through and explain any part of this submission in discussion.
