# Validate

Run the right validation gates for what actually changed. Stop at the first failure and fix it before continuing.

Use this skill at the end of any task before declaring it done.

---

## Step 0 — Determine scope

Run these two commands to see every file touched in this session:

```bash
git diff --name-only HEAD
git diff --name-only --cached
```

Classify the results:

| Area | Pattern |
|------|---------|
| **Backend** | `MEditService/**/*.cs` |
| **Frontend** | `medit-vscode/**/*.ts`, `medit-vscode/**/*.tsx`, `medit-vscode/package.json` |
| **API contract** | `medit-vscode/src/generated/api.ts` |
| **Config / docs only** | everything else |

Then select gates:

| Gate | Run when |
|------|----------|
| 1 — Simplify | Always |
| 2 — Backend lint | Any backend change |
| 3 — Backend tests | Any backend change |
| 4 — Frontend tests | Any frontend change, or api.ts regenerated |
| 5 — Mutation tests | Any `MEditService.Core/**/*.cs` change |
| 6 — Code review | Always |

If the only changes are config or docs (no `.cs`, `.ts`, `.tsx`), skip gates 2–5 and jump straight to Gate 6.

---

## Gate 1 — Simplify

Run `/simplify`. It will review and apply simplification, reuse, and efficiency cleanups to the changed code.

If it modifies files, continue to Gate 2 — the subsequent gates validate the simplified result.

---

## Gate 2 — Backend lint  *(backend changes only)*

Run `/lint-backend`. Both tools must pass with zero diagnostics.

If anything fails, fix it now. Do not proceed to Gate 3 until Gate 2 is clean.

---

## Gate 3 — Backend tests  *(backend changes only)*

```bash
cd MEditService
dotnet test
```

All tests must pass. A single failure means the gate is red — fix before continuing.

---

## Gate 4 — Frontend tests  *(frontend changes or api.ts regenerated)*

```bash
cd medit-vscode
npm run test:unit
npm run test:integration
```

Both suites must pass. `test:integration` runs inside a real VS Code process and takes ~10s.

---

## Gate 5 — Mutation tests  *(MEditService.Core changes only)*

Run `/mutation-test` scoped to the files you changed. See that skill for how to set the `mutate` glob in `stryker-config.json`.

**Survivors block completion.** Triage each one: add a test that kills it, simplify the code, or (last resort after subagent rubber-duck sign-off) suppress with justification. Do not move on with untriaged survivors.

---

## Gate 6 — Code review

Run `/thermo-nuclear-code-review`. Address any findings before declaring done.

---

## Declaring done

All applicable gates pass → the task is done.

If a gate cannot pass (e.g. a known pre-existing issue unrelated to your change), document why in your response. Do not silently skip gates.
