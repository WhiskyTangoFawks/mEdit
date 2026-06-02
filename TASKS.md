# mEdit — Task Roadmap

**POC complete** (Phases 0–7 + M). Core stack operational: plugin loading, DuckDB index, record compare grid, inline edit + save, FormKey picker, session wizard, backend lifecycle.

**Target game (v1):** Fallout 4. Multi-game architecture complete (Phase M); other games need NuGet packages + extension wiring.

---

## Completed Phases ✓

| Phase | Summary |
|-------|---------|
| **0** | Solution scaffold — C# backend + VS Code extension compile and start; `GET /health` live |
| **1** | Plugin loading — `IPluginLoader`, `PluginMetadata`, `IFormKeyResolver`; integration test |
| **2** | DuckDB index — `SchemaGenerator`, `RecordIndexer`, `UpdateWinners`, `SessionCache`; winner test |
| **3** | Read API — `/plugins`, `/record-types`, `/records`, `/records/{fk}`, `/records/{fk}/compare` |
| **4** | Write API — `PATCH /records/{fk}`, `POST /copy-to`, `GET/DELETE /changes`, `POST /save`; `PluginWriter`; backups |
| **5** | VS Code extension — backend lifecycle, status bar, session wizard, game path detection, generated API client |
| **5.1** | Tree drill-down — plugin → record type → record nodes; pagination; click → `mEdit.openEditor` |
| **6** | Webview read-only — compare grid (field × plugin), conflict highlighting, FormKey links |
| **M** | Multi-game architecture — `GameRelease` threaded through stack; implicit plugin loading; immutable base-game enforcement |
| **7** | Webview edit mode — inline field editing, pending change columns, revert, save, copy-to, `FormKeyPicker` |
| **8** | UI polish: immutability enforcement (lock icon, read-only badge, pending column suppression); error surfacing (409 → "Plugin is read-only"); `POST /plugins/create`; "New Plugin…" + "Copy as Override Into…" commands; `api.ts` regenerated with all write endpoints |
| **A** | Architectural cleanup — `SchemaReflector`/`TableDdlBuilder` split, `IConflictClassifier` extracted, `PluginWriter` apply-function tests, `SessionManager` thread-safety audit, `PluginFixtureBuilder`, naming pass, RFC 7807 error model, parameterized SQL |

---

## Phase B — Pending Change Model Redesign

*Design-first phase. Do not implement Phase 10 (Record Lifecycle) until this design is settled — Phase 10 extends the pending change model and will need to be revised against whatever is decided here.*

### Open questions to resolve (design work, not implementation)

**Storage**
- Current model: `ConcurrentDictionary` in memory — changes are lost if the backend restarts.
- DuckDB is not an option for persistence: it is rebuilt from disk on every session load (see ADR-0001).
- Options: keep in-memory (accept loss), SQLite sidecar file (persist across restarts), or treat loss-on-restart as a feature (forces explicit save discipline).
- Decision needed: is persistence across backend restarts a requirement? If yes, what stores it?

**Granularity: field delta vs full record snapshot**
- Current model: one `PendingChange` per `(FormKey, Plugin, FieldPath)` — field-level deltas.
- Alternative: one pending entry per `(FormKey, Plugin)` storing the full new record state.
- Field deltas: precise, revertable per-field, displayable as "these specific things changed." Hard to apply multi-field operations atomically.
- Full record: simpler apply path (just write the whole record), but loses field-level diff visibility and makes partial revert awkward.
- Decision needed: which model? Or a hybrid (store full record snapshot but track which fields changed)?

**Merge semantics**
- Current model already upserts: re-editing the same field updates `NewValue` in place, preserving the original `OldValue`. You cannot stack two pending changes for the same field — there is always at most one.
- This is probably correct. Confirm it is the intended behaviour.

