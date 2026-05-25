# Design Note

A short narrative of the decisions behind this submission.

## Goals (from the brief, ranked)

1. Backend engineering quality, normalization of messy vendor data, async processing.
2. API clarity and explainable derived output.
3. Production awareness (testing, observability hooks, idempotency, DLQ).
4. Ease of running and reviewing — Docker Compose, Scalar API explorer, Postman, and a frontend "Vendor Service" page with 30 pre-built scenarios.
5. Minimal frontend (the brief explicitly says: do not build a large frontend).

## Boundaries

Clean Architecture with four projects:

- `Domain` — pure POCO. No JSON, no Mongo, no ASP.NET. This is what every other layer agrees on.
- `Application` — orchestrates use cases. Defines persistence interfaces. Owns normalizers, factory, calculator, queue interface, worker. No Mongo.
- `Infrastructure` — only place that knows about Mongo. Implements the persistence interfaces. Owns seeders and class maps.
- `Api` — ASP.NET Core: controllers, middleware, OpenAPI (Swashbuckle) + Scalar UI, DI composition root.

Why this matters for review:
- Swapping persistence (SQL, EF, whatever) is an Infrastructure change. The Application doesn't notice.
- Adding a vendor is one `INormalizer` implementation + one `services.AddSingleton<INormalizer, X>()` line. Nothing else changes — Open-Closed in practice.

## Vendor normalization

Three vendors are present:

| Vendor | Timestamps | Vibration | Temperature | Power | Severity |
| --- | --- | --- | --- | --- | --- |
| PulseForge | ISO 8601 UTC | mm/s (canonical) | °C (canonical) | — | string `low|medium|high|critical` |
| ThermexWatch | epoch ms | g (converted via `v = g · 9806.65 / (2πf)` with f=10 Hz reference) | °F (converted) | kW | integer level 1–5 |
| MaintaFlow | `yyyy/MM/dd HH:mm:ss` (treated UTC) | none | none | none | derived from `maintenance_status` / `inspection_result` |

The normalizer interface returns both `events` and `issues`. Partial successes are first-class: a batch with one bad record still yields the good events plus a `PartiallySucceeded` state with explanatory text.

### Unit conversion notes

- **g → mm/s** is frequency-dependent. The vendor does not transmit frequency. We use a 10 Hz reference and document that assumption. If the vendor ever sends frequency, swap to per-event computation in one place.
- **°F → °C** is mechanical; rounded to 4 decimals to keep test assertions tight.
- All event times are stored UTC.

## Idempotency

Two layers:

1. **Inbound** — `raw_payloads.idempotency_key` is `UNIQUE`. Caller can pass `X-Idempotency-Key`. If absent we derive `sha256(vendor || body)`. Duplicate requests collapse to the existing record and return `200 OK` (not `202`).
2. **Inner** — `normalized_events.idempotency_key = "{vendor}:{vendor_event_id}"` is `UNIQUE`. Bulk insert is unordered: duplicates fail, the rest insert. We trust the dedup-by-index over application-side checks because it survives concurrent retries cleanly.

## Async processing

We use a bounded `System.Threading.Channels.Channel<ProcessingWorkItem>` plus a `BackgroundService`. The producer is the ingestion endpoint; the consumer is the worker. Each work item runs in its own DI scope so scoped repositories and Mongo handles stay tidy.

Retry policy: `2s → 8s → 30s`, three attempts, then dead-letter. Retryable failures only come from explicitly classified transient exceptions; malformed JSON is never retried.

Why no broker?
- The brief explicitly asks to prefer simple and well-reasoned over over-engineered.
- The contract is the `IProcessingQueue` interface. A `RabbitMqProcessingQueue` is a future implementation, not a redesign.

## Derived attention view

Rules are deterministic and explainable. Each rule contributes a constant `ReasonCode`. The calculator:

1. Returns `Stale` if the most recent event is > 48h old (overrides everything).
2. Records `UnderMaintenance` if the asset's `asset_status == "maintenance"` and no critical event exists.
3. Walks the 24-hour window and bumps the attention level by the most severe matched rule.
4. Honors vendor-reported severity as a floor — we never silently downgrade a critical alert.
5. Acknowledges recovery signals so machines don't get stuck in elevated states forever, but only when the most recent event is the recovery and the prior pile-up was mild.

The view records `source_event_refs` so the downstream consumer can audit which events drove the state. The view's `version` field powers optimistic concurrency in the repo.

## API design

- `POST /api/ingestion/{vendor}` — raw body; preserved verbatim into Mongo.
- `GET /api/processing/status/{id}` and `/recent` — for accept-side observability.
- `GET /api/processing/dead-letters` — for failure-side observability.
- `GET /api/machines` (filters: plant, line, status, minAttention) and `/{id}/view`, `/{id}/events`.
- `GET /api/plants` and `/{plantId}/summary`, `/{plantId}/lines/{lineId}/summary`.

CamelCase JSON, enums as strings, ISO 8601 timestamps. Errors are JSON with a stable `error` code.

## Testing strategy

- **Unit:** every normalizer (happy path, missing fields, unit conversions, enum mapping), `NormalizerFactory` registration, `DerivedStatusCalculator` rule table (vibration threshold, critical override, recovery semantics, stale detection, maintenance overdue, asset-status maintenance).
- **Integration:** API surface with a real ephemeral MongoDB:
  - Unauthorized request without API key gets `401`.
  - Authorized POST gets `202 Accepted` and queues the payload.
  - Replay of the same body gets `200 OK` with `duplicate: true`.

The integration test boots the actual `Program` with overridden configuration pointing at an EphemeralMongo runner.

## Production hardening (in priority order)

1. Externalize the queue (RabbitMQ / SQS / Service Bus) for horizontal-scale workers and persistence.
2. OpenTelemetry: trace propagation from HTTP → worker → Mongo.
3. Per-vendor schema versioning header (`X-Vendor-Schema-Version`) so we can ship breaking changes safely.
4. Admin endpoints: DLQ inspect + replay; manual reprocess of a specific raw payload.
5. Read-side authorization scoped by tenant / plant.
6. Rate limiting at the gateway / API layer.
7. CI for build + test + container publish.

## What's not in scope

- ML-based attention scoring. The brief states deterministic, explainable logic is preferred.
- A full frontend. The brief says not to build one. The included read-only dashboard exists only so a reviewer can see the system end-to-end.
- Multi-tenant isolation. Single-deployment-per-customer model assumed.
