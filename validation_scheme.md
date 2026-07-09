# Validation Scheme — Frontend Handoff

## Purpose

This document describes the .NET backend changes that expose a high-level **validation
scheme** on ML training policies. It is a handoff for the frontend (Angular) agent, who
will add UI to select and display this option. The backend already persists the value and
forwards it to the Python ML sidecar when a training run starts — the UI only needs to
let users choose it and show what was chosen.

## What This Feature Is

Every ML policy now carries a `validationScheme`: a single high-level choice of how a
training run is validated before a model can be promoted. There are exactly three values:

| Wire value | Suggested label | Meaning |
| --- | --- | --- |
| `single` | Single split | Default, fastest. One chronological train/out-of-sample split with a single-split promotion gate. |
| `block` | Block walk-forward | Walk-forward consistency over equal blocks of the development region. |
| `sliding` | Sliding walk-forward | Calendar walk-forward simulating periodic retraining with forward test windows; most realistic for serious promotion runs. |

The field is intentionally high-level only. Fold counts, calendar window sizes, embargo
bars, and promotion thresholds are engine-owned defaults in the Python sidecar and are
**not** exposed in the API — do not add UI inputs for them.

## API Changes

The scheme lives on the **ML policy**, not on the training-run trigger. It is set when a
policy is created or edited, and it is used automatically for every training run started
from that policy.

Base route: `/api/ml`. All endpoints require the `X-Api-Key` header (unchanged).

### Create policy — `POST /api/ml/policies`

Request body gains one optional field, `validationScheme`:

```jsonc
{
  "symbol": "BTCUSDT",
  "interval": "1h",
  "totalTimesteps": 5000,
  "initialBalance": 10000,
  "fee": 0.65,
  "slippage": 0,
  "dailyProfit": 300,
  "dailyDrawdownLimit": 200,
  "maxCandlesPerTrade": 48,
  "riskPerTrade": 50,
  "validationScheme": "sliding"   // NEW — optional
}
```

- **Optional.** Omitting it, or sending `null`/`""`, is treated as `"single"`.
- Case/whitespace tolerant: the backend trims and lowercases before validating, so
  `"Sliding"` or `" sliding "` are accepted and normalized to `"sliding"`. Prefer sending
  the exact lowercase value from the UI anyway.

### Update policy — `PUT /api/ml/policies/{id}`

Same request shape as create; `validationScheme` behaves identically. Note the update
endpoint replaces the whole policy, so **always send the current `validationScheme`** when
editing — omitting it resets the policy to `"single"`.

### Policy responses — `GET /api/ml/policies`, `GET /api/ml/policies/{id}`, and the create/update responses

The `MlPolicyResponse` object now includes `validationScheme` as a non-null string (one of
`single` / `block` / `sliding`). Existing policies created before this change read back as
`"single"` (backfilled in the database).

```jsonc
{
  "id": 1,
  "symbolId": 1,
  "symbolCode": "BTCUSDT",
  "intervalId": 3,
  "intervalCode": "1h",
  "totalTimesteps": 5000,
  "initialBalance": 10000,
  "fee": 0.65,
  "slippage": 0,
  "dailyProfit": 300,
  "dailyDrawdownLimit": 200,
  "maxCandlesPerTrade": 48,
  "riskPerTrade": 50,
  "validationScheme": "sliding",  // NEW — always present
  "createdAt": 1752000000000,
  "trainingRunCount": 3
}
```

### Training endpoints — no request change

`POST /api/ml/train`, `POST /api/ml/retrain-all`, and the training-run list/detail
responses are **unchanged**. The scheme is read from the policy at training time and
forwarded to the sidecar automatically; the trigger UI does not send it.

## Validation & Error Behavior

- The backend rejects unsupported values **before** touching the database or the ML
  service. An invalid `validationScheme` on create/update returns **HTTP 400** with a plain
  text message like:

  ```
  Unsupported validationScheme 'weekly'. Allowed values: single, block, sliding.
  ```

- The UI should still constrain the input to the three allowed values (a select/dropdown or
  segmented control), so this 400 is a safety net rather than the primary guard.
- No other status codes changed. Symbol/interval resolution failures still return 404 as
  before, and that check runs after scheme validation.

## Suggested Frontend Work

1. **Policy create/edit form:** add a `validationScheme` control with the three options.
   Use the labels from the table above (Single split / Block walk-forward / Sliding
   walk-forward) and submit the lowercase wire value. Default the control to `single` for
   new policies.
2. **Edit prefill:** initialize the control from the policy's `validationScheme` in the GET
   response. Remember the PUT replaces the whole policy, so include the field on save.
3. **Policy display/history:** surface the chosen scheme wherever policies are listed or a
   training run's policy is shown, so users can see how a run was validated. Map the wire
   value back to the friendly label for display.
4. **Type/model updates:** add `validationScheme` to the policy request and response
   TypeScript interfaces/models (optional string on the request, required string on the
   response). A small union type `'single' | 'block' | 'sliding'` is recommended.
5. **Copy/help text (optional):** a short hint that `single` is fastest and `sliding` is the
   most realistic for promotion runs helps users choose.

## Backward Compatibility

- Existing policies read back as `validationScheme: "single"` — no data migration needed on
  the frontend.
- Older UI code that ignores the field keeps working: the backend treats a missing value as
  `single`, exactly the prior behavior.
