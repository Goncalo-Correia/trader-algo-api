---
name: ml-agent
description: >-
  Consume the latest handoff written in the sibling trader-algo-ml repo and implement the
  described .NET changes here in trader-algo-api. Use when the user asks to "run the handoff",
  "implement the handoff", "apply the ML handoff", "act on the latest handoff", or "pick up the
  ML changes" — the inbound counterpart to the trader-algo-ml `handoff` skill (which writes the
  brief). This skill finds the newest handoff in trader-algo-ml, analyzes it against the real code,
  asks only genuinely blocking questions, then implements the backend changes and verifies with
  `dotnet build`.
---

# ml-agent — implement an inbound handoff from trader-algo-ml

The sibling **`trader-algo-ml`** (Python) repo's `handoff` skill produces a self-contained Markdown
brief describing what this **`trader-algo-api`** (.NET 10) backend must change to stay in contract
(DTOs, controllers/services, enum↔lookup pairs, EF migrations, telemetry schema). This skill is the
receiving end: it reads that brief, turns it into real backend changes, and verifies the build.

Do the work in order. Stop and report if a step can't be completed rather than guessing.

## 1. Find the latest handoff

Locate the newest handoff file, in this order of preference:

1. **The ML repo's handoff dir** — the sibling `../trader-algo-ml/handoff/handoff-*.md`
   (the ML `handoff` skill's default output location; this is the normal source).
2. **This repo's root** — `handoff.md` or `handoff-*.md` (a copy the user dropped here for you).

If several exist, pick the most recent by the timestamp in the filename (`YYYY-MM-DD-HHmm`, then
`YYYY-MM-DD`; fall back to mtime).
**Announce the exact path you settled on** before reading further. If the user named a specific file
or path, use that instead. If **no** handoff file exists in either location, stop and tell the user —
there is nothing to implement; do not invent work.

## 2. Analyze the brief against the real code

Read the whole handoff, then validate every claim against the current codebase before touching
anything — the brief describes intent, but the code is ground truth:

- **Contract changes** — for each change, open the exact `.cs` files it names
  (`Dtos/Ml/*.cs`, `Controllers/*`, `Services/*`, `Models/*`) and confirm the current shape matches
  what the brief assumes as the "before". `TraderAlgoApi/Program.cs` is the fastest map of the system.
- **Wire names** — the brief gives snake_case field names for the ML-facing contract
  (`validation_scheme`, `fee_rate`, `risk_per_trade`). Confirm the corresponding C# DTO property and
  its mapping. Almost all other DTOs are camelCase; the `/train` and `/decide` payloads are the
  snake_case exception.
- **Telemetry / DB** — if the brief changes a `training_*` column, that's a two-part change: a
  `Models/Telemetry/*` model edit **and** an EF migration. The sidecar writes these tables but the
  schema is owned here.
- **Explicitly NOT changing** — treat this section as a hard fence. Do **not** mirror ML-internal
  knobs (PPO/reward/fold/window internals) into DTOs; the `/train` contract is business-only.
- **Notes / open questions** — carry these into step 3.

If the brief says "no backend changes required," confirm that against the diff it references and, if
you agree, report that and stop — a no-op is a valid outcome.

## 3. Ask only blocking questions

Ask the user a question **only** when the answer changes what you implement and you can't resolve it
from the handoff or the code — e.g. an open question the brief flags, a contract detail that
contradicts the current code, an ambiguous type/nullability, or a migration that would be destructive.
If the brief is unambiguous and lines up with the code, **do not ask — just implement.** Batch any
questions into one round rather than trickling them out.

## 4. Implement

Make the changes the handoff prescribes, honoring this repo's conventions (see `CLAUDE.md`):

- Work inside the **`TraderAlgoApi/`** project directory.
- **DTOs** are `sealed record`s under `Dtos/`, grouped by feature; nullable reference types on.
- **Enums serialize as strings** (global `JsonStringEnumConverter`, no naming policy → the C# name
  is the wire value verbatim). `validationScheme` stays a **lowercase string**, not an enum.
- **Enum ↔ lookup dual representation** — when adding a lookup value, add it to **both** the
  `Models/Enums/*.cs` enum **and** the `Models/Lookups/*.cs` `HasData` seed with **matching integer
  IDs**, then create a migration.
- **Migrations run against a live production DB — additive only.** Never drop or rename populated
  columns/tables. Create with `dotnet ef migrations add <Name>` from `TraderAlgoApi/`. Retired
  columns stay mapped rather than being dropped.
- **Decimals** for prices/quantities/PnL use `[Precision(28, 10)]`.
- **Errors** flow through `GlobalExceptionHandler` as RFC 7807 ProblemDetails — return typed errors,
  don't hand-roll error bodies. Validation failures return **400**.
- **Do not add tests or a test framework** — the owner does not want them and none exists.

Keep edits scoped to what the handoff describes plus what's mechanically required to compile
(mappings, DI registration in `Program.cs`, migration). Don't refactor unrelated code.

## 5. Verify

From `TraderAlgoApi/`, run `dotnet build` and confirm it succeeds — this is the **only** verification
gate in this repo (no linter, no tests). If it fails, fix the cause and rebuild; don't leave a broken
build. If a migration was added, sanity-check it is additive (no `DropColumn`/`DropTable` on
populated schema) before considering the work done. Do **not** run `dotnet ef database update` against
the production DB unless the user explicitly asks.

## 6. Report

Summarize for the user:
- Which handoff file you implemented (path), and that it came from trader-algo-ml.
- The concrete changes made, grouped by file (DTOs, controllers/services, enums+lookups, migration).
- Build result.
- Anything you deliberately did **not** do (items in the brief's "Explicitly NOT changing", or work
  you flagged for the user to confirm).

## Guardrails

- This skill **implements** a handoff; it does not deploy. Stay on the working branch (`dev`) and
  leave committing/merging/pushing to the `deploy` skill unless the user asks.
- The brief is the plan, but the **code is the source of truth** — if they conflict, surface the
  conflict instead of blindly following the brief.
- Respect the "Explicitly NOT changing" fence; internal ML knobs must not leak into the contract.
- Additive migrations only; never destructive against the live DB.
- Never add tests or `--no-verify`/force operations unless the user explicitly asks.
