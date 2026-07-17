# Frontend handoff: remove `sliding` validation scheme from ML policies

**Source:** trader-algo-api @ `dev` (working tree vs `main`; no commits ahead — changes are uncommitted)
**Generated:** 2026-07-17
**For:** trader-algo-ui (Angular)

## Summary

The ML policy `validationScheme` no longer supports `"sliding"`. The allowed set shrank from
`single | block | sliding` to just **`single | block`**. The backend now silently coerces a submitted
`"sliding"` to `"block"` (it is **not** rejected), and no policy will ever store or return `"sliding"`
again. The only frontend change required is to drop the `"sliding"` option from the validation-scheme
string-union type and from any policy create/edit UI (dropdown/radio). No fields were added, removed,
renamed, or retyped.

## REST changes

### `POST /api/ml/policies` and `PUT /api/ml/policies/{id}` — changed

- **Request body** (`MlPolicyRequest`, camelCase) — shape unchanged; only the `validationScheme`
  value domain changed:
  ```json
  {
    "symbol": "string",
    "interval": "string",
    "totalTimesteps": 0,
    "initialBalance": 0.0,
    "fee": 0.0,
    "slippage": 0.0,
    "dailyProfit": 0.0,
    "dailyDrawdownLimit": 0.0,
    "maxCandlesPerTrade": 0,
    "riskPerTrade": 0.0,
    "validationScheme": "single | block | null"
  }
  ```
- **Response body** (`MlPolicyResponse`, camelCase) — unchanged shape; `validationScheme` will now
  only ever be `"single"` or `"block"`:
  ```json
  {
    "id": 0,
    "symbolId": 0,
    "symbolCode": "string",
    "intervalId": 0,
    "intervalCode": "string",
    "totalTimesteps": 0,
    "initialBalance": 0.0,
    "fee": 0.0,
    "slippage": 0.0,
    "dailyProfit": 0.0,
    "dailyDrawdownLimit": 0.0,
    "maxCandlesPerTrade": 0,
    "riskPerTrade": 0.0,
    "validationScheme": "single | block",
    "createdAt": 0,
    "trainingRunCount": 0
  }
  ```
- **What changed vs. before:** `validationScheme` allowed values `single | block | sliding` →
  **`single | block`**. Submitting `"sliding"` is now accepted but **coerced to `"block"`** (the
  closest walk-forward equivalent), so the persisted/returned value is `"block"`, never `"sliding"`.
  `null`/blank still normalizes to `"single"`.
- **Status codes / errors:** `400` when `validationScheme` is a non-empty value that is not one of
  the allowed strings. The error text now reads `Unsupported validationScheme '<value>'. Allowed
  values: single, block.` (Note: `"sliding"` no longer triggers this 400 — it is coerced, not
  rejected.)
- **Frontend touch points:** `structures/ml-policy.ts` (the `validationScheme` string-union on the
  policy interface + create/update request types), `trader-algo-api.service.ts` (the create/update
  policy methods — no signature change), `pages/ml` (remove the `sliding` choice from the
  scheme selector).

## Enum changes

- `validationScheme` is **not** a C# enum — it is a plain lowercase string with an allow-list.
  The string-union in `structures/ml-policy.ts` must drop `"sliding"`, leaving `'single' | 'block'`.

## Behavior notes

- **Fold results value domain:** `GET /api/ml/training-runs/{runId}/folds` returns
  `FoldResultResponse` objects that include a `scheme` field. Its shape is unchanged, but the value
  `"sliding"` will no longer appear — only `"single"` / `"block"`. If any UI branches on
  `scheme === 'sliding'`, that branch is now dead.
- **Clean slate server-side:** all ML data on the backend was wiped for a fresh restart (policies,
  training runs, all training telemetry, and the MLflow tracking/registry data). So there are **no
  legacy policies or runs carrying `"sliding"`** for the UI to encounter — training-run lists,
  policy lists, and performance/fold views will start empty until new policies are created and
  trained.

## Explicitly unchanged

- **No DTO fields added/removed/renamed/retyped** on any ML request or response. `MlPolicyRequest`
  and `MlPolicyResponse` have the exact same field set as before (the internal removal of retired
  `Quantity`/`Breakeven`/`BreakevenStop` policy columns never appeared in these DTOs, so it is
  invisible to the UI).
- **No changes** to `GET /api/ml/policies`, `GET /api/ml/policies/{id}`,
  `DELETE /api/ml/policies/{id}`, the training-run endpoints, the ML performance endpoints
  (`/api/ml/...`), or their response shapes — only the `validationScheme` value domain and the
  `scheme` value domain noted above.
- **No WebSocket changes** — no new streams, no changed event envelopes or frame payloads.
- **No new endpoints.**

## Open questions

- If the policy create/edit UI currently defaults to or persists `"sliding"` anywhere in local
  state (e.g. a saved form value), decide whether to migrate it to `"block"` on load or just remove
  the option; the backend will coerce it regardless, but the dropdown should no longer offer it.
