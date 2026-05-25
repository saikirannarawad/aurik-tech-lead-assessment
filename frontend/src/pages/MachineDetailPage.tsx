import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { api } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";

export function MachineDetailPage() {
  const { machineId = "" } = useParams();
  const viewQ = useQuery({
    queryKey: ["machine-view", machineId],
    queryFn: () => api.getMachineView(machineId),
    enabled: !!machineId,
  });
  const eventsQ = useQuery({
    queryKey: ["machine-events", machineId],
    queryFn: () => api.getMachineEvents(machineId),
    enabled: !!machineId,
  });

  return (
    <>
      <p>
        <Link to="/dashboard">← Dashboard</Link>
      </p>
      <h1>Machine {machineId}</h1>

      {viewQ.isError && <div className="error">No operational view yet for this machine.</div>}

      {viewQ.data && (
        <div className="card">
          <div className="row">
            <div className="kpi">
              <div className="v">
                <StatusBadge status={viewQ.data.derived_status} />
              </div>
              <div className="l">Status</div>
            </div>
            <div className="kpi">
              <div className="v">{viewQ.data.attention_level}</div>
              <div className="l">Attention</div>
            </div>
            <div className="kpi">
              <div className="v">{viewQ.data.plant_id}</div>
              <div className="l">Plant</div>
            </div>
            <div className="kpi">
              <div className="v">{viewQ.data.line_id}</div>
              <div className="l">Line</div>
            </div>
            <div className="kpi">
              <div className="v">{viewQ.data.version}</div>
              <div className="l">Version</div>
            </div>
          </div>
          <h2>Reason codes</h2>
          <div>
            {viewQ.data.reason_codes.length === 0 && <span className="muted">None</span>}
            {viewQ.data.reason_codes.map((r) => (
              <span className="tag" key={r}>
                {r}
              </span>
            ))}
          </div>
          <h2>Source event refs</h2>
          <div className="muted">
            {viewQ.data.source_event_refs.length === 0 ? "—" : viewQ.data.source_event_refs.join(", ")}
          </div>
          <p className="muted">
            Latest relevant event:{" "}
            {viewQ.data.latest_relevant_event_time ? new Date(viewQ.data.latest_relevant_event_time).toLocaleString() : "—"}
            {" · "}
            Last processed: {viewQ.data.last_processed_at ? new Date(viewQ.data.last_processed_at).toLocaleString() : "—"}
          </p>
        </div>
      )}

      <h2>Recent normalized events</h2>
      <div className="card">
        {eventsQ.data && eventsQ.data.length === 0 && <div className="muted">No events.</div>}
        {eventsQ.data && eventsQ.data.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Time (UTC)</th>
                <th>Vendor</th>
                <th>Code</th>
                <th>Canonical</th>
                <th>Severity</th>
                <th>Vib (mm/s)</th>
                <th>Temp (°C)</th>
                <th>Power (kW)</th>
              </tr>
            </thead>
            <tbody>
              {eventsQ.data.map((e) => (
                <tr key={e.id}>
                  <td>{new Date(e.event_time).toISOString()}</td>
                  <td>{e.vendor}</td>
                  <td>{e.vendor_event_code ?? "—"}</td>
                  <td>{e.canonical_type}</td>
                  <td>{e.severity_hint}</td>
                  <td>{e.vibration_mm_s?.toFixed(2) ?? "—"}</td>
                  <td>{e.temperature_c?.toFixed(2) ?? "—"}</td>
                  <td>{e.power_kw?.toFixed(2) ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