**Multi-record operations (prerequisite for Phase 10)**
- Phase 10 introduces `ChangeGroup` — a named group of changes spanning multiple records (e.g. delete + nullify all FormLink references, or renumber a FormKey across all referencing plugins).
- Design needed: how does a group relate to individual pending changes? Can you revert one change in a group independently, or must the group be reverted atomically?
- Design needed: how are groups surfaced in the UI — as a collapsible section in the pending changes panel, or as a separate view?

**UI**
- Current: pending changes appear as an extra column in the compare grid for the affected record.
- For multi-record operations, there is no UI design yet.
- Design needed: a pending changes panel (sidebar or bottom panel) that shows all staged changes across all records, grouped by plugin and optionally by `ChangeGroup`. Must support per-field revert, per-record revert, per-group revert, and save.

### Deliverable
A written design (ADR or TASKS note) that answers all five questions above before any implementation begins.

---

## Phase 9 — Conflict Classification & Filtering

*Goal: users can see the conflict landscape at a glance and drill into only the records that matter.*

Model decided in ADR-0016: two-axis classification (`ConflictAll` per record row + `ConflictThis` per plugin column). See CONTEXT.md for the full enum definitions.

**Tier 1 (this phase):** Two-axis classification using Mutagen typed values. ConflictPriority per field is Tier 2 (later).

**Filter design:** filter dimensions are `conflictAll`, `hasPendingChanges`, and free-text `editorId`. They compose with AND. No separate `GET /conflicts` endpoint — `GET /records?conflictAll=Conflict` is sufficient. The tree toolbar maps directly to query params.

### Backend

**Conflict classifier (`MEditService.Core/Queries/ConflictClassifier.cs`)**
- [ ] Add `ConflictAll` and `ConflictThis` C# enums matching CONTEXT.md definitions
- [ ] `ConflictClassifier.Classify(IReadOnlyList<(string plugin, IRecord record)> overrideStack)` — takes the override stack in load-order position; returns `(ConflictAll, IReadOnlyList<(string plugin, ConflictThis)>)`. Algorithm: compare each plugin's field values against the master (position 0) and against the winning override (last position). Fields absent in a PartialForm record are excluded from comparison.
- [ ] Store `conflict_all` (string enum) in a DuckDB `conflict_state` table keyed on `form_key`, populated/invalidated on every index update — this is the read model for filtering, not for the compare grid. The classifier itself runs in C# over full Mutagen objects.
- [ ] Expose `ConflictAll` and per-plugin `ConflictThis` on the `GET /records/{fk}/compare` response — each plugin column in the compare response gets a `conflictThis` field; the record-level response gets a `conflictAll` field.
- [ ] `GET /records?conflictAll=Conflict|ConflictCritical|Override|ConflictBenign|NoConflict|OnlyOne` — filter parameter maps directly to the `conflict_state` DuckDB table; composable with existing `plugin`, `recordType`, `editorId` filters
- [ ] `GET /plugins/{plugin}/conflicts` — conflict records where this plugin's `ConflictThis` is `ConflictWins` or `ConflictLoses`; sourced from the compare endpoint data

**Display name improvements** (needed for conflict usability)
- [ ] REFR, ACHR, PGRE, PMIS record summaries: resolve the base object FormLink and use its `FULL` name as the display name — bare EditorIDs are nearly always empty on placed objects; xEdit shows the base object's name instead
- [ ] CELL records without EditorID: display as grid coordinates `<X, Y>` from the `XCLC` field

### Extension
- [ ] Top-level "Conflicts" tree node showing total conflict count badge; lazy-loads `GET /records?conflictAll=Conflict,ConflictCritical`
- [ ] Conflict and override badge icons on record nodes in the tree (drives `contextValue` on tree nodes)
- [ ] Filter toolbar on plugin tree: "All" / "Conflicts Only" / "Overrides Only" toggle — maps to `conflictAll` query param
- [ ] `mEdit.showConflicts` command (palette + tree toolbar button)
- [ ] General record tree filter: free-text search by EditorID or FormKey, record type dropdown; composable with conflict-state toggle via AND

