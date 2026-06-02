# Mutation Test

Run Stryker.NET mutation tests against `MEditService.Core`. All commands run from `MEditService/`.

## Tool: Stryker.NET

Installed as a global dotnet tool (`dotnet stryker`). Config lives at `MEditService/stryker-config.json`.

The run always has two phases:
1. **Initial test run** (~60s) — Stryker runs all 188 tests once to build a coverage map. This cost is fixed regardless of scope.
2. **Mutation phase** — only mutants covered by at least one test are actually exercised. Scoping via `mutate` shrinks this phase dramatically.

## Glob pattern rule

The `mutate` glob in `stryker-config.json` is matched against **absolute file paths**. Always prefix with `**/` or the pattern will never match anything:

```json
// correct
"mutate": ["**/Queries/ConflictClassifier.cs"]

// wrong — silently filters everything out
"mutate": ["MEditService.Core/Queries/ConflictClassifier.cs"]
```

## Running against specific new code (preferred when validating a task)

The full Core sweep takes 20–30 minutes. When validating a specific change, scope `mutate` to the file(s) you touched. This keeps the mutation phase to seconds:

```bash
cd MEditService
# edit stryker-config.json to set mutate to your changed files, then:
dotnet stryker --config-file stryker-config.json
```

Example `mutate` values:

| Changed code | mutate value |
|---|---|
| Single file | `["**/Queries/ConflictClassifier.cs"]` |
| Whole folder | `["**/Queries/**/*.cs"]` |
| Multiple files | `["**/Queries/RecordQueryService.cs", "**/Schema/ColumnSpec.cs"]` |
| All of Core | `["**/MEditService.Core/**/*.cs"]` |

Edit `stryker-config.json` before each run. Do not commit scope-narrowed configs — restore `mutate` to the full-Core value when done.

## Running against all of Core (full sweep)

Remove or set `mutate` to match all Core files:

```json
"mutate": ["**/MEditService.Core/**/*.cs"]
```

Then run:

```bash
cd MEditService
dotnet stryker --config-file stryker-config.json
```

Expect ~20–30 minutes. The HTML report opens at the path printed at the end of the run.

## Reading results

- **Killed** — mutant was caught by a test (good)
- **Survived** — mutant was not caught (test gap, consider adding a case)
- **NoCoverage** — no test exercises that code at all
- **CompileError** — Stryker's mutation made the code uncompilable; these are skipped automatically. Two known offenders: `DuckDbRecordRepository.Index` and `SchemaReflector.GetSubFieldInfo` (both use `out` variable patterns that confuse Stryker). These will always appear as compile errors and can be ignored.

The HTML report (path printed at run end) has per-file, per-mutant detail including the exact source change Stryker made.

## Handling survivors

Triage each survivor into **needs coverage** or **suspected inert**.

**Needs coverage:** add a test that fails on the mutated code and passes on the original.

**Suspected inert:** work through these steps in order before accepting it:

1. **Delete** — if the code is a guard against an impossible state, remove it entirely. Dead code is a liability.
2. **Simplify** — redundant conditions (four null checks that are always equivalent) can often be reduced. Use `!` to document invariants rather than untestable defensive throws.
3. **Config suppression** — if a whole mutator category is inert by convention across the project (e.g. `String` mutations on exception message text), suppress it in `stryker-config.json`.
4. **Source annotation** — last resort. The comment must explain why the code exists *and* why the mutation is inert. An annotation with no reasoning is not acceptable.

## Known issues

- The `progress` reporter crashes when Stryker is not attached to a real TTY. Use `"dots"` instead (already set in `stryker-config.json`).
- The initial test run is slow (~60s) because the test suite loads Mutagen types and DuckDB infrastructure. This is fixed overhead — cannot be avoided by scoping.
