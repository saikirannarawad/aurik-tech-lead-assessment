import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { useState } from "react";
import { api } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";

export function DashboardPage() {
  const plantsQ = useQuery({ queryKey: ["plants"], queryFn: api.listPlants });
  const [plantId, setPlantId] = useState<string>("");

  const activePlantId = plantId || plantsQ.data?.[0]?.plant_id || "";

  const summaryQ = useQuery({
    queryKey: ["plant-summary", activePlantId],
    queryFn: () => api.getPlantSummary(activePlantId),
    enabled: !!activePlantId,
  });

  const machinesQ = useQuery({
    queryKey: ["machines", activePlantId],
    queryFn: () => api.listMachines({ plantId: activePlantId }),
    enabled: !!activePlantId,
  });

  return (
    <>
      <h1>Plant Dashboard</h1>

      <div className="card">
        <label>Plant: </label>
        <select value={activePlantId} onChange={(e) => setPlantId(e.target.value)}>
          {(plantsQ.data ?? []).map((p) => (
            <option key={p.plant_id} value={p.plant_id}>
              {p.plant_id} ({p.machine_count} machines)
            </option>
          ))}
        </select>
      </div>

      {summaryQ.data && !summaryQ.data.has_data && (
        <div className="card muted">
          No machine views yet for <strong>{activePlantId}</strong>. Submit vendor payloads via the
          ingestion API (or run <code>./scripts/load-samples.sh</code>) and they will appear here within a
          second.
        </div>
      )}

      {summaryQ.data && summaryQ.data.has_data && (
        <>
          <div className="card">
            <div className="row">
              <div className="kpi">
                <div className="v">{summaryQ.data.total_machines}</div>
                <div className="l">Total machines</div>
              </div>
              <div className="kpi">
                <div className="v">{summaryQ.data.needing_attention}</div>
                <div className="l">Needing attention</div>
              </div>
              {Object.entries(summaryQ.data.status_counts)
                .filter(([, n]) => n > 0)
                .map(([s, n]) => (
                  <div className="kpi" key={s}>
                    <div className="v">{n}</div>
                    <div className="l">{s}</div>
                  </div>
                ))}
            </div>
          </div>

          <h2>Lines</h2>
          <div className="card">
            <table>
              <thead>
                <tr>
                  <th>Line</th>
                  <th>Total</th>
                  <th>Attention</th>
                  <th>Critical</th>
                  <th>Stale</th>
                </tr>
              </thead>
              <tbody>
                {summaryQ.data.lines.map((l) => (
                  <tr key={l.line_id}>
                    <td>{l.line_id}</td>
                    <td>{l.total}</td>
                    <td>{l.needing_attention}</td>
                    <td>{l.critical}</td>
                    <td>{l.stale}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      <h2>Machines</h2>
      <div className="card">
        {machinesQ.isLoading && <div className="muted">Loading…</div>}
        {machinesQ.data && machinesQ.data.length === 0 && (
          <div className="muted">No machine views yet. Once vendor payloads are ingested and processed, they will appear here.</div>
        )}
        {machinesQ.data && machinesQ.data.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Machine</th>
                <th>Line</th>
                <th>Status</th>
                <th>Attention</th>
                <th>Reasons</th>
                <th>Latest event</th>
              </tr>
            </thead>
            <tbody>
              {machinesQ.data.map((m) => (
                <tr key={m.machine_id}>
                  <td>
                    <Link to={`/machines/${m.machine_id}`}>{m.machine_id}</Link>
                  </td>
                  <td>{m.line_id}</td>
                  <td>
                    <StatusBadge status={m.derived_status} />
                  </td>
                  <td>{m.attention_level}</td>
                  <td>
                    {m.reason_codes.map((r) => (
                      <span className="tag" key={r}>
                        {r}
                      </span>
                    ))}
                  </td>
                  <td className="muted">
                    {m.latest_relevant_event_time
                      ? new Date(m.latest_relevant_event_time).toLocaleString()
                      : "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
