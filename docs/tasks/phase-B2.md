# Phase B.2 — Pending-Overlay Read Views

**Status: Not Started**

*Goal: reads reflect staged changes. Generate per-record-type DuckDB views that overlay `pending_changes` onto the committed base tables, so all SQL — the ADR-0018 filter, Phase 15 scripts, and internal read paths — queries current state without hand-written overlay logic. Decision recorded in [ADR-0025](../adr/0025-reads-overlay-pending-via-views.md).*

## Why

- Today the filter runs `CREATE OR REPLACE TABLE _filter AS ({sql})` against the **committed** base tables ([DuckDbRecordRepository.cs:876](../../MEditService/MEditService.Core/Records/DuckDbRecordRepository.cs#L876)) — staged edits are invisible to filter SQL. Phase 15 scripts inherit the same blind spot.
- Overlay logic currently has to be hand-written into each query that cares (e.g. `GetReferences` merges `pending_changes` by hand at [DuckDbRecordRepository.cs:687](../../MEditService/MEditService.Core/Records/DuckDbRecordRepository.cs#L687)). A view centralizes it.
- DuckDB (engine behind DuckDB.NET 1.5.2) has **no materialized views** — only logical `CREATE VIEW` (re-evaluated at query time) or `CREATE TABLE AS` snapshots. A logical view is the *right* tool here: `pending_changes` mutates on every `edit()`, so a snapshot would need constant refresh + invalidation. A logical view is always live against current `pending_changes` with zero refresh.

## Design

- Committed base table per type stays one row per `(form_key, plugin)` (ADR-0006), renamed `<type>_committed`. A logical view `<type>` exposes current state. Users/scripts/agents keep querying `<type>` (ADR-0018) and now see committed + staged by default.
- View per type = `(committed LEFT JOIN field-overlays) UNION (pending creates) EXCEPT (pending deletes)`:
  - **Field edits** — `COALESCE` the staged value over the committed column, cast to the column type. The common case; sufficient for Phase 15 scalar edits and the filter.
  - **Creates** — `UNION` pending-created rows (lands with Phase 10).
  - **Deletes** — anti-join to exclude (lands with Phase 10).
- View DDL is **generated** from `RecordTableSchema` in `TableDdlBuilder`, alongside the base-table DDL (ADR-0005) — not hand-maintained per type.
- `_filter` materialization and all record read queries repoint at the views.

## Open decisions

1. **Naming** — rename base → `<type>_committed` + view = `<type>` (recommended: keeps the user's mental model "`npc` = current state" and ADR-0018 names intact; cost: audit internal queries that assume `<type>` is committed-only — the conflict classifier genuinely needs committed-per-plugin and must point at `_committed`). Alternative: additive `<type>_current` view, base stays `<type>` (lower-risk, but users must remember `_current`).
2. **Value typing** — `pending_changes` stores values as JSON/serialized; the overlay must cast each staged value back to the committed column's type. Confirm per-type-kind cast coverage.
3. **Overlay shape/perf** — per-column correlated `COALESCE` vs a single pivot of that record's `pending_changes` rows into columns then `LEFT JOIN`. Measure on a realistic index.

## Backend

- [ ] Generate per-type overlay view DDL in `TableDdlBuilder` from `RecordTableSchema`
- [ ] Field-edit overlay: `COALESCE` staged-over-committed, typed cast
- [ ] Repoint `_filter` materialization ([DuckDbRecordRepository.cs:876](../../MEditService/MEditService.Core/Records/DuckDbRecordRepository.cs#L876)) and record read queries at the views
- [ ] (with Phase 10) creates/deletes overlay — `UNION` pending creates, anti-join pending deletes
- [ ] Remove hand-written overlay logic the view subsumes (e.g. `GetReferences` pending merge)

## Tests

- [ ] A staged field edit appears in `SELECT <field> FROM <type>` for that `form_key`
- [ ] Filter SQL referencing a staged value selects the record (proves the filter sees the overlay)
- [ ] `<type>_committed` still returns the on-disk value (overlay does not leak into committed reads)
- [ ] (Phase 10) a pending-created record appears in the view; a pending-deleted record is absent

## Proof

*To be filled in on completion. Paste `dotnet test` output, `npm run test:unit` output, and commit hash here.*
