import { useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { ingestPayload, type IngestResult, type VendorSlug } from "../api/client";
import { VENDORS, groupByCategory } from "../vendor/templates";

export function VendorIngestionPage() {
  const queryClient = useQueryClient();

  const [vendorSlug, setVendorSlug] = useState<VendorSlug>("pulseforge");
  const vendor = useMemo(() => VENDORS.find((v) => v.slug === vendorSlug)!, [vendorSlug]);

  const [apiKey, setApiKey] = useState<string>(vendor.defaultApiKey);
  const [useCurrentTime, setUseCurrentTime] = useState<boolean>(true);

  const [templateId, setTemplateId] = useState<string>(vendor.templates[0].id);
  const template = useMemo(
    () => vendor.templates.find((t) => t.id === templateId) ?? vendor.templates[0],
    [vendor, templateId],
  );
  const groupedTemplates = useMemo(() => groupByCategory(vendor.templates), [vendor]);

  const [body, setBody] = useState<string>(() => template.build(new Date(), useCurrentTime));
  const [bodyDirty, setBodyDirty] = useState<boolean>(false);

  const [sending, setSending] = useState(false);
  const [result, setResult] = useState<IngestResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  function onVendorChange(slug: VendorSlug) {
    const v = VENDORS.find((x) => x.slug === slug)!;
    setVendorSlug(slug);
    setApiKey(v.defaultApiKey);
    setTemplateId(v.templates[0].id);
    setBody(v.templates[0].build(new Date(), useCurrentTime));
    setBodyDirty(false);
    setResult(null);
    setError(null);
  }

  function onTemplateChange(id: string) {
    const tpl = vendor.templates.find((t) => t.id === id);
    if (!tpl) return;
    setTemplateId(id);
    setBody(tpl.build(new Date(), useCurrentTime));
    setBodyDirty(false);
  }

  function onToggleUseCurrentTime(checked: boolean) {
    setUseCurrentTime(checked);
    if (!bodyDirty) {
      setBody(template.build(new Date(), checked));
    }
  }

  function regenerateFromTemplate() {
    setBody(template.build(new Date(), useCurrentTime));
    setBodyDirty(false);
  }

  function tryFormatJson() {
    try {
      const parsed = JSON.parse(body);
      setBody(JSON.stringify(parsed, null, 2));
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Invalid JSON");
    }
  }

  async function onSend() {
    setSending(true);
    setError(null);
    setResult(null);
    try {
      JSON.parse(body);
    } catch (e) {
      setError(`Body is not valid JSON: ${e instanceof Error ? e.message : String(e)}`);
      setSending(false);
      return;
    }
    try {
      const res = await ingestPayload(vendorSlug, apiKey, body);
      setResult(res);
      queryClient.invalidateQueries({ queryKey: ["processing-recent"] });
      queryClient.invalidateQueries({ queryKey: ["machines"] });
      queryClient.invalidateQueries({ queryKey: ["plant-summary"] });
      queryClient.invalidateQueries({ queryKey: ["dead-letters"] });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Request failed");
    } finally {
      setSending(false);
    }
  }

  const statusClass = result
    ? result.status >= 200 && result.status < 300
      ? "ok"
      : "err"
    : "";

  const scenarioCount = vendor.templates.length;

  return (
    <>
      <h1>Vendor Service</h1>
      <p className="muted">
        Pick a vendor, pick a scenario, then hit Send. Each scenario is a pre-built request body that
        exercises a specific code path — thresholds, severity levels, edge cases, multi-record batches.
        Edit the JSON freely if you want to customize a payload before sending.
      </p>

      <div className="card">
        <div className="form-row">
          <label>
            <span className="form-label">Vendor</span>
            <select value={vendorSlug} onChange={(e) => onVendorChange(e.target.value as VendorSlug)}>
              {VENDORS.map((v) => (
                <option key={v.slug} value={v.slug}>
                  {v.name} ({v.templates.length} scenarios)
                </option>
              ))}
            </select>
          </label>

          <label style={{ flex: 1 }}>
            <span className="form-label">X-Vendor-Api-Key</span>
            <input
              type="text"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              spellCheck={false}
            />
          </label>
        </div>

        <div className="form-row">
          <label style={{ flex: 1 }}>
            <span className="form-label">Scenario ({scenarioCount} total)</span>
            <select value={templateId} onChange={(e) => onTemplateChange(e.target.value)}>
              {Array.from(groupedTemplates.entries()).map(([category, items]) => (
                <optgroup key={category} label={category}>
                  {items.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.label}
                    </option>
                  ))}
                </optgroup>
              ))}
            </select>
          </label>
          <label className="checkbox">
            <input
              type="checkbox"
              checked={useCurrentTime}
              onChange={(e) => onToggleUseCurrentTime(e.target.checked)}
            />
            <span>Use current timestamp</span>
          </label>
          <button type="button" className="btn-secondary" onClick={regenerateFromTemplate}>
            Reset to template
          </button>
        </div>

      </div>

      <div className="card">
        <div className="form-label-row">
          <span className="form-label">Request body (JSON)</span>
          <button type="button" className="btn-link" onClick={tryFormatJson}>
            Format JSON
          </button>
        </div>
        <textarea
          className="json-editor"
          value={body}
          onChange={(e) => {
            setBody(e.target.value);
            setBodyDirty(true);
          }}
          spellCheck={false}
          rows={18}
        />
        <div className="form-row" style={{ marginTop: 12 }}>
          <button type="button" className="btn-primary" onClick={onSend} disabled={sending}>
            {sending ? "Sending…" : `Send → POST ${vendor.endpoint}`}
          </button>
          {bodyDirty && <span className="muted small">edited from template</span>}
        </div>
      </div>

      {error && <div className="error">{error}</div>}

      {result && (
        <div className="card">
          <h2>
            Response{" "}
            <span className={`status-pill ${statusClass}`}>
              HTTP {result.status} {result.statusText}
            </span>
          </h2>
          <pre className="json-output">{JSON.stringify(result.body, null, 2)}</pre>
          {result.status === 202 && (
            <p className="muted small">
              Accepted — the worker will process this shortly. Check the{" "}
              <a href="/ingestion">Ingestion Status</a> page or the{" "}
              <a href="/dashboard">Dashboard</a> to see it land.
            </p>
          )}
          {result.status === 200 && (
            <p className="muted small">
              Duplicate — same idempotency key as a previous submission. No new event created.
            </p>
          )}
          {result.status === 401 && (
            <p className="muted small">
              Unauthorized — check the X-Vendor-Api-Key field above. Each vendor has its own key.
            </p>
          )}
          {result.status === 400 && (
            <p className="muted small">
              Bad request — the body was rejected before reaching the worker. Common causes: empty
              body, payload too large.
            </p>
          )}
        </div>
      )}
    </>
  );
}
