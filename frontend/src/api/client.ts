export type MachineView = {
  machine_id: string;
  plant_id: string;
  line_id: string;
  derived_status: string;
  attention_level: string;
  needs_attention: boolean;
  reason_codes: string[];
  latest_relevant_event_time: string | null;
  processing_status: string;
  source_event_refs: string[];
  last_processed_at: string | null;
  version: number;
};

export type LineSummaryRow = {
  line_id: string;
  total: number;
  needing_attention: number;
  critical: number;
  stale: number;
};

export type CriticalMachineRow = {
  machine_id: string;
  line_id: string;
  reason_codes: string[];
};

export type PlantSummary = {
  plant_id: string;
  total_machines: number;
  needing_attention: number;
  status_counts: Record<string, number>;
  lines: LineSummaryRow[];
  critical_machines: CriticalMachineRow[];
  has_data: boolean;
};

export type PlantLine = {
  line_id: string;
  line_name: string | null;
  operating_window: string | null;
};

export type Plant = {
  plant_id: string;
  machine_count: number;
  lines: PlantLine[];
};

export type ProcessingStatusRow = {
  raw_payload_id: string;
  vendor: string;
  state: string;
  received_at: string;
  last_attempt_at: string | null;
  attempt_count: number;
  failure_reason: string | null;
  record_count: number;
};

export type DeadLetterRow = {
  id: string;
  raw_payload_id: string;
  vendor: string;
  dead_lettered_at: string;
  attempt_count: number;
  reason: string;
};

export type NormalizedEvent = {
  id: string;
  vendor: string;
  vendor_event_id: string;
  vendor_event_code: string | null;
  canonical_type: string;
  severity_hint: string;
  event_time: string;
  vibration_mm_s: number | null;
  temperature_c: number | null;
  power_kw: number | null;
  sensor_health: number | null;
  note: string | null;
  maintenance_status: string | null;
  inspection_result: string | null;
};

async function get<T>(path: string): Promise<T> {
  const res = await fetch(path);
  if (!res.ok) {
    throw new Error(`${res.status} ${res.statusText} at ${path}`);
  }
  return res.json() as Promise<T>;
}

export type IngestResponse = {
  raw_payload_id: string;
  state: string;
  duplicate: boolean;
  record_count: number;
  idempotency_key: string;
};

export type IngestResult = {
  status: number;
  statusText: string;
  body: unknown;
};

export type VendorSlug = "pulseforge" | "thermexwatch" | "maintaflow";

export async function ingestPayload(
  vendor: VendorSlug,
  apiKey: string,
  body: string,
  idempotencyKey?: string,
): Promise<IngestResult> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    "X-Vendor-Api-Key": apiKey,
  };
  if (idempotencyKey) headers["X-Idempotency-Key"] = idempotencyKey;

  const res = await fetch(`/api/ingestion/${vendor}`, { method: "POST", headers, body });
  const text = await res.text();
  let parsed: unknown;
  try {
    parsed = text ? JSON.parse(text) : null;
  } catch {
    parsed = text;
  }
  return { status: res.status, statusText: res.statusText, body: parsed };
}

export const api = {
  listMachines: (params?: { plantId?: string; lineId?: string; status?: string; minAttention?: string }) => {
    const q = new URLSearchParams();
    if (params?.plantId) q.set("plantId", params.plantId);
    if (params?.lineId) q.set("lineId", params.lineId);
    if (params?.status) q.set("status", params.status);
    if (params?.minAttention) q.set("minAttention", params.minAttention);
    return get<MachineView[]>(`/api/machines?${q.toString()}`);
  },
  getMachineView: (machineId: string) => get<MachineView>(`/api/machines/${machineId}/view`),
  getMachineEvents: (machineId: string) => get<NormalizedEvent[]>(`/api/machines/${machineId}/events`),
  listPlants: () => get<Plant[]>(`/api/plants`),
  getPlantSummary: (plantId: string) => get<PlantSummary>(`/api/plants/${plantId}/summary`),
  getProcessingRecent: () => get<ProcessingStatusRow[]>(`/api/processing/recent`),
  getDeadLetters: () => get<DeadLetterRow[]>(`/api/processing/dead-letters`),
};