### Webview
- [ ] Record row background color driven by `ConflictAll`: no color (OnlyOne/NoConflict), green (Override), yellow (ConflictBenign), orange (Conflict), red (ConflictCritical) — same palette xEdit uses
- [ ] Per-plugin column cell color driven by `ConflictThis`: grey (IdenticalToMaster), green (Override), yellow (ConflictBenign), orange (ConflictWins), red (ConflictLoses), no color (Master/OnlyOne)
- [ ] PartialForm columns: absent fields are omitted from the column (empty cell, no color), not shown as blank — add a tooltip or italicised "partial" badge on the column header

### Tests
- [ ] Backend: `ConflictClassifier` returns `ConflictAll=Conflict`, winning plugin `ConflictThis=ConflictWins`, losing plugin `ConflictThis=ConflictLoses` for a two-plugin fixture where both override the same field with different values
- [ ] Backend: `ConflictClassifier` returns `ConflictAll=Override`, both plugins `ConflictThis=IdenticalToMaster` when the override copies values identically
- [ ] Backend: `ConflictClassifier` correctly handles a PartialForm record in the stack (absent fields do not generate ConflictLoses)
- [ ] Backend: `GET /records?conflictAll=Conflict` returns only conflicting records from the index
- [ ] Backend: free-text `editorId` filter on `GET /records` returns matching records across all loaded plugins
- [ ] Backend: REFR compare response uses base object FULL name as display name when EditorID is absent

---

## Phase 9.5 — Conflict Classification Tier 2: ConflictPriority Refinements

*Prerequisite: Phase 9 (Tier 1 classifier working). Goal: match xEdit's field-priority-aware conflict detection for the common cases that affect every load order.*

This phase requires building a lookup table of `(record_type, field_name) → ConflictPriority` derived from the xEdit definition files in `TES5Edit/Core/wbDefinitionsFO4.pas`. The priority values change which differences register as conflicts and which are silently benign.

### Backend
- [ ] Build `ConflictPriorityTable`: a static `Dictionary<(string recordType, string fieldName), ConflictPriority>` populated by parsing the xEdit definitions or hand-extracted from them. Key entries: `cpIgnore` fields (XLRT, PNAM/FNAM on some records), `cpBenign` fields, `cpBenignIfAdded` (XLRL Location Reference on REFR/CELL/WRLD records)
- [ ] `ConflictClassifier` consults `ConflictPriorityTable` per field before comparing: skip `cpIgnore` fields entirely, cap results at `ConflictBenign` for `cpBenign` fields, apply `cpBenignIfAdded` logic (benign only when absent in master)
- [ ] Sorted array detection: mark known sorted arrays (Script Properties, Quest Aliases, Door Links, Weather Types) so the classifier matches elements by sort key rather than by array index before comparing
- [ ] Injected record detection: if a record's FormKey origin plugin is not a declared master of the override plugin, treat as `cpCritical` and bump to `ConflictCritical`

### Tests
- [ ] `cpIgnore` field (e.g. XLRT on a REFR record) does not contribute to ConflictAll even when values differ
- [ ] `cpBenignIfAdded` field (XLRL) is ConflictBenign when absent in master, ConflictNormal when present and differing
- [ ] Injected record receives ConflictCritical
- [ ] Sorted array: two overrides that sort to the same order are NoConflict regardless of insertion order

---

## Phase 10 — Record Lifecycle Operations

*Goal: full create / delete / renumber lifecycle for records, with cascading safety checks and atomic rollback.*

