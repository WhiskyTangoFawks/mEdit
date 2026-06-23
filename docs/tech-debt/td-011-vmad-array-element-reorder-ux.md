# TD-011 — VMAD scalar-array element reorder needs UX specification

**Status:** Open  
**Source:** Phase 13.6 planning — deferred pending UX spec  

---

## Problem

Phase 13.6 adds add/remove editing for VMAD scalar-array properties (`ArrayOf{Int,Float,Bool,String,Object}`).
Reorder (moving an element up or down within the array) was intentionally omitted because no UX spec exists for it.

## Proposed interaction model (not yet specified)

Right-click context menu on an element row in the VMAD array section → "Move Up" / "Move Down" options.
Operations should be scoped to the plugin column that was right-clicked, consistent with all other edit operations in the compare grid.

## Why this is deferred

- The xEdit-unified-tree model (ADR-0019) describes sorted/unsorted alignment across plugins but doesn't prescribe a reorder interaction.
- Right-click context menus in webview table rows are not yet spec'd in `docs/UI_SPEC.md`.
- The add/remove operations introduced in 13.6 deliver the bulk of the editing value; reorder is a secondary affordance.

## Scope (when prioritised)

1. Update `docs/UI_SPEC.md` with the right-click context menu spec for array element rows.
2. `medit-vscode/webview/src/VmadSection.tsx` — context menu handler on element `<tr>`; call `onEdit(plugin, arrayVmadPath, reorderedArray)`.
3. No backend change needed — the same atomic-column pending model applies.
