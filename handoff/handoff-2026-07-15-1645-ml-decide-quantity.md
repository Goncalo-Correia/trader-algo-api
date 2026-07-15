# Frontend handoff: `/api/ml/decide` response gains a `quantity` field

**Source:** trader-algo-api @ `dev` (working-tree changes; `main` has no committed diff yet)
**Generated:** 2026-07-15
**For:** trader-algo-ui (Angular)

## Summary
The ML sidecar now uses a deterministic **ATR-regime bracket rule** and, for policies trained with
`risk_per_trade`, returns an explicit order size on each decision. That size flows through the
`POST /api/ml/decide` response as a **new `quantity` field**. The only wire-visible change is this
one added, nullable field on the decide response. If the UI renders the raw `/api/ml/decide`
response (e.g. a "what would the model do now?" preview), it should surface `quantity`; otherwise no
change is strictly required, but the response type should be widened so the field isn't dropped.

## REST changes
### `POST /api/ml/decide` — changed (response adds one field)

This endpoint returns the sidecar decision verbatim. **Note: this DTO serializes `snake_case`, not
the usual camelCase** — it's a passthrough of the ML sidecar contract. Treat it like the candle
payload: use a `*Dto` with snake_case keys + a `toX()` mapper rather than assuming camelCase.

- **Request body** — **unchanged** (`MlDecideQueryRequest`). Not repeated here.
- **Response body** (`MlDecideResponse`, **snake_case**):
  ```json
  {
    "action": 0,
    "action_name": "Hold",
    "confidence": 0.0,
    "model_id": "123",
    "ml_policy_id": 123,
    "sl_bracket": null,
    "tp_bracket": null,
    "sl_atr_mult": null,
    "tp_r_mult": null,
    "quantity": null
  }
  ```
  Field types: `action` `number` (int), `action_name` `string`, `confidence` `number`, `model_id`
  `string`, `ml_policy_id` `number`, `sl_bracket` `number | null`, `tp_bracket` `number | null`,
  `sl_atr_mult` `number | null`, `tp_r_mult` `number | null`, **`quantity` `number | null` (new)**.
- **What changed vs. before:** added **`quantity`** (`number | null`). All other fields are
  unchanged in name and type.
- **Status codes / errors:** unchanged (`404` unknown policy or no candle data; `400` symbol/interval
  mismatch vs. the policy).
- **Frontend touch points:** `structures/predict.ts` (or wherever the `/api/ml/decide` response type
  / `*Dto` + mapper lives — confirm the actual filename), `services/trader-algo-api.service.ts` (the
  decide method), `pages/ml` (if the decision is rendered).

## Behavior notes
- **`quantity` semantics:** non-null (e.g. `1.0` / `0.5` / `0.25`) when the served model was trained
  with `risk_per_trade` (ATR-regime mode) — it's the regime-selected order size to display/use
  verbatim. `null` in the legacy menu mode, where no explicit size is chosen. Don't coerce `null` to
  `0` for display; render it as "not specified" / omit it.
- **`sl_bracket` / `tp_bracket` can now be `null` on an entry.** In ATR-regime mode the sidecar
  returns these two as `null` while still returning `sl_atr_mult` and `tp_r_mult` (the effective
  multiples). If any UI code assumed a non-null bracket index on an entry decision, relax that — the
  meaningful values are `sl_atr_mult` (stop = `sl_atr_mult × ATR-at-entry`) and `tp_r_mult`
  (take-profit = `tp_r_mult × stop`). This is a value/semantics note; the field types were already
  `number | null`.
- **snake_case reminder:** keys are `action_name`, `model_id`, `ml_policy_id`, `sl_bracket`,
  `tp_bracket`, `sl_atr_mult`, `tp_r_mult`, `quantity` — not camelCase.

## Explicitly unchanged
- **No request-side change.** `MlDecideQueryRequest` (the `POST /api/ml/decide` body) is untouched.
- **No other endpoints changed.** The `/train` contract (`POST /api/ml/policies`, `/api/ml/train`,
  `MlPolicyRequest`/`MlTrainRequest`) is unchanged — no new policy fields, no ATR-regime knobs on the
  wire (thresholds/quantities/TP-multiples are model-internal).
- **No enum changes.** No new `TradingStrategy`/`TradeSide`/`TradeStatus`/etc. values.
- **No WebSocket changes.** No new streams; no change to any `{ type, data }` envelope, event type,
  or frame payload (including the tradebot-events and live-chart streams).
- **No DB/telemetry surface change** visible to the UI (the `training_*` tables and their read
  endpoints are unchanged).

## Open questions
- Does the UI currently render the raw `/api/ml/decide` response anywhere? If not, the only needed
  change is widening the response type/`*Dto` to carry `quantity` so it isn't silently dropped. If it
  does show the decision, decide how to present `quantity` (a size chip alongside the direction, only
  when non-null).