### Pending change model changes
- [ ] Add `ChangeType` enum to `PendingChange`: `FieldEdit | Create | Delete` — `FieldEdit` is the current behavior; `Create` and `Delete` have no meaningful `FieldPath`/`OldValue`/`NewValue` and must be handled separately in `PluginWriter` and `PendingChangeService`
- [ ] Add `GroupId: Guid?` to `PendingChange` — `null` for standalone field edits, set for grouped operations (delete, renumber); included in all `GET /changes` responses so the UI can discover the group from any individual change
- [ ] Add `ChangeGroup` entity `{ Id, Operation, Description, CreatedAt }` tracked in `PendingChangeService`
- [ ] `PATCH /records/{formKey}` returns 409 if the record has any pending change in a group; error detail names the group — user must commit or revert the group first
- [ ] `DELETE /changes/{changeId}` returns 409 if the change belongs to a group — must use `DELETE /changes/group/{groupId}` instead
- [ ] `DELETE /changes/group/{groupId}` — atomically reverts every change in the group
- [ ] Edit-not-stack semantics: `PATCH /records/{formKey}` upserts into the existing `PendingChange` for the same `(FormKey, Plugin, FieldPath)` — no duplicate field entries; standalone edits can be freely re-edited

### ChangeGroup access
- [ ] `GET /changes?groupId={id}` — extend existing `GET /changes` with a `groupId` filter; returns all `PendingChange` records in the group
- [ ] `GET /change-groups` — list all active `ChangeGroup` records `{ id, operation, description, createdAt, changeCount }`; gives the UI a summary of in-flight multi-record operations without scanning individual changes

### New record
- [ ] `POST /plugins/{plugin}/records` with body `{ type: string, templateFormKey?: string }` — creates a blank record of the given type, or copies from template if `templateFormKey` is provided
- [ ] Stages as standalone pending changes (single record, no group needed)
- [ ] Supersedes `POST /records/{formKey}/copy-to/{targetPlugin}`; remove old endpoint

### Delete records
- [ ] **Prerequisite:** `form_references` table from Phase 11
- [ ] `POST /records/delete` with body `{ records: [{ formKey: string, plugin: string }] }` — batch; stages a single `ChangeGroup` covering all deletions + nullification of intra-plugin FormLink fields pointing to any deleted record
- [ ] Returns 409 if any record in the batch is referenced by another plugin or an immutable plugin; response body lists which records blocked the operation
- [ ] Returns 409 if a pending group is already active for any FormKey in the batch

### Renumber FormID
- [ ] **Prerequisite:** `form_references` table from Phase 11
- [ ] `POST /records/{formKey}/renumber` with body `{ newFormId: uint, plugin: string }`
- [ ] Returns 409 if any references are in immutable plugins (cannot update them)
- [ ] Stages a `ChangeGroup`: FormKey field update on the record + all reference field updates across editable plugins

### Save path
- [ ] `POST /plugins/{plugin}/save` returns 409 if any group it would drain spans multiple plugins — the caller must use the group save endpoint instead
- [ ] `POST /change-groups/{groupId}/save` — saves all plugins touched by the group atomically: drain the group, write each plugin via `PluginWriter`, re-index all affected plugins; fails as a unit if any write fails
- [ ] `PluginWriter`: add `Create` code path — call Mutagen record creation API, then apply field changes on top
- [ ] `PluginWriter`: add `Delete` code path — call Mutagen record removal API
- [ ] `PluginWriter`: add `Renumber` code path — change the FormKey in Mutagen (distinct from a field edit)
- [ ] Re-index after renumber must rebuild `form_references` rows for the affected FormKey (old rows removed, new rows inserted)
- [ ] **Known gap:** pending `Create` records are not visible in `GET /records` or the tree until after save + re-index; accepted for now, can be addressed later with a pending-creations overlay

