# Modbench-6 — Mod installation from archive/folder (was M-5)

**Status: Not Started**
**Recommended model: Sonnet 4.6** — mostly mechanical (extract, detect root, normalize, write `meta.ini`); the only judgment call is the archive-lib decision, which is bounded.

*Goal: install a mod from a local archive or folder into an MO2-format mod folder.*

Spec: [mod-manager.md](../mod-manager.md) (Feature Specs §1 "Mod Installation"). Prereq: Modbench-2. Effort: ~1 wk.

## Extension

- [ ] "Install from Archive…" (`.zip`/`.7z`/`.rar`) and "Install from Folder…".
- [ ] Extract to a temp staging directory.
- [ ] Detect root type (`Data/` subfolder vs `.esp`/meshes at root) and normalise to a flat mod folder.
- [ ] Write `mods/<name>/` + `meta.ini` (Nexus id/version if known) via the active `IModlistSource`; append to the profile's `modlist.txt` as disabled.
- [ ] FOMOD detection — flag `fomod/ModuleConfig.xml` mods for manual setup; do **not** implement the scripted installer (separate sub-project).

## Open question

Archive extraction: Node has no native 7z/RAR — shell out to `7z` or use a Node archive lib. Decide here (spec "Open Questions").

## Tests

- [ ] Unit: archive with a `Data/` root and archive with files at root both normalise to a flat `mods/<name>/`.
- [ ] Unit: install writes `meta.ini` and appends a disabled line to `modlist.txt`.
- [ ] Unit: a FOMOD archive is flagged, not auto-installed.

## Proof

*To be filled in on completion. Paste `npm run test:unit` output and commit hash here.*
