# Phase 15 — Scripting Engine

**Status: Not Started**

*Prerequisites: Phase B.1 (DuckDB-backed PendingChangeService), Phase B.2 (pending-overlay read views). Goal: power users write Python scripts against the loaded mod data — the xEdit scripting experience, native to VS Code.*

## Architecture

Python scripts are **HTTP clients of the existing backend** — not a backend-spawned subprocess ([ADR-0024](../adr/0024-python-scripts-are-http-clients.md)). A small `medit` Python package wraps the same endpoints the VS Code extension uses, so scripts are symmetric with the extension: one data path, no second transport (cf. ADR-0018, "identical interfaces for humans and agents, no separate data path").

Consequences of the client model:

- **Staging is respected by construction.** `edit()` calls the existing stage endpoint with `source: "script"`, lands in `PendingChangeService` exactly like a manual edit, and the user reviews/approves/discards in the VS Code UI before anything is written. Scripts cannot bypass the approval gate because they never touch the write pipeline directly.
- **Zero Mutagen access.** Scripts see JSON over HTTP, never a Mutagen type. The C# endpoints are the only code that touches Mutagen.
- **No raw shared-DB writes.** Scripts never write to DuckDB directly (that would bypass `ColumnSpec.Apply` validation and reference-validation-at-stage-time, ADR-0020). Game-data writes go only through the stage endpoint.
- **Runs anywhere.** Scripts run from the extension's "Run Script" command, a terminal, a REPL, a notebook, or CI — debuggable with normal Python tooling. The extension's command is a convenience that spawns `python script.py` with the backend URL in env.

```python
# ---
# name: Scale Nord NPCs
# description: Make all Nord NPCs 10% taller
# context: global
# query: |
#   SELECT form_key, plugin, record_type, height
#   FROM npc WHERE race_editor_id = 'NordRace'
# ---

for npc in records:
    npc.set("Height", npc.height * 1.1)   # row carries its own form_key/plugin/record_type
```

## Read/write contract

- **Selection — raw SQL SELECT.** The `query:` block runs via `POST /query` against the Phase B.2 **overlay views**, so scripts read committed + staged state (the world as it will be after save). SQL stays the selection layer (consistent with ADR-0018): DuckDB does filtering, joins, and aggregates; agents already generate SQL reliably. No query-builder DSL.
- **Writes — structured only.** `edit()` / `row.set()` route to the stage endpoint → `ColumnSpec.Apply` → `PendingChangeService`. Raises if `(record_type, field)` has no `ColumnSpec` or is read-only (the existing `StageEditResult.ReadOnlyFields` / `InvalidReferences` variants surface as Python exceptions).
- **Script compute is unrestricted, but local.** Heavy intermediate work (temp tables, aggregation) lives in the script's own in-memory DuckDB or pandas — never the shared index.
- **Scope: scalar leaf fields first.** `value` is a JSON scalar initially. Struct/array/VMAD values (nested JSON) are additive later — no transport change needed.

## API shape

Level-1 ergonomics: SQL selection, but query results are row objects that carry their own identity so writes don't repeat it.

- `records` — iterable of row objects; column access by attribute (`npc.height`) using the same names `ColumnSpec` uses (both derived from `SchemaReflector`).
- `row.set(field, value)` — stages an edit to *this* row's record.
- `edit(form_key, plugin, record_type, field, value)` — module-level, for editing a record not in the result set.
- Domain helpers (`find_references`, `list_overrides`, …) ship as **example scripts**, not core API — they are compositions of query + edit. Keep the core API tiny.

## Open decisions

1. **Bulk vs per-record staging.** `PATCH /records/{fk}` is one record per call → N edits = N round trips. Recommend adding a **bulk stage endpoint** (one atomic `ChangeGroup` per run + a per-record error report) over client-side accumulate-and-flush.
2. **Within-run read visibility.** If edits buffer client-side, a re-`query()` won't see them. Decide flush-before-query vs "reads = committed + previously-staged only." Tied to (1).
3. **Token substitution** (`{{formKey}}`, `{{plugin}}`, `{{editorId}}`, `{{type}}`) — done client-side in the `medit` lib from the run context, before the SQL is sent.
4. **Backend URL** — env var injected by the extension when spawning; default `localhost:5172` standalone.

## Backend

- [ ] `POST /query` — execute a SQL SELECT against the overlay views; returns `{ columns: string[], rows: unknown[][] }`; SELECT-only (reject DDL/DML)
- [ ] (decision 1) bulk stage endpoint — accept many records' `{ formKey, plugin, fields }` in one call, stage as one `ChangeGroup`, return per-record results
- [ ] *No* `POST /script/run` — the backend does not spawn Python. *No* backend `GET /scripts` — script discovery is extension-side filesystem listing.

## `medit` Python package

- [ ] HTTP client wrapping `POST /query` and the stage endpoint; backend URL from env
- [ ] Frontmatter parser (YAML) + client-side token substitution
- [ ] Row objects with attribute access + `row.set()`; module-level `edit()`
- [ ] `StageEditResult` HTTP problem responses → typed Python exceptions

## Script format

- [ ] YAML frontmatter: `name`, `description`, `context` (`record | plugin | global`), `query` (SQL string)
- [ ] `edit()` raises if `(record_type, field)` has no `ColumnSpec`

## Extension

- [ ] "Run Script…" command on tree context menu + command palette; spawns `python script.py` with backend URL in env; QuickPick populated from filesystem listing of `mEdit.scriptsPath` + built-in `extension/scripts/`
- [ ] Script output panel (append-only log of script stdout + edits-staged summary)
- [ ] Reuses `mEdit.scriptsPath` and Code Lens infra from ADR-0018

## Built-in scripts (`extension/scripts/`)

- [ ] `find-references.py` — lists all records referencing current FormKey
- [ ] `list-overrides.py` — lists all FormKeys with >1 override for current plugin
- [ ] `find-itms.py` — finds ITM records in current plugin
- [ ] `conflict-summary.py` — prints conflict counts by record type

## Tests

- [ ] Backend: `POST /query` returns correct columns and rows for a SELECT, and reflects a staged edit (overlay view)
- [ ] Backend: `POST /query` rejects DDL/DML
- [ ] `medit`: a script calling `edit()` stages the correct pending change with `source: "script"`
- [ ] `medit`: a script editing an unknown `(record_type, field)` raises (maps `ReadOnlyFields`/validation problem to exception)

## Proof

*To be filled in on completion. Paste `dotnet test` output, `npm run test:unit` output, and commit hash here.*
