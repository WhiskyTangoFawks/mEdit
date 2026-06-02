# Mutation Test

Run Stryker.NET mutation tests against `MEditService.Core`. All commands run from `MEditService/`.

## Always run fresh — never reuse a previous run

Do not read or cite results from a prior Stryker run. The scope may have been different, and stale results give false confidence. Always execute the script for the files you are currently validating.

## Running the report

The script auto-scopes to Core files touched in the current diff (staged + unstaged + commits ahead of main), patches `stryker-config.json` at runtime, and restores it when done. Never edit `stryker-config.json` manually.

```bash
cd MEditService && python stryker-report.py
```

To scope to all of Core instead:

```bash
cd MEditService && python stryker-report.py --all
```

Allow up to 3 minutes. The script prints the computed scope before running so you can confirm it is correct.

The script exits 0 if all mutants killed, 1 if any survivors or NoCoverage mutants remain.

## Performance notes

- **Initial test run** (~60s) — Stryker builds a coverage map. Fixed overhead regardless of scope.
- **Mutation phase** — only mutants covered by at least one test are exercised. Auto-scoping shrinks this phase to seconds.

## Reading the report

The script prints each surviving or uncovered mutant with:
- File path and line number
- Mutator name and what the mutation changed
- 3 lines of source context with the mutated line marked `>>>`
- A ready-to-paste suppression snippet

Status meanings:
- **Killed** — caught by a test (good)
- **Survived** — not caught (test gap; investigate)
- **NoCoverage** — no test exercises this code at all
- **CompileError** — Stryker's mutation made the code uncompilable; skipped automatically. Two known offenders: `DuckDbRecordRepository.Index` and `SchemaReflector.GetSubFieldInfo` (both use `out` variable patterns that confuse Stryker). Always appear as compile errors — ignore them.

## Handling survivors

Triage each survivor in order. Stop at the first step that resolves it — do not skip ahead to suppression.

**Step 1 — Needs coverage:** Add a test that fails on the mutated code and passes on the original. This is almost always the right answer.

**Step 2 — Delete the code:** If the survivor is a guard against a state that cannot happen, the guard is dead code. Remove it entirely.

**Step 3 — Simplify the code:** Redundant conditions can often be reduced so the mutant no longer exists.

**Step 4 — Config suppression (mutator category):** If a whole mutator category is inert by convention across the project (e.g. `String` mutations on exception message text), suppress it in `stryker-config.json` under `ignore-mutants` with a comment in the PR description explaining why.

**Step 5 — Source-level annotation (last resort):**

Before writing a `// Stryker disable` comment, you **must** invoke the rubber-duck agent:

```
/rubber-duck  [describe the mutant, the code, and your reasoning that it is inert]
```

The annotation must explain both why the code exists **and** why the mutation is inert. An annotation without reasoning is not acceptable and will be rejected in review.

## Suppression format

### Source-level (line)

```csharp
// Stryker disable once <MutatorName>: <reason>
someCode();
```

### Source-level (block)

```csharp
// Stryker disable <MutatorName>: <reason>
someCode();
moreCode();
// Stryker restore <MutatorName>
```

### Config-level (mutator category across the project)

```json
"ignore-mutants": [
  { "mutant": "StringLiteral", "description": "Exception message text is not tested by design" }
]
```

### Common mutator names

| Mutator | What it changes |
|---|---|
| `ConditionalBoundary` | `>` ↔ `>=`, `<` ↔ `<=` |
| `Equality` | `==` ↔ `!=` |
| `LogicalOperator` | `&&` ↔ `\|\|` |
| `StringLiteral` | string contents |
| `Arithmetic` | `+` ↔ `-`, `*` ↔ `/` |
| `BooleanLiteral` | `true` ↔ `false` |
| `NullCoalescing` | `??` removal |
| `RemoveConditional` | removes `if` condition |

## Known issues

- The `progress` reporter crashes when Stryker is not attached to a real TTY. Use `"dots"` instead (already set in `stryker-config.json`).
- The initial test run is slow (~60s) because the test suite loads Mutagen types and DuckDB infrastructure. This is fixed overhead — cannot be avoided by scoping.
