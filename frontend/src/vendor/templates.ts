import type { VendorSlug } from "../api/client";

export type ScenarioCategory =
  | "Healthy / Nominal"
  | "Warning"
  | "Critical / Alert"
  | "Recovery"
  | "Maintenance"
  | "Inspection"
  | "Operator note"
  | "Calibration"
  | "Multi-record batch"
  | "Different plant"
  | "Edge case";

export type VendorTemplate = {
  id: string;
  label: string;
  category: ScenarioCategory;
  description: string;
  /** Expected effect on the derived machine view — shown to the user as a hint. */
  expectedEffect: string;
  /** Build the JSON body. `now` lets timestamps be rewritten to the current time. */
  build: (now: Date, useCurrentTime: boolean) => string;
};

export type VendorMeta = {
  slug: VendorSlug;
  name: string;
  defaultApiKey: string;
  endpoint: string;
  templates: VendorTemplate[];
};

const pulseForgeFixedTime = "2026-04-18T07:58:01Z";
const thermexWatchFixedMs = 1776499152000;
const maintaFlowFixedTime = "2026/04/18 08:14:05";

function maintaFlowFormat(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getUTCFullYear()}/${pad(d.getUTCMonth() + 1)}/${pad(d.getUTCDate())} ${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:${pad(d.getUTCSeconds())}`;
}

const stringify = (obj: unknown) => JSON.stringify(obj, null, 2);
const uniqueId = (prefix: string) => `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
const pfTime = (now: Date, useNow: boolean) =>
  (useNow ? now : new Date(pulseForgeFixedTime)).toISOString();
const twTime = (now: Date, useNow: boolean) => (useNow ? now.getTime() : thermexWatchFixedMs);
const mfTime = (now: Date, useNow: boolean) => (useNow ? maintaFlowFormat(now) : maintaFlowFixedTime);

// ────────────────────────────────────────────────────────────────────
// PulseForge scenarios
// ────────────────────────────────────────────────────────────────────
const pulseForgeTemplates: VendorTemplate[] = [
  {
    id: "pf_healthy",
    label: "Nominal reading (within all thresholds)",
    category: "Healthy / Nominal",
    description: "EQ-001 — vibration 4.2 mm/s and 65°C, both well below rated max. Severity low.",
    expectedEffect: "Machine stays Healthy, no reason codes added.",
    build: (now, useNow) =>
      stringify({
        vendor: "PulseForge",
        plant_id: "PLANT_01",
        batch_generated_at: pfTime(now, useNow),
        events: [
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-001",
            line_id: "LINE-A",
            event_time: pfTime(now, useNow),
            event_type: "HIGH_VIBRATION",
            severity: "low",
            vibration_mm_s: 4.2,
            temperature_c: 65.0,
            machine_state: "running",
            sensor_health: 0.96,
            vendor_confidence: 0.92,
          },
        ],
      }),
  },
  {
    id: "pf_vib_warning",
    label: "Vibration warning (just under threshold)",
    category: "Warning",
    description: "EQ-001 vibration 8.7 mm/s vs rated max 9.0 — severity medium.",
    expectedEffect: "Attention Moderate. No VIBRATION_OVER_THRESHOLD (still under rated max).",
    build: (now, useNow) =>
      stringify({
        vendor: "PulseForge",
        plant_id: "PLANT_01",
        batch_generated_at: pfTime(now, useNow),
        events: [
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-001",
            line_id: "LINE-A",
            event_time: pfTime(now, useNow),
            event_type: "HIGH_VIBRATION",
            severity: "medium",
            vibration_mm_s: 8.7,
            temperature_c: 78.0,
            machine_state: "running",
            sensor_health: 0.9,
            vendor_confidence: 0.85,
          },
        ],
      }),
  },
  {
    id: "pf_vib_above_threshold",
    label: "Vibration ABOVE rated max",
    category: "Critical / Alert",
    description: "EQ-001 vibration 11.8 mm/s vs rated 9.0 — severity high.",
    expectedEffect: "AtRisk + reasons: VIBRATION_OVER_THRESHOLD, VENDOR_REPORTED_HIGH.",
    build: (now, useNow) =>
      stringify({
        vendor: "PulseForge",
        plant_id: "PLANT_01",
        batch_generated_at: pfTime(now, useNow),
        events: [
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-001",
            line_id: "LINE-A",
            event_time: pfTime(now, useNow),
            event_type: "HIGH_VIBRATION",
            severity: "high",
            vibration_mm_s: 11.8,
            temperature_c: 83.2,
            machine_state: "running",
            sensor_health: 0.91,
            vendor_confidence: 0.87,
          },
        ],
      }),
  },
  {
    id: "pf_temp_critical",
    label: "Temperature spike (critical)",
    category: "Critical / Alert",
    description: "EQ-002 temperature 96.4°C vs rated 90°C, severity critical.",
    expectedEffect: "Critical status + reasons: TEMPERATURE_OVER_THRESHOLD, VENDOR_REPORTED_CRITICAL.",
    build: (now, useNow) =>
      stringify({
        vendor: "PulseForge",
        plant_id: "PLANT_01",
        batch_generated_at: pfTime(now, useNow),
        events: [
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-002",
            line_id: "LINE-A",
            event_time: pfTime(now, useNow),
            event_type: "TEMP_SPIKE",
            severity: "critical",
            vibration_mm_s: 7.1,
            temperature_c: 96.4,
            machine_state: "running",
            sensor_health: 0.76,
            vendor_confidence: 0.81,
          },
        ],
      }),
  },
  {
    id: "pf_sensor_drop",
    label: "Sensor health degraded",
    category: "Warning",
    description: "EQ-003 sensor health drops to 0.45 — sensor reliability is questionable.",
    expectedEffect: "Adds SENSOR_HEALTH_LOW at Low attention.",
    build: (now, useNow) =>
      stringify({
        vendor: "PulseForge",
        plant_id: "PLANT_01",
        batch_generated_at: pfTime(now, useNow),
        events: [
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-003",
            line_id: "LINE-B",
            event_time: pfTime(now, useNow),
            event_type: "SENSOR_HEALTH_DROP",
            severity: "medium",
            vibration_mm_s: 5.5,
            temperature_c: 70.0,
            machine_state: "running",
            sensor_health: 0.45,
            vendor_confidence: 0.4,
          },
        ],
      }),
  },
  {
    id: "pf_power_fluct",
    label: "Power fluctuation",
    category: "Warning",
    description: "EQ-004 — power instability event from PulseForge.",
    expectedEffect: "POWER_FLUCTUATION event, severity medium.",
    build: (now, useNow) =>
      stringify({
        vendor: "PulseForge",
        plant_id: "PLANT_01",
        batch_generated_at: pfTime(now, useNow),
        events: [
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-004",
            line_id: "LINE-C",
            event_time: pfTime(now, useNow),
            event_type: "POWER_FLUCTUATION",
            severity: "medium",
            vibration_mm_s: 6.0,
            temperature_c: 72.0,
            machine_state: "running",
            sensor_health: 0.85,
            vendor_confidence: 0.78,
          },
        ],
      }),
  },
  {
    id: "pf_recovery",
    label: "Recovery signal",
    category: "Recovery",
    description: "EQ-001 readings return to baseline. RECOVERY_SIGNAL, severity low.",
    expectedEffect: "Adds RECOVERY_OBSERVED. May downgrade attention if previous events were mild.",
    build: (now, useNow) =>
      stringify({
        vendor: "PulseForge",
        plant_id: "PLANT_01",
        batch_generated_at: pfTime(now, useNow),
        events: [
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-001",
            line_id: "LINE-A",
            event_time: pfTime(now, useNow),
            event_type: "RECOVERY_SIGNAL",
            severity: "low",
            vibration_mm_s: 2.1,
            temperature_c: 60.4,
            machine_state: "running",
            sensor_health: 0.97,
            vendor_confidence: 0.94,
          },
        ],
      }),
  },
  {
    id: "pf_batch_three",
    label: "Batch — 3 mixed events",
    category: "Multi-record batch",
    description: "One POST with 3 events: EQ-001 high vibration, EQ-002 temp critical, EQ-003 recovery.",
    expectedEffect: "Three machine views recomputed in one call.",
    build: (now, useNow) =>
      stringify({
        vendor: "PulseForge",
        plant_id: "PLANT_01",
        batch_generated_at: pfTime(now, useNow),
        events: [
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-001",
            line_id: "LINE-A",
            event_time: pfTime(now, useNow),
            event_type: "HIGH_VIBRATION",
            severity: "high",
            vibration_mm_s: 11.0,
            temperature_c: 82.0,
            machine_state: "running",
            sensor_health: 0.88,
            vendor_confidence: 0.86,
          },
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-002",
            line_id: "LINE-A",
            event_time: pfTime(now, useNow),
            event_type: "TEMP_SPIKE",
            severity: "critical",
            vibration_mm_s: 6.2,
            temperature_c: 95.0,
            machine_state: "running",
            sensor_health: 0.74,
            vendor_confidence: 0.8,
          },
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-003",
            line_id: "LINE-B",
            event_time: pfTime(now, useNow),
            event_type: "RECOVERY_SIGNAL",
            severity: "low",
            vibration_mm_s: 2.5,
            temperature_c: 62.0,
            machine_state: "running",
            sensor_health: 0.95,
            vendor_confidence: 0.91,
          },
        ],
      }),
  },
  {
    id: "pf_plant2",
    label: "Event for PLANT_02 (EQ-005)",
    category: "Different plant",
    description: "Sends an event for the Mixer in plant 2.",
    expectedEffect: "PLANT_02 / LINE-D dashboard updates.",
    build: (now, useNow) =>
      stringify({
        vendor: "PulseForge",
        plant_id: "PLANT_02",
        batch_generated_at: pfTime(now, useNow),
        events: [
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-005",
            line_id: "LINE-D",
            event_time: pfTime(now, useNow),
            event_type: "HIGH_VIBRATION",
            severity: "high",
            vibration_mm_s: 9.5,
            temperature_c: 86.0,
            machine_state: "running",
            sensor_health: 0.82,
            vendor_confidence: 0.79,
          },
        ],
      }),
  },
  {
    id: "pf_partial_bad_record",
    label: "Batch with one bad record (missing event_id)",
    category: "Edge case",
    description: "Two events — first missing event_id, second valid. Tests partial-success handling.",
    expectedEffect: "raw_payload state becomes PartiallySucceeded. 1 event normalized, 1 issue reported.",
    build: (now, useNow) =>
      stringify({
        vendor: "PulseForge",
        plant_id: "PLANT_01",
        batch_generated_at: pfTime(now, useNow),
        events: [
          {
            event_id: null,
            machine_id: "EQ-001",
            line_id: "LINE-A",
            event_time: pfTime(now, useNow),
            event_type: "HIGH_VIBRATION",
            severity: "high",
          },
          {
            event_id: uniqueId("PF"),
            machine_id: "EQ-001",
            line_id: "LINE-A",
            event_time: pfTime(now, useNow),
            event_type: "TEMP_SPIKE",
            severity: "high",
            vibration_mm_s: 6.8,
            temperature_c: 88.0,
            machine_state: "running",
            sensor_health: 0.9,
            vendor_confidence: 0.86,
          },
        ],
      }),
  },
];

