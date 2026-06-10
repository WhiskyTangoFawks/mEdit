# Phase 9.8 — Struct Sub-Row Display

**Status: Not Started**

*Design to be finalized in a grill session before implementation.*

## Goal

Replace the current in-cell nested-table expansion for struct fields with true sub-rows in
the main diff table. The expand toggle lives in the field-name column; each sub-field gets
its own full-width row with per-plugin value cells and per-cell conflict coloring.

Arrays (simple scalars and struct-element) and VMAD are out of scope for the initial cut.

---

## Current Behavior

`FieldDiff("Marker", values: {...})` → one `<tr>`. Inside each plugin `<td>`,
`StructRowGroup` renders a nested `<table>`. The toggle is per-cell; each plugin column
expands independently.

## Desired Behavior

- **Parent row**: field name `<td>` has a ▶/▼ toggle; value `<td>` cells show `{…}` collapsed
  or nothing expanded.
- **Child rows**: one `<tr>` per sub-field (`Type`, `Flags`, …), each spanning all plugin columns.
  Conflict coloring driven by per-sub-field `CellStates` (extends phase-9.7 Issue 2 pattern).
- **Editing**: sub-field cells use `ScalarCell`/`FormKeyCell`. On commit, reconstruct the full
  struct JSON from sibling values and call `onEdit(plugin, parentField, reconstructed)`.

---

## Implementation Options

### Option A — Pure Frontend Expansion (no backend changes)

Frontend parses the struct `JsonElement` using `FieldMetadata.fields` and synthesizes sub-rows.
Sub-field conflict coloring computed inline in TypeScript.

- **Pro**: No API change; fast to implement.
- **Con**: Duplicates `ConflictClassifier` logic in TypeScript. Sub-fields get no server-authoritative
  `CellStates`; any conflict-algorithm change must be mirrored in two places.

### Option B — Backend `FieldDiff.Children` (recommended)

Add `IReadOnlyList<FieldDiff>? Children` to `FieldDiff`. `ConflictClassifier` populates children
for struct-typed fields by extracting per-sub-field values from each plugin's `JsonElement` and
diffing them (reuses existing `ValuesEqual`). Each child gets its own `Values` + `CellStates`.

Frontend renders child `FieldDiff` entries as indented `<tr>` rows via the existing `DiffRow`
component (pass `depth` prop).

- **Pro**: Conflict logic stays in C#. `CellStates` extends cleanly. Frontend stays dumb.
- **Con**: Larger backend change. Variable-length arrays (different element counts per plugin) need
  careful handling and are deferred.

### Option C — Hybrid (rejected)

Frontend parses for display; backend learns field-path patches (`marker.type`). Worst of both
worlds — duplicate display logic AND a new backend concept.

---

## Deferred

- Array sub-rows (indexed `[0]`, `[1]`, …) — variable length across plugins is complex.
- VMAD — too deeply nested; render as readonly JSON viewer indefinitely.

---

## Proof

*To be filled in on completion.*