### Tests
- [ ] `PATCH` on a record with a pending group change returns 409
- [ ] `DELETE /changes/{id}` on a group-owned change returns 409
- [ ] `DELETE /changes/group/{id}` reverts all changes in the group atomically
- [ ] `GET /changes?groupId={id}` returns only the changes for that group
- [ ] `GET /change-groups` lists active groups with correct `changeCount`
- [ ] `POST /plugins/{plugin}/records` without template creates a blank record pending change
- [ ] `POST /plugins/{plugin}/records` with `templateFormKey` stages a copy (same behavior as old copy-to)
- [ ] `POST /records/delete` returns 409 with blocking records listed when external references exist
- [ ] `POST /records/delete` with a valid batch stages one `ChangeGroup` covering all records
- [ ] Renumber returns 409 when an immutable plugin holds a reference
- [ ] Successful renumber stages changes for the record and all referencing editable-plugin fields

---

## Phase 11 — Referenced By / Record Graph

*Goal: see every record that references a given FormKey — essential for understanding the impact of a change, and a prerequisite for Phase 10 delete and renumber.*

### Backend
- [ ] Add `form_references (source_form_key, source_plugin, target_form_key, field_path, record_type)` table to DuckDB, populated at index time — for every FormLink field encountered during indexing, write one row; this is the indexed read model for reference queries and is required for Phase 10 delete/renumber safety checks
- [ ] `GET /records/{formKey}/references` — queries `form_references` table; returns `{ formKey, editorId, plugin, fieldPath }[]`

### Extension / Webview
- [ ] "Referenced By" tab in the record panel (alongside the compare grid); lazy-loads on tab click
- [ ] Each reference entry: plugin chip + record EditorID + field path; clicking opens that record
- [ ] Empty state: "No references found"

### Tests
- [ ] Backend: `form_references` is populated correctly for a fixture with a known FormLink field
- [ ] Backend: references endpoint returns the referencing NPC when a weapon FormKey is searched
- [ ] Backend: unknown FormKey returns empty array (not 404)

---

## Phase 12 — Struct/Array Field Types

*Goal: complex fields (keyword lists, NPC traits, weapon damage entries) render instead of being silently omitted, with full type safety derived from Mutagen's reflection model.*

