---
name: handoff
description: >-
  Generate a frontend handoff document describing the API contract changes made in this
  trader-algo-api (backend) branch, so an agent working in the trader-algo-ui (Angular)
  repo can implement the matching frontend changes. Use this whenever the user asks to
  "write a handoff", "hand off to the frontend", "generate the UI handoff", "document the
  API changes for the frontend", or otherwise wants to pass backend work over to the UI —
  even if they don't say the word "handoff". This produces the .md brief the next agent
  reads; it does NOT edit the frontend itself.
---

# Handoff — backend → frontend

Turn the API changes on this branch into a self-contained brief that an agent in the
**trader-algo-ui** (Angular) repo can act on without seeing this codebase or this
conversation. The reader has no access to trader-algo-api, so the doc must carry every
contract detail it needs.

## What a frontend agent actually needs

The UI consumes trader-algo-api over exactly two channels — **REST** (`TraderAlgoApiService`)
and **WebSocket** (`LiveChartDataService`, `TradeBotEventsService`). So the only backend
changes that matter to it are ones that cross that wire:

- **REST endpoints** — new/changed/removed routes: HTTP method, path, query params, request
  body, response body, status codes.
- **DTO shape** — fields added/removed/renamed/retyped on any request or response `record`
  under `Dtos/`. The wire name is what counts (the `[JsonPropertyName]` value), not the C#
  property name.
- **Enum values** — new/removed values on any enum that appears in a DTO. Enums serialize as
  **strings** (`JsonStringEnumConverter`, no naming policy → the value is the C# name verbatim,
  e.g. `Active`, `MlPolicy`), so the UI's string-union types must match exactly.
- **WebSocket streams** — new endpoints under `WebSockets/WebSocketEndpoints.cs`, or changes to
  the event envelope (`{ type, data }`), event `type` names, or the payload shape of any frame.

Purely internal changes — EF migrations, background services, indicator math, service
refactors, DB schema that isn't exposed — **do not belong in the handoff** unless they change
something observable on the wire (e.g. a field's value semantics, a new validation that returns
400, a behavior the UI must reflect). When in doubt, ask: "would the Angular app render or send
anything different?" If no, leave it out.

## Steps

1. **Establish the change set.** Find what this branch changed relative to `main` (the deploy
   target). Use `git diff main...HEAD` for committed work plus `git status` / `git diff` for
   uncommitted work. If the branch is `main` or the range is empty/ambiguous, ask the user which
   commits or range to hand off. Announce the range you settled on.

2. **Filter to the contract surface.** From the diff, keep only changes under `Controllers/`,
   `Dtos/`, `Models/Enums/`, `Models/Lookups/` (new enum/lookup values), and
   `WebSockets/`. Read each changed file at its new version to get exact wire names and types —
   don't infer field names from the C# property; read the `[JsonPropertyName]`. Trace each
   changed DTO to the endpoint(s) that return/accept it so you can name the route.

3. **Note the snake_case exception.** Almost all DTOs serialize **camelCase**. The
   candle-with-indicators payload is the known exception — it serializes **snake_case** (e.g.
   `taker_buy_base_asset_volume`, `sma_20`, `macd_line`). If your change touches that payload,
   flag it explicitly so the UI adds a `*Dto` + `toX()` mapper rather than assuming camelCase.

4. **Map to frontend touch points.** For each contract change, name where it lands in
   trader-algo-ui so the next agent starts in the right place. The layout there:
   - `src/app/structures/*` — domain interfaces, `*Dto` types, and `toX()` mappers (one file per
     feature: `trade.ts`, `backtest.ts`, `ml-policy.ts`, `ml-training.ts`, `trade-bot.ts`,
     `candle.ts`, `session.ts`, `symbol.ts`, `interval.ts`, `strategy.ts`, `trading-account.ts`,
     `predict.ts`). This is where field/enum changes go.
   - `src/app/services/trader-algo-api.service.ts` — every REST call; add/adjust the method here.
   - `src/app/services/live-chart-data.service.ts` — all WebSocket streams. `TradeBotEventsService`
     (`/ws/tradebots/events`) is a separate live stream.
   - `src/app/pages/*` — the routed page that would surface the change to the user.
   These are pointers to orient the agent, not prescriptions — it will confirm against the real
   files. Only reference a path you're reasonably confident exists from the feature name; if
   unsure, describe the change and let the agent locate the file.

