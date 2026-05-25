#!/usr/bin/env bash
# Submit the supplied vendor JSON samples against a running API.
# Usage: ./scripts/load-samples.sh [BASE_URL]
set -euo pipefail

BASE_URL="${1:-http://localhost:8080}"
SAMPLES="$(cd "$(dirname "$0")/.." && pwd)/backend/seed/vendor_api_samples"

PF_KEY="${PULSEFORGE_KEY:-pf-dev-key-change-me}"
TW_KEY="${THERMEXWATCH_KEY:-tw-dev-key-change-me}"
MF_KEY="${MAINTAFLOW_KEY:-mf-dev-key-change-me}"

post() {
  local vendor="$1" key="$2" file="$3"
  echo "→ POST $vendor: $(basename "$file")"
  curl -sS -X POST "$BASE_URL/api/ingestion/$vendor" \
    -H "Content-Type: application/json" \
    -H "X-Vendor-Api-Key: $key" \
    --data-binary "@$file" \
    -w "\n  HTTP %{http_code}\n" || true
}

post pulseforge   "$PF_KEY" "$SAMPLES/pulseforge_sample.json"
post thermexwatch "$TW_KEY" "$SAMPLES/thermexwatch_sample.json"
post maintaflow   "$MF_KEY" "$SAMPLES/maintaflow_sample.json"

echo
echo "Edge-case batches:"
# happy_path.json bundles all three vendors as separate sections — split via jq if available.
if command -v jq >/dev/null 2>&1; then
  for vendor in pulseforge thermexwatch maintaflow; do
    body="$(jq -c ".${vendor}" "$SAMPLES/happy_path.json")"
    if [[ "$body" != "null" ]]; then
      echo "→ happy_path / $vendor"
      key_var="$(echo "$vendor" | tr '[:lower:]' '[:upper:]')_KEY"
      key="${!key_var:-}"
      [[ -z "$key" ]] && case "$vendor" in pulseforge) key="$PF_KEY";; thermexwatch) key="$TW_KEY";; maintaflow) key="$MF_KEY";; esac
      printf '%s' "$body" | curl -sS -X POST "$BASE_URL/api/ingestion/$vendor" \
        -H "Content-Type: application/json" -H "X-Vendor-Api-Key: $key" \
        --data-binary @- -w "\n  HTTP %{http_code}\n" || true
    fi
  done
else
  echo "(jq not installed — skipping happy_path.json split)"
fi

post pulseforge "$PF_KEY" "$SAMPLES/duplicates.json"   || true
post pulseforge "$PF_KEY" "$SAMPLES/out_of_order.json" || true
post pulseforge "$PF_KEY" "$SAMPLES/retry_cases.json"  || true
post pulseforge "$PF_KEY" "$SAMPLES/conflicting_updates.json" || true
post pulseforge "$PF_KEY" "$SAMPLES/malformed.json"    || true

echo
echo "Done. Watch the worker logs (docker compose logs -f api) and curl /api/processing/recent."
