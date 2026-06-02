# Validate

Run all validation gates in order. Stop at the first failure and fix it before continuing — a later gate passing does not redeem an earlier failure.

Use this skill at the end of any task before declaring it done.

## Gate order

```
1. simplify           (clean up the code first)
2. lint-backend       (C# format + analyzers)
3. dotnet test        (C# unit + integration tests)
4. frontend tests     (TS unit + integration)
5. mutation tests     (C# only, scoped to changed files)
6. thermo-nuclear     (final quality review)
```

Gates 1–4 are always required. Gate 5 is required for any C# logic change. Gate 6 is always required.

---

## Gate 1 — Simplify

Run `/simplify`. It will review and apply simplification, reuse, and efficiency cleanups to the changed code.

If it modifies files, continue to Gate 2 — the subsequent gates validate the simplified result.

---

## Gate 2 — Backend lint

Run `/lint-backend`. Both tools must pass with zero diagnostics.

If anything fails, fix it now. Do not proceed to Gate 3 until Gate 2 is clean.

---

## Gate 3 — Backend tests

```bash
cd MEditService
dotnet test
```

All tests must pass. A single failure means the gate is red — fix before continuing.

---

## Gate 4 — Frontend tests

```bash
cd medit-vscode
npm run test:unit
npm run test:integration
```

Both suites must pass. `test:integration` runs inside a real VS Code process and takes ~10s.

---

## Gate 5 — Mutation tests (C# changes only)

Skip this gate if your change touched only TypeScript, config, or docs.

Run `/mutation-test` scoped to the files you changed. See that skill for how to set the `mutate` glob in `stryker-config.json`.

**Survivors block completion.** Triage each one: add a test that kills it, simplify the code, or suppress with justification. Do not move on with untriaged survivors.

---

## Gate 6 — Thermo-nuclear code quality review

Run `/thermo-nuclear-code-quality-review`. Address any findings before declaring done.

---

## Declaring done

All applicable gates pass → the task is done.

If a gate cannot pass (e.g. a known pre-existing issue unrelated to your change), document why in your response. Do not silently skip gates.