5. **Write the doc** using the template below. Default output path is `handoff.md` at the
   trader-algo-api repo root (git-ignore-friendly; the user copies it into a trader-algo-ui
   session). Honor any path the user gives instead. If a `handoff.md` already exists, confirm
   before overwriting.

6. **Report** the output path and a one-line summary of what's in it.

## Output template

Use this structure. Omit any section that has no content rather than writing "None" everywhere —
except **Explicitly unchanged**, which is valuable precisely because it stops the frontend agent
from guessing. Under each contract change, give the concrete before/after wire shape.

```markdown
# Frontend handoff: <short title of the change>

**Source:** trader-algo-api @ `<branch>` (`<base>...<head>`, or commit list)
**Generated:** <YYYY-MM-DD>
**For:** trader-algo-ui (Angular)

## Summary
<2–4 sentences: what changed on the backend and why the UI has to change. Plain language.>

## REST changes
### `<METHOD> <path>`  — <new | changed | removed>
- **Query params:** `<name>` (`<type>`, required?/default) …
- **Request body** (`<DtoName>`, camelCase):
  ```json
  { "fieldName": "<type>", ... }
  ```
- **Response body** (`<DtoName>`, camelCase):
  ```json
  { "fieldName": "<type>", ... }
  ```
- **What changed vs. before:** <added `x`, removed `y`, renamed `a`→`b`, retyped `c`>
- **Status codes / errors:** <e.g. 400 when validationScheme is not single|block|sliding>
- **Frontend touch points:** `structures/<file>.ts` (interface + Dto), `trader-algo-api.service.ts` (`<method>`), `pages/<page>`

## Enum changes
- `<EnumName>` — added value `"<Verbatim>"` (serializes as this exact string). UI string-union in `structures/<file>.ts` must include it.

## WebSocket changes
### `<ws path>`
- **Envelope:** `{ type, data }` — event types: `<Type1>`, `<Type2>` …
- **Changed frame:** `<Type>` now carries `{ ... }`
- **Finite vs. live:** <finite replay (reconnect:false) | live stream>
- **Frontend touch points:** `services/live-chart-data.service.ts` / `trade-bot-events.service.ts`, `structures/<file>.ts`

## Behavior notes
<Semantics the UI must respect that aren't obvious from the shape: value ranges, defaults,
null handling (e.g. `riskPerTrade` null → treated as 0), ordering, snake_case payloads.>

## Explicitly unchanged
<Contract surfaces a reader might assume changed but didn't — e.g. "response DTO fields are
untouched; only the request adds one optional field", "no new WebSocket events". This prevents
speculative frontend edits.>

## Open questions
<Anything the frontend agent must decide or confirm with the user — UI/UX choices the backend
change doesn't dictate. Omit if none.>
```

## Example (abbreviated)

For the recent `validationScheme` work, the REST section would read:

```markdown
### `POST /api/ml/policies` and `PUT /api/ml/policies/{id}` — changed
- **Request body** (`MlPolicyRequest`, camelCase): adds optional
  `"validationScheme": string | null` (allowed: `"single" | "block" | "sliding"`; null/blank → `"single"`).
- **Response body** (`MlPolicyResponse`): adds `"validationScheme": string` (always a normalized value).
- **Status codes:** 400 if `validationScheme` is present but not one of the three allowed values.
- **Frontend touch points:** `structures/ml-policy.ts` (`CreatePolicyRequest`, `UpdatePolicyRequest`,
  `MlPolicy`), `trader-algo-api.service.ts`, `pages/ml`.
```

## Guardrails

- This skill **only writes the handoff .md**. It does not edit the trader-algo-ui repo — that's the
  next agent's job. Don't `cd` into trader-algo-ui to make changes here.
- Read files at their branch version for exact names; never guess a JSON field name from the C#
  property. A wrong field name silently breaks the frontend.
- Keep it self-contained: the reader can't see this repo, so paste the actual JSON shapes rather
  than referencing "the DTO".
- Stay at the contract altitude. Backend internals (migrations, services, DI wiring) are noise to
  the frontend and dilute the brief.
