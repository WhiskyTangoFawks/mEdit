# ADR-0019 — xEdit Unified Tree Model for the Compare Grid

**Status**: Accepted

## Context

The compare grid shows field values across all loaded plugin overrides for a record. For scalar fields (string, int, FormKey, enum), each field is one row and the display is straightforward. For complex fields — arrays and structs — a design decision is required.

The original implementation rendered arrays as a per-cell `ArrayRowGroup` widget: each plugin column got its own independent list widget. This made cross-plugin element comparison impossible — you could not see that plugin A has `KeywordA` while plugin B does not, because the two plugins' arrays were rendered separately. For agentic edits, the pending column showed the entire new array as a JSON blob, making review impractical.

xEdit (the reference tool that all mEdit users will be familiar with) uses a different model: a single unified tree where every element — subrecord, struct sub-field, array element — is a node. The tree has one shape; each node carries one slot per plugin. Plugin columns are aligned at every depth. Sorted arrays align by sort key across plugins; unsorted arrays align by position.

## Decision

Adopt the xEdit unified tree model for the compare grid.

Arrays generate `children` in `FieldDiff` using the same recursive expansion mechanism already used for struct sub-fields:

- **Sorted arrays**: child count = union of sort keys across all plugins. Each child row represents one unique element (by FormKey or other sort key). A plugin that lacks that element shows an empty cell in its column.
- **Unsorted arrays**: child count = max element count across all plugins. Elements are aligned by index.

The parent array row shows the field name and collapses/expands the element sub-rows. Element sub-rows show each plugin's value for that element. Struct-typed array elements expand further into their own sub-field rows.

Pending display: for an element sub-row, the pending value is extracted from the pending whole-array value at that index/key. Only rows where the pending element differs from the disk element are highlighted. The revert button lives on the parent array row only — reverting restores the entire committed array atomically. No per-element revert affordance exists.

`ArrayRowGroup` is removed from the compare grid. It was the previous approach and is superseded by this model.

## Consequences

- `ConflictClassifier.BuildDiffs()` gains `BuildArrayChildren()` — the sorted/unsorted alignment logic.
- The recursive depth limit currently applied to struct sub-fields (skipping nested arrays and nested structs) is lifted for this model; depth is bounded by the data schema.
- The pending column display logic for array element rows must extract the element value from the parent pending change's stored JSON array.
- Agent review quality improves significantly: only changed element rows are highlighted, making it immediately obvious what an agent modified in a complex field.
- `ArrayRowGroup` either becomes dead code or is repurposed for contexts outside the compare grid.

## Alternatives Considered

**Per-cell ArrayRowGroup** (prior implementation): each plugin column renders its own array widget independently. Discarded because cross-plugin element comparison is impossible and the pending review experience is unreadable (full JSON blob).

**Element-level pending changes** (`field_path = "packages[1]"`): store one pending change per touched element. Discarded because array indices are positional and have no stable identity — any insert or delete invalidates all higher-index pending changes. The atomic whole-column model is the correct fit for the data structure.
