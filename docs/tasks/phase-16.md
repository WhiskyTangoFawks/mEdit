# Phase 16 — Per-Plugin Worldspace / Cell / Placed-Object Tree + CRUD

**Status: 16.1 complete (read + display + edit). 16.2 in progress (create/copy/delete).**

*Goal: WRLD/CELL/REFR render in their xEdit-style spatial hierarchy **per plugin** (what
that plugin declares — records and overrides, not a winner), and the standard record
operations (edit, create, copy-as-override, delete) work on placed objects.*

Design: placed objects (REFR/ACHR) are indexed as normal records; GRUP parentage lives in
`placement` / `cell_location` side tables populated by a reflection-based structural pass.
Reads/detail/CRUD/agent are uniform DuckDB queries. See [ADR-0023](../adr/0023-placed-objects-indexed-with-placement-side-tables.md)
and UI_SPEC §2.6.

## 16.1 — Index + read + display + edit (DONE)
- [x] Un-exclude `refr`/`achr` (`SchemaReflector`) → reflected record tables
- [x] `placement` / `cell_location` side tables (`TableDdlBuilder`) + indexes
- [x] `PlacementWalker` (game-agnostic) + `DuckDbRecordRepository.IndexPlacement` structural pass, incl. `pos_x/y/z`
- [x] `WorldspaceQueryService` + `GET /plugins/{plugin}/worldspaces`, `/worldspaces/{fk}/blocks`, `/cells/{fk}/references`, `/interior-cells`
- [x] Record detail for REFR works via existing `GET /records/{fk}` (refr indexed); field edits round-trip via existing pipeline (`ApplyFieldChanges` descends into cells)
- [x] Extension: per-plugin "Worldspaces" + "Interior Cells" nodes; WRLD → Block → Sub-block → Cell → Persistent/Temporary → placed; click opens editor; interior-cell pagination; spatial/placed types hidden from flat list
- [x] Tests: indexing pass, repository reads, block grouping, tree provider; `dotnet test` 700+/`npm run test:unit` 261 green; integration green

## 16.2 — Create / copy-as-override / delete placed objects (TODO)
- [ ] `pending_changes` placement intent columns (`parent_cell`, `placement_group`)
- [ ] `PluginWriter` cell-aware create/copy/delete (GetOrAddAsOverride parent cell; add/remove in Persistent/Temporary)
- [ ] `EditOrchestrator` placed paths (`CreatePlacedRecord`; copy/delete capture placement from `placement`)
- [ ] Walk surfaces pending-created/copied placed refs under their target cell; pending deletes hidden
- [ ] Frontend create/copy/delete actions on cell/group/placed nodes

## Proof

*16.1 (read + display + edit): `dotnet test` **723 passing**; `npm run test:unit` **261
passing**; integration **4 passing**; `npm run build` clean. `/simplify` + `/code-review`
(high) completed, no confirmed bugs. Mutation baseline 74% (changed-files scope); all
survivors triaged (tests strengthened, dead code removed) — confirming re-run **deferred to a
manual run**, see [followup-mutation-workflow.md](followup-mutation-workflow.md). Commit:
`9c41ea4` + the triage commit on branch `phase-16-1-worldspace-tree`.*

*16.2 proof + final commit hash to be filled in on completion.*
