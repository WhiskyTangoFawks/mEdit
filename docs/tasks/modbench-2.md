# Modbench-2 — Mod list core (was M-1)

**Status: Not Started**
**Recommended model: Opus 4.8** — the largest task, and the MO2 byte-faithful round-trip (separators/categories/metadata preserved) is a genuine unknown with no margin for sloppy serialization.

*Goal: a working MO2-compatible mod list — read an instance, show it, toggle and reorder mods, writing MO2 format back. No conflict detection, no deploy, no editor coupling yet.*

Spec: [mod-manager.md](../mod-manager.md) ("Modlist format & source adapters", "Mod List Tree", Feature Specs §2–3). Architecture: [ADR-0021](../adr/0021-mod-manager-in-extension.md), [MM ADR-0001](../../medit-vscode/src/modmanager/docs/adr/0001-mo2-native-modlist-format.md). Effort: ~1.5 wk.

> **Planning prerequisite — request screenshots.** This task maps MO2's mod-list UI onto a VS Code TreeView (no equivalent table widget), so the UI decisions can't be guessed. Before writing the plan, ask the user for screenshots of the MO2 behaviour: the mod list with the **profile dropdown** and visible columns, the per-mod **right-click context menu**, and how **separators/categories** render. Use them to settle: profile selector, enable/disable, drag-reorder, what shows in the row label vs `description` vs tooltip, and separator handling. (UI_SPEC has no Mod Manager section yet — record the resolved decisions there.)

All work is extension-side (`medit-vscode/src/modmanager/`); it is file/JSON work and never parses plugin binaries.

## Extension

- [ ] `GameDirectory` — `medit.gameDirectory` config with `GamePathDetector` autodetect fallback; one-time stock-game-folder setup option.
- [ ] `IModlistSource` over an in-memory modlist model; **MO2 adapter** (first-class): read `mods/<name>/`, the active profile's `modlist.txt` (`+`/`-`, top = highest priority) and `plugins.txt`, per-mod `meta.ini` (Nexus id/version). Default active profile from `ModOrganizer.ini` (`[General] selected_profile`).
- [ ] **Profile selection** — enumerate profiles from the instance's `profiles/` dir; let the user pick the active one (each profile has its own `modlist.txt`/`plugins.txt`). The selection is the session boundary — switching is a new session (wired in Modbench-5). Persist the choice (write `selected_profile` back to `ModOrganizer.ini`). UI mechanism is a UI-spec decision (MO2 uses a dropdown; see [UI_SPEC](../UI_SPEC.md) mod-manager section). **Out of scope:** per-profile isolated saves and base-game config (`local savegames` / INI) — optional MO2 features, deferred.
- [ ] **Native adapter** (first-class): writes MO2-format instances for fresh setups (no separate format).
- [ ] `ModListProvider` `TreeDataProvider` — sidebar Mod List view; name, version, enabled checkbox.
- [ ] Enable/disable — toggle the `+`/`-` prefix in `modlist.txt` through the active source.
- [ ] Manual ordering — `TreeDragAndDropController` reorders the mod's line in `modlist.txt` (top = highest priority).
- [ ] Round-trip fidelity — preserve unmodelled constructs (separators, categories, metadata) verbatim on write.

## Tests

- [ ] Unit: MO2 adapter reads a fixture instance into the model and writes it back byte-faithfully (separators/categories preserved).
- [ ] Unit: enable/disable and reorder produce the expected `modlist.txt`.
- [ ] Unit: profiles are enumerated from `profiles/`; selecting a profile reads that profile's `modlist.txt`/`plugins.txt` and persists `selected_profile`.
- [ ] Integration: Mod List tree renders the fixture instance; toggle/reorder/profile commands registered.

## Open question

MO2 round-trip fidelity needs a test corpus from real MO2 instances (see spec "Open Questions").

## Proof

*To be filled in on completion. Paste `npm run test:unit` / `test:integration` output and commit hash here.*
