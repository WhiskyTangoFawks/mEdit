# Modbench-9 — Plugin load order (was M-8)

**Status: Not Started**
**Recommended model: Sonnet 4.6** — small surface, but auto-sort is a topological sort with cycle handling where correctness matters; above Haiku territory.

*Goal: manage plugin load order (`plugins.txt`) in the Plugin List view, with dependency-aware auto-sort.*

Spec: [mod-manager.md](../mod-manager.md) (Feature Specs §7 "Plugin Load Order"). Prereq: Modbench-2 (`plugins.txt` source). Effort: ~3 days.

> **Planning prerequisite — request screenshots.** Before planning, ask the user for a screenshot of MO2's **plugin list** (load-order index, reorder, missing-master indicator) to map it onto the Plugin List tree rather than guessing the presentation.

## Extension

- [ ] Extend the existing Plugin List tree: load-order index inline; missing-master `✗` badge (via `MasterReader`).
- [ ] Drag-and-drop reorder writes `plugins.txt`.
- [ ] Auto-sort — topological sort by master dependencies (simplified LOOT: dependency ordering only, no rule database).

## Tests

- [ ] Unit: drag reorder writes the expected `plugins.txt`.
- [ ] Unit: auto-sort orders a master before its dependents; detects/handles a cycle.

## Proof

*To be filled in on completion. Paste `npm run test:unit` / `test:integration` output and commit hash here.*
