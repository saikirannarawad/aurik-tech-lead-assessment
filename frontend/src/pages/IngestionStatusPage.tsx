import { useQuery } from "@tanstack/react-query";
import { api } from "../api/client";

export function IngestionStatusPage() {
  const recentQ = useQuery({
    queryKey: ["processing-recent"],
    queryFn: api.getProcessingRecent,
    refetchInterval: 5000,
  });
  const dlqQ = useQuery({
    queryKey: ["dead-letters"],
    queryFn: api.getDeadLetters,
    refetchInterval: 10000,
  });

  return (
    <>
      <h1>Ingestion Status</h1>
      <p className="muted">Auto-refreshes every 5s. Submit payloads via the ingestion API to see them appear.</p>

      <h2>Recent payloads</h2>
      <div className="card">
        {recentQ.data && recentQ.data.length === 0 && <div className="muted">No payloads yet.</div>}
        {recentQ.data && recentQ.data.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Received</th>
                <th>Vendor</th>
                <th>State</th>
                <th>Records</th>
                <th>Attempts</th>
                <th>Failure reason</th>
                <th>Raw ID</th>
              </tr>
            </thead>
            <tbody>
              {recentQ.data.map((r) => (
                <tr key={r.raw_payload_id}>
                  <td>{new Date(r.received_at).toLocaleString()}</td>
                  <td>{r.vendor}</td>
                  <td>{r.state}</td>
                  <td>{r.record_count}</td>
                  <td>{r.attempt_count}</td>
                  <td className="muted">{r.failure_reason ?? "—"}</td>
                  <td className="muted" style={{ fontFamily: "monospace", fontSize: 11 }}>
                    {r.raw_payload_id}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <h2>Dead-letter queue</h2>
      <div className="card">
        {dlqQ.data && dlqQ.data.length === 0 && <div className="muted">No dead-lettered payloads.</div>}
        {dlqQ.data && dlqQ.data.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Dead-lettered</th>
                <th>Vendor</th>
                <th>Attempts</th>
                <th>Reason</th>
                <th>Raw ID</th>
              </tr>
            </thead>
            <tbody>
              {dlqQ.data.map((d) => (
                <tr key={d.id}>
                  <td>{new Date(d.dead_lettered_at).toLocaleString()}</td>
                  <td>{d.vendor}</td>
                  <td>{d.attempt_count}</td>
                  <td>{d.reason}</td>
                  <td className="muted" style={{ fontFamily: "monospace", fontSize: 11 }}>
                    {d.raw_payload_id}
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
