# Frontend handoff: training-decisions endpoint now streams raw stored JSON

**Source:** trader-algo-api @ `dev` (uncommitted working-tree changes; `dev` has no commits ahead of `main`)
**Generated:** 2026-07-10
**For:** trader-algo-ui (Angular)

## Summary
The `GET /api/ml/training-runs/{id}/decisions` endpoint changed how it produces its response
body, but **not the field names or shape**. Previously the backend deserialized the sidecar's
stored JSON into the `MlTrainingDecisionsResponse` DTO and let ASP.NET re-serialize it; now it
returns the stored JSON blob **verbatim** (`Content-Type: application/json`), because these logs
can be tens of megabytes and the round-trip was wasteful. The payload is still the same
**snake_case** decision log. There is nothing the UI *must* change — but there are a couple of
robustness caveats worth confirming (see Behavior notes), because the response is no longer
normalized through a fixed C# schema.

## REST changes
### `GET /api/ml/training-runs/{id}/decisions` — changed (transport only)
- **Path params:** `id` (`long`, required) — training run id.
- **Response body** (still the decision log, **snake_case** — this is the known snake_case
  payload exception, NOT camelCase):
  ```json
  {
    "ml_policy_id": 0,
    "training_run_id": 0,
    "model_id": "string",
    "symbol": "string",
    "interval": "string",
    "from_date": "string",
    "to_date": "string",
    "initial_balance": 0.0,
    "final_balance": 0.0,
    "pnl_pct": 0.0,
    "oos_final_balance": 0.0,
    "oos_pnl_pct": 0.0,
    "n_trades": 0,
    "calibrated": false,
    "continued_from_training_run_id": 0,
    "policy_params": { "<key>": "<any JSON value>" },
    "decisions": [
      {
        "candle_index": 0,
        "open_time": 0,
        "action": 0,
        "action_name": "string",
        "confidence": 0.0,
        "probs": [0.0],
        "position": 0,
        "balance": 0.0
      }
    ],
    "trades": [
      {
        "entry_step": 0,
        "entry_time": 0,
        "entry_price": 0.0,
        "side": "string",
        "exit_step": 0,
        "exit_time": 0,
        "exit_price": 0.0,
        "reason": "string",
        "pnl": 0.0
      }
    ]
  }
  ```
- **What changed vs. before:** No field added, removed, renamed, or retyped. The body is now the
  raw stored payload rather than a DTO round-trip, so it is no longer coerced to a fixed schema
  (see Behavior notes for the two practical consequences).
- **Status codes / errors:** `200` with the JSON body; `404` (`NotFound`) with a plain-text
  message `"No training decision log for run {id}."` when no log exists for that run (training
  not finished, never run, or sidecar telemetry disabled). Unchanged from before.
- **Frontend touch points:** `structures/ml-training.ts` (the decision-log `*Dto` +
  `toX()` mapper), `services/trader-algo-api.service.ts` (the method that fetches decisions),
  `pages/ml` (the decision-replay / training visualization view). No code change is required
  unless the current mapper assumes a strict field set — see Behavior notes.

## Behavior notes
The response is snake_case (as it already was), so keep the existing `*Dto` + `toDecisionLog()`
mapper. Two things are now technically looser because the body is passed through verbatim instead
of being normalized by the C# DTO:

1. **Unknown keys may appear.** If the ML sidecar writes any keys beyond the fields listed above,
   they were previously silently dropped by the DTO and will now pass through to the UI. The
   mapper should keep ignoring keys it doesn't recognize (standard for a `toX()` mapper — no
   action needed if it already reads only the fields it maps).
2. **Optional fields may be absent rather than `null`.** The nullable fields — `oos_final_balance`,
   `oos_pnl_pct`, `calibrated`, `continued_from_training_run_id`, and per-decision/per-trade
   `open_time` / `entry_time` / `exit_time` — were previously always emitted (as `null`) by the
   DTO round-trip. In the verbatim payload the sidecar may **omit them entirely**. Treat
   `missing === null`: read with optional access (`?.`) / nullish defaults, not a hard assumption
   the key exists. If the mapper already does this, no change is needed.

Neither of these changes a field's meaning; they only affect how defensively the payload should
be parsed.

## Explicitly unchanged
- **No field names, types, or enum values changed.** `MlTrainingDecisionsResponse`,
  `MlTrainingDecision`, and `MlTrainingTrade` all keep the exact snake_case keys above.
- **Route, method, path params, and the `404` behavior are identical.**
- **No WebSocket changes.** The decision-log WebSocket replay reads the same table and is
  unaffected; its event envelope and frames are untouched.
- **No other REST endpoint changed.** This branch's only other edit
  (`TrainingDecisionsQueryExtensions`) is an internal refactor that splits out a raw-payload
  reader; it produces no new wire surface.
