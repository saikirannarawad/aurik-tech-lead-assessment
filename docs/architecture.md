# Architecture

## Overview

```mermaid
flowchart LR
    subgraph Vendors
        PF[PulseForge]
        TW[ThermexWatch]
        MF[MaintaFlow]
    end

    subgraph API["ASP.NET Core API (Aurik.Monitoring.Api)"]
        IC[IngestionController]
        PC[ProcessingController]
        MC[MachinesController]
        PLC[PlantsController]
        AUTH[ApiKeyAuthMiddleware]
    end

    subgraph App["Application layer"]
        ING[IngestionService]
        FAC[NormalizerFactory]
        NPF[PulseForgeNormalizer]
        NTW[ThermexWatchNormalizer]
        NMF[MaintaFlowNormalizer]
        Q[(Channel queue)]
        WK[NormalizationWorker]
        PP[PayloadProcessor]
        DSC[DerivedStatusCalculator]
    end

    subgraph Infra["Infrastructure layer"]
        RPR[(RawPayloadRepo)]
        NER[(NormalizedEventRepo)]
        MVR[(MachineViewRepo)]
        DLR[(DeadLetterRepo)]
        MR[(MachineRepo)]
        LR[(LineRepo)]
        SEED[ReferenceDataSeeder]
    end

    DB[(MongoDB)]

    PF & TW & MF --> AUTH --> IC
    IC --> ING --> RPR
    ING --> Q --> WK --> PP
    PP --> FAC
    FAC --> NPF & NTW & NMF
    PP --> NER
    PP --> MR
    PP --> DSC --> MVR
    WK --> DLR

    PC --> RPR & DLR
    MC --> MVR & NER & MR
    PLC --> MVR & MR & LR

    RPR & NER & MVR & DLR & MR & LR -.-> DB
    SEED --> MR & LR
```

## Sequence: vendor payload happy path

```mermaid
sequenceDiagram
    autonumber
    participant V as Vendor
    participant A as IngestionController
    participant S as IngestionService
    participant R as RawPayloadRepo
    participant Q as ChannelQueue
    participant W as NormalizationWorker
    participant P as PayloadProcessor
    participant F as NormalizerFactory
    participant N as VendorNormalizer
    participant E as NormalizedEventRepo
    participant D as DerivedStatusCalculator
    participant M as MachineViewRepo

    V->>A: POST /api/ingestion/{vendor}<br/>X-Vendor-Api-Key
    A->>S: AcceptAsync(vendor, body, idemKey)
    S->>R: InsertIfNotExistsAsync
    R-->>S: (payload, isNew)
    alt isNew
        S->>Q: EnqueueAsync(payloadId, attempt=1)
        S-->>A: 202 Accepted (Queued)
    else duplicate
        S-->>A: 200 OK (Duplicate)
    end
    A-->>V: response
    Note over W,P: Async, separate task
    Q-->>W: dequeued work item
    W->>P: ProcessAsync(payloadId, attempt)
    P->>R: GetAsync + UpdateState(Processing)
    P->>F: GetFor(vendor)
    F-->>P: INormalizer
    P->>N: Normalize(rawJson)
    N-->>P: NormalizationResult{events, issues}
    P->>E: InsertManyIdempotentAsync
    loop for each affected machine
        P->>M: GetAsync (current view)
        P->>E: GetSinceAsync (recent events)
        P->>D: Compute(machine, events)
        D-->>P: MachineOperationalView
        P->>M: UpsertWithVersionAsync(expected)
    end
    P->>R: UpdateState(Succeeded | PartiallySucceeded | Failed)
    alt retryable failure within attempts
        P-->>W: outcome.Retryable=true
        W->>Q: re-enqueue (attempt+1) after backoff
    else exhausted
        W->>R: UpdateState(DeadLettered)
        W->>DLQ: insert DeadLetterEntry
    end
```

## Mongo collections and indexes

| Collection | Key indexes | Why |
| --- | --- | --- |
| `raw_payloads` | `ux_raw_idempotency` (unique on idempotencyKey), `ix_raw_state_received` | Idempotent ingestion + DLQ / status queries |
| `normalized_events` | `ux_event_idempotency` (unique on idempotencyKey), `ix_event_machine_time` | Dedup vendor events across retries; fast per-machine windowed queries |
| `machines` | `ux_machine_id` | Single-doc lookups |
| `lines` | `ux_line_plant_line` | Unique line key |
| `machine_views` | `ux_view_machine` (unique on machineId), `ix_view_plant_line` | View read paths |
| `dead_letters` | (implicit `_id`) | Inspection only — low write rate |