// ────────────────────────────────────────────────────────────────────
// ThermexWatch scenarios
// ────────────────────────────────────────────────────────────────────
const thermexWatchTemplates: VendorTemplate[] = [
  {
    id: "tw_ok",
    label: "OK / nominal (level 1)",
    category: "Healthy / Nominal",
    description: "EQ-006 — clear reading, all parameters within normal range.",
    expectedEffect: "Stays Healthy. No reason codes.",
    build: (now, useNow) =>
      stringify({
        source: "ThermexWatch",
        site_code: "PLANT_02",
        response_time_epoch_ms: twTime(now, useNow),
        readings: [
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-006",
            productionLine: "D",
            timestampMs: twTime(now, useNow),
            alertCode: "OK",
            level: 1,
            vibration_g: 0.09,
            temperature_f: 132.0,
            power_kw: 11.8,
            is_active: true,
            signal_quality: "GOOD",
          },
        ],
      }),
  },
  {
    id: "tw_vib_level2",
    label: "Vibration warning — level 2 (low)",
    category: "Warning",
    description: "VIB_WARN level 2 — vendor sees mild vibration. Sent in g, converted internally.",
    expectedEffect: "Attention Low.",
    build: (now, useNow) =>
      stringify({
        source: "ThermexWatch",
        site_code: "PLANT_01",
        response_time_epoch_ms: twTime(now, useNow),
        readings: [
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-001",
            productionLine: "A",
            timestampMs: twTime(now, useNow),
            alertCode: "VIB_WARN",
            level: 2,
            vibration_g: 0.18,
            temperature_f: 165.0,
            power_kw: 35.5,
            is_active: true,
            signal_quality: "GOOD",
          },
        ],
      }),
  },
  {
    id: "tw_vib_level3",
    label: "Vibration warning — level 3 (moderate)",
    category: "Warning",
    description: "VIB_WARN level 3. Sets attention Moderate.",
    expectedEffect: "Degraded status, attention Moderate.",
    build: (now, useNow) =>
      stringify({
        source: "ThermexWatch",
        site_code: "PLANT_01",
        response_time_epoch_ms: twTime(now, useNow),
        readings: [
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-001",
            productionLine: "A",
            timestampMs: twTime(now, useNow),
            alertCode: "VIB_WARN",
            level: 3,
            vibration_g: 0.42,
            temperature_f: 171.0,
            power_kw: 36.5,
            is_active: true,
            signal_quality: "GOOD",
          },
        ],
      }),
  },
  {
    id: "tw_vib_level4",
    label: "Vibration warning — level 4 (high)",
    category: "Critical / Alert",
    description: "VIB_WARN level 4 on EQ-001. Vibration_g 0.81 ≈ 12.6 mm/s (above rated 9.0).",
    expectedEffect: "AtRisk + VIBRATION_OVER_THRESHOLD + VENDOR_REPORTED_HIGH.",
    build: (now, useNow) =>
      stringify({
        source: "ThermexWatch",
        site_code: "PLANT_01",
        response_time_epoch_ms: twTime(now, useNow),
        readings: [
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-001",
            productionLine: "A",
            timestampMs: twTime(now, useNow),
            alertCode: "VIB_WARN",
            level: 4,
            vibration_g: 0.81,
            temperature_f: 181.2,
            power_kw: 37.8,
            is_active: true,
            signal_quality: "GOOD",
          },
        ],
      }),
  },
  {
    id: "tw_temp_crit",
    label: "Temperature CRITICAL — level 5",
    category: "Critical / Alert",
    description: "TEMP_CRIT level 5 on EQ-003. 195°F ≈ 90.6°C, above rated 82°C.",
    expectedEffect: "Critical + TEMPERATURE_OVER_THRESHOLD + VENDOR_REPORTED_CRITICAL.",
    build: (now, useNow) =>
      stringify({
        source: "ThermexWatch",
        site_code: "PLANT_01",
        response_time_epoch_ms: twTime(now, useNow),
        readings: [
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-003",
            productionLine: "B",
            timestampMs: twTime(now, useNow),
            alertCode: "TEMP_CRIT",
            level: 5,
            vibration_g: 0.42,
            temperature_f: 195.0,
            power_kw: 18.5,
            is_active: true,
            signal_quality: "GOOD",
          },
        ],
      }),
  },
  {
    id: "tw_temp_warn",
    label: "Temperature warning — level 3",
    category: "Warning",
    description: "TEMP_WARN level 3, temperature elevated but within machine limits.",
    expectedEffect: "Attention Moderate, no TEMPERATURE_OVER_THRESHOLD (still below rated).",
    build: (now, useNow) =>
      stringify({
        source: "ThermexWatch",
        site_code: "PLANT_02",
        response_time_epoch_ms: twTime(now, useNow),
        readings: [
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-008",
            productionLine: "E",
            timestampMs: twTime(now, useNow),
            alertCode: "TEMP_WARN",
            level: 3,
            vibration_g: 0.32,
            temperature_f: 178.0,
            power_kw: 32.0,
            is_active: true,
            signal_quality: "FAIR",
          },
        ],
      }),
  },
  {
    id: "tw_power_drop",
    label: "Power drop — abnormal power reduction",
    category: "Warning",
    description: "POWER_DROP level 3 — power drops from baseline 36 kW to 12 kW (66% deviation).",
    expectedEffect: "Adds POWER_ANOMALY (>30% deviation). Attention Moderate.",
    build: (now, useNow) =>
      stringify({
        source: "ThermexWatch",
        site_code: "PLANT_01",
        response_time_epoch_ms: twTime(now, useNow),
        readings: [
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-001",
            productionLine: "A",
            timestampMs: twTime(now, useNow),
            alertCode: "POWER_DROP",
            level: 3,
            vibration_g: 0.15,
            temperature_f: 158.0,
            power_kw: 12.0,
            is_active: true,
            signal_quality: "GOOD",
          },
        ],
      }),
  },
  {
    id: "tw_batch_four",
    label: "Batch — 4 readings",
    category: "Multi-record batch",
    description: "Single POST with 4 readings touching multiple machines on PLANT_01.",
    expectedEffect: "Four machine views recomputed in one call.",
    build: (now, useNow) =>
      stringify({
        source: "ThermexWatch",
        site_code: "PLANT_01",
        response_time_epoch_ms: twTime(now, useNow),
        readings: [
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-001",
            productionLine: "A",
            timestampMs: twTime(now, useNow),
            alertCode: "VIB_WARN",
            level: 4,
            vibration_g: 0.78,
            temperature_f: 180.0,
            power_kw: 38.0,
            is_active: true,
            signal_quality: "GOOD",
          },
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-002",
            productionLine: "A",
            timestampMs: twTime(now, useNow),
            alertCode: "TEMP_WARN",
            level: 3,
            vibration_g: 0.22,
            temperature_f: 188.0,
            power_kw: 29.5,
            is_active: true,
            signal_quality: "GOOD",
          },
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-003",
            productionLine: "B",
            timestampMs: twTime(now, useNow),
            alertCode: "OK",
            level: 1,
            vibration_g: 0.12,
            temperature_f: 138.0,
            power_kw: 18.2,
            is_active: true,
            signal_quality: "FAIR",
          },
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-004",
            productionLine: "C",
            timestampMs: twTime(now, useNow),
            alertCode: "POWER_DROP",
            level: 3,
            vibration_g: 0.18,
            temperature_f: 162.0,
            power_kw: 14.0,
            is_active: true,
            signal_quality: "GOOD",
          },
        ],
      }),
  },
  {
    id: "tw_plant2",
    label: "Plant 2 — Packager OK (EQ-006)",
    category: "Different plant",
    description: "Routine reading on EQ-006 in PLANT_02.",
    expectedEffect: "PLANT_02 dashboard reflects EQ-006 as Healthy.",
    build: (now, useNow) =>
      stringify({
        source: "ThermexWatch",
        site_code: "PLANT_02",
        response_time_epoch_ms: twTime(now, useNow),
        readings: [
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-006",
            productionLine: "D",
            timestampMs: twTime(now, useNow),
            alertCode: "OK",
            level: 1,
            vibration_g: 0.09,
            temperature_f: 132.0,
            power_kw: 11.8,
            is_active: true,
            signal_quality: "GOOD",
          },
        ],
      }),
  },
  {
    id: "tw_missing_ts",
    label: "Reading with missing timestampMs",
    category: "Edge case",
    description: "Two readings — first missing timestampMs, second valid.",
    expectedEffect: "PartiallySucceeded. 1 event in, 1 issue logged.",
    build: (now, useNow) =>
      stringify({
        source: "ThermexWatch",
        site_code: "PLANT_01",
        response_time_epoch_ms: twTime(now, useNow),
        readings: [
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-001",
            productionLine: "A",
            timestampMs: null,
            alertCode: "OK",
            level: 1,
            vibration_g: 0.1,
            temperature_f: 140.0,
            power_kw: 30.0,
            is_active: true,
            signal_quality: "GOOD",
          },
          {
            readingId: uniqueId("TW"),
            assetCode: "EQ-001",
            productionLine: "A",
            timestampMs: twTime(now, useNow),
            alertCode: "VIB_WARN",
            level: 3,
            vibration_g: 0.4,
            temperature_f: 170.0,
            power_kw: 35.0,
            is_active: true,
            signal_quality: "GOOD",
          },
        ],
      }),
  },
];