### Backend
- [ ] `SchemaGenerator`: serialize `IReadOnlyList<T>` / `ExtendedList<T>` as JSON `VARCHAR`; emit `type: 'array'` in field metadata; element type recursively reflected
- [ ] `SchemaGenerator`: for nested struct properties (getter interfaces, C# value types), walk the type's own properties recursively via reflection to produce a `fields: FieldMetadata[]` sub-schema — same shape as top-level field metadata, so the frontend gets `name`, `type`, `enumValues`, `validFormKeyTypes` at every nesting level
- [ ] Sub-schema generation is recursive (structs can contain FormLinks, enums, further structs); stop at primitives and known leaf types
- [ ] `PluginWriter`: handle JSON round-trip for array and struct fields on write; use sub-schema to apply individual sub-field writes with correct types (no raw string coercion)

### Extension / Webview
- [ ] `<ArrayRowGroup>`: collapsible row-group; each element a child row; add/remove in edit mode
- [ ] `<StructRowGroup>`: collapsible row-group; each property a child row with type-correct cell (uses sub-schema `FieldMetadata` to drive `ScalarCell` / `FormKeyCell` / nested group)
- [ ] Edit inputs for struct sub-fields and array elements are driven by the sub-schema type — no free-text JSON entry; the type hierarchy from Mutagen reflection is the source of truth
- [ ] Collapsed by default; expand state persisted per session
- [ ] Enum scalar fields render as `<select>` dropdown in edit mode; option list sourced from schema `enumValues`; displayed as enum name in read mode (never raw integer)
- [ ] Flag fields (bit-flag enums, e.g. NPC flags) render as a multi-select dropdown with per-flag checkboxes in edit mode; displayed as comma-separated active flag names in read mode

### Tests
- [ ] Backend: `SchemaGenerator` emits `type: 'array'` for a known list property (e.g. `IKeywordGetter` list)
- [ ] Backend: struct sub-schema contains correct `FieldMetadata` entries (names + types) for a known Mutagen getter interface
- [ ] Backend: array field survives round-trip through write → re-index → read
- [ ] Webview: enum field renders a `<select>` with the correct options in edit mode
- [ ] Webview: flag field renders checkboxes; toggling one flag updates only that bit in the pending value

---

## Phase 14 — Plugin File Management

*Goal: operations mod authors need for preparing plugins for distribution or integration.*

### Backend
- [ ] `POST /plugins/{plugin}/compact-formids` — renumber non-master FormIDs into 0x001–0xFFF range for ESL eligibility; returns `{ remapped: int, backupPath: string }`
- [ ] `POST /plugins/{plugin}/convert` — toggle ESL/ESM flag; request body `{ targetType: "esp"|"esm"|"esl" }`
- [ ] `POST /plugins/{plugin}/masters/add` — add a new master reference to the plugin header
- [ ] `POST /plugins/{plugin}/masters/sort` — reorder masters to match current load order
- [ ] `POST /plugins/{plugin}/masters/clean` — remove unused master references (not referenced by any record)
- [ ] `POST /plugins/merge` — merge source plugin records into target plugin; adjusts FormID mapping; creates backup
- [ ] `POST /plugins/{plugin}/records/inject-to-master` — move records from this plugin into its declared master: transfers the record definition into the master plugin and removes it from the dependent; adjusts FormIDs and all intra-load-order references; creates backups of both plugins before writing
- [ ] Master auto-update on copy-to: when `PluginWriter` writes a copied record into a target plugin, automatically add the source plugin as a master of the target if not already present; `POST /copy-to` must never leave a plugin referencing a FormKey whose origin is not declared in the header

### Extension
- [ ] Plugin context menu: "Compact FormIDs", "Convert to ESL / ESM", "Add Master…", "Sort Masters", "Clean Masters", "Inject Forms into Master…", "Merge Into…"
- [ ] Confirmation dialogs for all destructive operations
- [ ] Result notification (backup path, counts)

### Tests
- [ ] Backend: `compact-formids` renumbers records and updates all cross-references within the plugin
- [ ] Backend: `masters/clean` removes only the unreferenced master
- [ ] Backend: `inject-to-master` moves record into master and removes it from the dependent; both plugins updated atomically
- [ ] Backend: copy-to automatically adds the source as a master of the target plugin when the master declaration is absent

---

## Phase 15 — Scripting Engine

*Goal: power users write Python scripts against the loaded mod data — the xEdit scripting experience, native to VS Code.*

### Design

Scripts are Python files with a YAML frontmatter block. The frontmatter declares a SQL query that selects the records the script operates on. The script body iterates the query results and calls `edit()` to stage changes. All edits flow through `PendingChangeService` → `PluginWriter` — the same pipeline as manual field edits.

```python
# ---
# name: Scale Nord NPCs
# description: Make all Nord NPCs 10% taller
# context: global
# query: |
#   SELECT form_key, plugin, record_type, height
#   FROM npc WHERE race_editor_id = 'NordRace'
# ---

for row in records:
    edit(row.form_key, row.plugin, row.record_type, "Height", row.height * 1.1)
```

- SQL is the selection layer — leverages DuckDB for filtering, joins across plugins, aggregates
- `edit(form_key, plugin, record_type, field, value)` is the only write API; routes to the `ColumnSpec.Apply` delegate for that `(record_type, field)` pair, same as a UI edit
- Column names in query results are the same names `ColumnSpec` uses (both derived from the same `SchemaReflector` reflection) — no separate stub generation needed
- Scripts run in a Python subprocess; the extension communicates via stdin/stdout JSON-RPC; the user never sees HTTP or transport details
- Scripts are read-only by default (no `edit()` calls); any `edit()` call stages a pending change that the user can review and save or discard

### Backend
- [ ] `POST /query` — execute a SQL SELECT against DuckDB; returns `{ columns: string[], rows: unknown[][] }`; read-only (no DDL/DML); scripts do their own selection here
- [ ] `POST /script/run` — accepts `{ script: string, context: ScriptContext }`; executes the Python subprocess, collects `edit()` calls, stages them as pending changes via `PendingChangeService`; returns `{ editsStaged: number, log: string[] }`
- [ ] `GET /scripts` — list available scripts from user-configurable folder + built-in `extension/scripts/`; returns `{ name, description, context }[]`

### Script format
- [ ] YAML frontmatter: `name`, `description`, `context` (`record | plugin | global`), `query` (SQL string)
- [ ] Token substitution in `query`: `{{formKey}}`, `{{plugin}}`, `{{editorId}}`, `{{type}}` — substituted from context before execution
- [ ] `edit(form_key, plugin, record_type, field, value)` — the only write API available to scripts; raises if `(record_type, field)` has no `ColumnSpec`

### Extension
- [ ] "Run Script…" command on tree context menu + command palette; QuickPick populated from `GET /scripts`
- [ ] Script output panel (append-only log of script stdout + edits staged summary)
- [ ] User setting: `mEdit.scriptsPath` for custom script folder

### Built-in scripts (`extension/scripts/`)
- [ ] `find-references.py` — lists all records referencing current FormKey
- [ ] `list-overrides.py` — lists all FormKeys with >1 override for current plugin
- [ ] `find-itms.py` — finds ITM records in current plugin
- [ ] `conflict-summary.py` — prints conflict counts by record type

### Tests
- [ ] Backend: `POST /query` returns correct columns and rows for a SELECT
- [ ] Backend: `POST /script/run` stages correct pending changes for a script that calls `edit()`
- [ ] Backend: `POST /script/run` rejects a script whose `edit()` references an unknown `(record_type, field)`

---

## Phase 16 — Worldspace / Cell Tree

*Goal: WRLD and CELL records render in their correct spatial hierarchy in the tree, matching xEdit's world-tree structure.*

Background: Bethesda plugins use two distinct record hierarchies for placed objects. **Worldspaces** (WRLD) group CELL records spatially into blocks → sub-blocks → cells (identified by XCLC grid coordinates), each containing Persistent and Temporary REFR groups. **Interior cells** (CELL without a worldspace parent) appear as a flat list. The current tree shows CELL and REFR as a flat list under their record type — this phase restructures them into the correct hierarchy.

### Backend
- [ ] `GET /worldspaces` — returns WRLD records for the session; each entry: `{ formKey, editorId, plugin }`
- [ ] `GET /worldspaces/{formKey}/blocks` — spatial hierarchy for one worldspace: `{ blocks: [{ x, y, subBlocks: [{ x, y, cells: [{ formKey, editorId, cellX, cellY }] }] }] }`; XCLC coordinates sourced from the `XCLC` field on each CELL record
- [ ] `GET /cells/{formKey}/references` — REFR records inside a specific CELL, split into `{ persistent: RecordSummary[], temporary: RecordSummary[] }`; Persistent vs Temporary derived from the child-group type in the plugin binary structure
- [ ] `GET /interior-cells` — CELL records with no worldspace parent; supports pagination

### Extension
- [ ] "Worldspaces" top-level tree node (alongside existing plugin/record-type nodes); lazy-loads `GET /worldspaces`
- [ ] WRLD child nodes expand to Block nodes (labelled e.g. "Block 0, 0"); Block nodes expand to Sub-block nodes; Sub-block nodes lazy-load CELL children from `GET /worldspaces/{fk}/blocks`
- [ ] CELL node labelled by XCLC grid coordinates (e.g. "Cell (12, -5)") or EditorID when present; click opens record editor
- [ ] Persistent and Temporary child nodes under each CELL; lazy-load REFR children from `GET /cells/{fk}/references` on expand
- [ ] REFR leaf nodes labelled as "EditorID [REFR:FormID]"; click opens record editor
- [ ] "Interior Cells" top-level node; lazy-loads `GET /interior-cells`

### Tests
- [ ] Backend: `GET /worldspaces/{fk}/blocks` returns correct block/sub-block/cell nesting for a fixture with a known WRLD record
- [ ] Backend: XCLC coordinates are correctly read from cell records and reflected in the response
- [ ] Backend: `GET /cells/{fk}/references` separates persistent and temporary REFRs correctly
- [ ] Backend: `GET /interior-cells` returns only cells that have no WRLD parent

---

## Phase 17 — Record Editor Column Interactions

*Goal: the compare grid supports the column-level operations xEdit users expect — collapsing noisy columns, moving values between overrides, and acting on a whole override at once.*

### Webview
- [ ] Left-click on a plugin column header collapses that column to minimal width (just the plugin name chip); click again to expand; collapsed state persisted per record panel session
- [ ] Drag-drop of a field cell value between plugin columns: drops the source value as a pending field change into the target plugin's column; target must be an editable plugin; dragging from a read-only column is allowed (copy, not move)
- [ ] Visual drag affordance on cells in edit mode (cursor change, subtle grab handle)

### Extension / Webview
- [ ] Right-click on plugin column header in record editor context menu:
  - "Copy All to Pending" — copies every field value from this column into a pending change for the active editable plugin (equivalent to xEdit "copy as override" from the column header)
  - "Copy as New Record" — copies all field values as a new record pending change in the active editable plugin
  - "Remove Override" — stages a delete of this plugin's override of this record (delegates to Phase 10 delete; disabled for immutable plugins)

### Tests
- [ ] Webview: collapsed column stores state in component; re-click restores full width
- [ ] Webview: drag-drop from a source column stages the correct pending field change for the target plugin
- [ ] Webview: "Copy All to Pending" context menu action stages pending changes for all fields visible in the column

---


## Deferred / Stretch Goals

### Near-term deferred
- **Non-FO4 game support** — backend architecture complete (Phase M); blocked on adding `Mutagen.Bethesda.Skyrim`, `.Oblivion`, `.Starfield` NuGet packages + extension game-picker wiring
- **Backend binary bundled in VSIX** — package .NET self-contained binary into the extension so users don't need a separate install step
- **MO2 native reconstruction** — doc: add backend exe to MO2 Tools, start from MO2 → attached mode works normally

### Power / analysis features
- **Build Reachable Info** — graph traversal from known entry points through all record references; marks unreachable records stricken-through; complex, low ROI for most users
- **Conflict resolution assistant** — "Apply All Wins" batch action: copies all winning-override field values to a designated patch plugin in one operation
- **Diff export** — save conflict report (all overrides for selected records) to `.txt` or `.html`
- **Circular leveled list detection** — recursive CTE query to find cycles in `lvln`/`lvli` chains
- **Batch field edits** — `PATCH /records` supporting multiple FormKeys in one request for bulk operations

### Future explorations
- Sideloading
    * Open plugin file outside a load order (mutagen grabs the default steam load order to deal with masters)
    * Import/Export from Spriggit
- Agentic integration - ACP/MPC?
- Extra mutagen tooling
    * Analysis
    * Merge Plugins
    * ???
- **REFR spatial rendering** — select placed-object (`REFR`) records, render their 3D cell positions on a top-down map; use DuckDB spatial extension (`ST_Within`, radius queries) for proximity searches; requires a Three.js or Canvas 2D renderer webview
- Navmesh editting
- Previsibine generation
- **Asset handling** — resolve loose-file and BA2-packed assets referenced by records (textures, meshes, sounds); repeat XEdit hash textures so faction paintjob distribution can me migrated
- Vector DB for semantic lookup with standalone MCP server -> this work is inherently template based, so being able to do a lookup is going to be fairly critical for a more automated agent -> need to dump the FO4 wiki here too...