---
name: deploy
description: Deploy the trader-algo-api project. Use when the user asks to "deploy", "ship", or "release" the project. Updates docs, commits and pushes dev, then merges into main and pushes main.
---

# Deploy

Run this when the user asks to deploy the project. Perform the steps below in order. Stop and report if any step fails — do not force-push or skip verification.

## Steps

1. **Verify build.** From `TraderAlgoApi/`, run `dotnet build` and confirm it succeeds. This is the only verification gate in this repo. If it fails, stop and report — do not deploy a broken build.

2. **Update docs.** Review the current diff (`git diff`, `git log main..dev --oneline`) and bring `CLAUDE.md` and `README.md` up to date with any changes since the last deploy — new/changed endpoints, services, env vars, conventions, or architecture. If nothing meaningful changed, say so and leave them as-is.

3. **Commit and push dev.**
   - Confirm the current branch is `dev` (`git branch --show-current`). If not, stop and ask.
   - Stage all changes: `git add -A`.
   - Commit with a concise message describing what's being deployed.
   - Push: `git push origin dev`.

4. **Merge into main and push.**
   - `git checkout main`
   - `git pull origin main` (fast-forward remote changes first)
   - `git merge dev` (prefer a fast-forward or a clean merge; if there are conflicts, stop and report)
   - `git push origin main`
   - `git checkout dev` (return to the working branch)

5. **Report.** Summarize what was committed, the merge result, and that both branches are pushed. Note that Render auto-deploys from the root `Dockerfile` on push to main.

## Notes

- Commit messages: end with the standard `Co-Authored-By` trailer per repo convention.
- Never use `--no-verify` or force-push unless the user explicitly asks.
- On Windows the filesystem is case-insensitive, so `README.md` / `Readme.md` / `readme.md` are the same file.