// ────────────────────────────────────────────────────────────────────
// MaintaFlow scenarios
// ────────────────────────────────────────────────────────────────────
const maintaFlowTemplates: VendorTemplate[] = [
  {
    id: "mf_inspect_passed",
    label: "Inspection — passed (no defects)",
    category: "Inspection",
    description: "EQ-001 inspection_result=passed_no_defects.",
    expectedEffect: "No new reason codes.",
    build: (now, useNow) =>
      stringify({
        provider_name: "MaintaFlow",
        factory_id: "PLANT_01",
        records: [
          {
            record_id: uniqueId("MF"),
            machine_ref: "EQ-001",
            line_ref: "LINE-A",
            recorded_at: mfTime(now, useNow),
            record_type: "inspection",
            inspection_result: "passed_no_defects",
            maintenance_status: "not_due",
            days_since_last_service: 12,
            technician_note: "All systems nominal",
            manual_confidence: "high",
          },
        ],
      }),
  },
  {
    id: "mf_inspect_minor",
    label: "Inspection — minor defect",
    category: "Inspection",
    description: "EQ-001 inspection_result=minor_defect_found.",
    expectedEffect: "Adds INSPECTION_DEFECT at Moderate attention.",
    build: (now, useNow) =>
      stringify({
        provider_name: "MaintaFlow",
        factory_id: "PLANT_01",
        records: [
          {
            record_id: uniqueId("MF"),
            machine_ref: "EQ-001",
            line_ref: "LINE-A",
            recorded_at: mfTime(now, useNow),
            record_type: "inspection",
            inspection_result: "minor_defect_found",
            maintenance_status: "not_due",
            days_since_last_service: 18,
            technician_note: "Belt wear visible; machine still operational",
            manual_confidence: "medium",
          },
        ],
      }),
  },
  {
    id: "mf_inspect_major",
    label: "Inspection — MAJOR defect",
    category: "Inspection",
    description: "EQ-003 inspection_result=major_defect_found.",
    expectedEffect: "Adds INSPECTION_DEFECT.",
    build: (now, useNow) =>
      stringify({
        provider_name: "MaintaFlow",
        factory_id: "PLANT_01",
        records: [
          {
            record_id: uniqueId("MF"),
            machine_ref: "EQ-003",
            line_ref: "LINE-B",
            recorded_at: mfTime(now, useNow),
            record_type: "inspection",
            inspection_result: "major_defect_found",
            maintenance_status: "due_soon",
            days_since_last_service: 47,
            technician_note: "Cracked housing — service immediately",
            manual_confidence: "high",
          },
        ],
      }),
  },
  {
    id: "mf_maint_not_due",
    label: "Maintenance — not due",
    category: "Maintenance",
    description: "EQ-002 maintenance update, recent service.",
    expectedEffect: "No new reason codes.",
    build: (now, useNow) =>
      stringify({
        provider_name: "MaintaFlow",
        factory_id: "PLANT_01",
        records: [
          {
            record_id: uniqueId("MF"),
            machine_ref: "EQ-002",
            line_ref: "LINE-A",
            recorded_at: mfTime(now, useNow),
            record_type: "maintenance_update",
            inspection_result: null,
            maintenance_status: "not_due",
            days_since_last_service: 8,
            technician_note: "Last service was a week ago, all good",
            manual_confidence: "high",
          },
        ],
      }),
  },
  {
    id: "mf_maint_due_soon",
    label: "Maintenance — due soon",
    category: "Maintenance",
    description: "EQ-005 maintenance status: due_soon.",
    expectedEffect: "Adds MAINTENANCE_DUE_SOON, attention Low.",
    build: (now, useNow) =>
      stringify({
        provider_name: "MaintaFlow",
        factory_id: "PLANT_02",
        records: [
          {
            record_id: uniqueId("MF"),
            machine_ref: "EQ-005",
            line_ref: "LINE-D",
            recorded_at: mfTime(now, useNow),
            record_type: "maintenance_update",
            inspection_result: null,
            maintenance_status: "due_soon",
            days_since_last_service: 27,
            technician_note: "Plan service this week",
            manual_confidence: "medium",
          },
        ],
      }),
  },
  {
    id: "mf_maint_overdue",
    label: "Maintenance — OVERDUE (90 days)",
    category: "Maintenance",
    description: "EQ-004 maintenance status overdue + 90 days since last service.",
    expectedEffect: "Adds MAINTENANCE_OVERDUE. AtRisk, attention High.",
    build: (now, useNow) =>
      stringify({
        provider_name: "MaintaFlow",
        factory_id: "PLANT_01",
        records: [
          {
            record_id: uniqueId("MF"),
            machine_ref: "EQ-004",
            line_ref: "LINE-C",
            recorded_at: mfTime(now, useNow),
            record_type: "maintenance_update",
            inspection_result: null,
            maintenance_status: "overdue",
            days_since_last_service: 90,
            technician_note: "Scheduled service was missed",
            manual_confidence: "high",
          },
        ],
      }),
  },
  {
    id: "mf_operator_low_conf",
    label: "Operator note — low confidence",
    category: "Operator note",
    description: "Operator hears odd noise but isn't sure (manual_confidence=low).",
    expectedEffect: "Adds OPERATOR_CONCERN at Low attention.",
    build: (now, useNow) =>
      stringify({
        provider_name: "MaintaFlow",
        factory_id: "PLANT_01",
        records: [
          {
            record_id: uniqueId("MF"),
            machine_ref: "EQ-004",
            line_ref: "C",
            recorded_at: mfTime(now, useNow),
            record_type: "operator_note",
            inspection_result: null,
            maintenance_status: "unknown",
            days_since_last_service: null,
            technician_note: "Intermittent noise reported during warmup",
            manual_confidence: "low",
          },
        ],
      }),
  },
  {
    id: "mf_operator_high_conf",
    label: "Operator note — high confidence",
    category: "Operator note",
    description: "Experienced operator reports clear vibration (manual_confidence=high).",
    expectedEffect: "OPERATOR_CONCERN added.",
    build: (now, useNow) =>
      stringify({
        provider_name: "MaintaFlow",
        factory_id: "PLANT_01",
        records: [
          {
            record_id: uniqueId("MF"),
            machine_ref: "EQ-003",
            line_ref: "LINE-B",
            recorded_at: mfTime(now, useNow),
            record_type: "operator_note",
            inspection_result: null,
            maintenance_status: "unknown",
            days_since_last_service: null,
            technician_note: "Clear vibration felt by hand on housing — recommend immediate inspection",
            manual_confidence: "high",
          },
        ],
      }),
  },
  {
    id: "mf_calibration",
    label: "Calibration record",
    category: "Calibration",
    description: "EQ-008 calibration completed.",
    expectedEffect: "Routine calibration event stored; no reason codes added.",
    build: (now, useNow) =>
      stringify({
        provider_name: "MaintaFlow",
        factory_id: "PLANT_02",
        records: [
          {
            record_id: uniqueId("MF"),
            machine_ref: "EQ-008",
            line_ref: "LINE-E",
            recorded_at: mfTime(now, useNow),
            record_type: "calibration",
            inspection_result: null,
            maintenance_status: "not_due",
            days_since_last_service: 4,
            technician_note: "Sensor calibration completed, drift 0.3%",
            manual_confidence: "high",
          },
        ],
      }),
  },
  {
    id: "mf_plant2_overdue",
    label: "Plant 2 — EQ-007 overdue maintenance",
    category: "Different plant",
    description: "EQ-007 (Pump on PLANT_02 / LINE-E) overdue 120 days.",
    expectedEffect: "Asset_status is 'maintenance' so view stays UnderMaintenance; MAINTENANCE_OVERDUE recorded.",
    build: (now, useNow) =>
      stringify({
        provider_name: "MaintaFlow",
        factory_id: "PLANT_02",
        records: [
          {
            record_id: uniqueId("MF"),
            machine_ref: "EQ-007",
            line_ref: "LINE-E",
            recorded_at: mfTime(now, useNow),
            record_type: "maintenance_update",
            inspection_result: null,
            maintenance_status: "overdue",
            days_since_last_service: 120,
            technician_note: "Pump still down — awaiting parts",
            manual_confidence: "high",
          },
        ],
      }),
  },
];

// ────────────────────────────────────────────────────────────────────
// Public registry
// ────────────────────────────────────────────────────────────────────
export const VENDORS: VendorMeta[] = [
  {
    slug: "pulseforge",
    name: "PulseForge",
    defaultApiKey: "pf-dev-key-change-me",
    endpoint: "/api/ingestion/pulseforge",
    templates: pulseForgeTemplates,
  },
  {
    slug: "thermexwatch",
    name: "ThermexWatch",
    defaultApiKey: "tw-dev-key-change-me",
    endpoint: "/api/ingestion/thermexwatch",
    templates: thermexWatchTemplates,
  },
  {
    slug: "maintaflow",
    name: "MaintaFlow",
    defaultApiKey: "mf-dev-key-change-me",
    endpoint: "/api/ingestion/maintaflow",
    templates: maintaFlowTemplates,
  },
];

/** Group templates by category, preserving the order they appear in the array. */
export function groupByCategory(templates: VendorTemplate[]): Map<ScenarioCategory, VendorTemplate[]> {
  const groups = new Map<ScenarioCategory, VendorTemplate[]>();
  for (const t of templates) {
    const existing = groups.get(t.category);
    if (existing) existing.push(t);
    else groups.set(t.category, [t]);
  }
  return groups;
}
