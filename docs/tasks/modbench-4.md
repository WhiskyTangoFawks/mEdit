# Modbench-4 — Deployer (standalone mode) (was M-3)

**Status: Not Started**
**Recommended model: Sonnet 4.6** — clearly specified, but watch the filesystem edge cases (never overwrite vanilla, cross-volume fallback, crash-recovery via manifest); escalate to Opus if those get hairy.

*Goal: let the game run with mods — hardlink the merged view into the game directory's `Data/`, purge it back out, launch the game. Standalone mode only; hidden when an external manager (MO2/Vortex) owns deployment.*

Spec: [mod-manager.md](../mod-manager.md) ("Deployment Model: Hardlinks", Feature Specs §4 Deploy/Purge/Launch). Prereq: Modbench-2, Modbench-3 (winner map). Effort: ~1 wk.

`fs.link`/`fs.symlink` are native to Node — no P/Invoke, no platform-specific VFS code.

## Extension

- [ ] `Deployer.deploy` — for each `winner` entry, `fs.link(source, Data/<relativePath>)`. Skip when `Data/<relativePath>` exists and is not a prior-deploy hardlink (never overwrite a vanilla file).
- [ ] Write `mods/.medit-manifest.json` listing every link created.
- [ ] `Deployer.purge` — delete each manifested hardlink; move `Data/` files not in the manifest and not vanilla → `mods/overwrite/` (F4SE outputs, MCM INI writes); delete the manifest.
- [ ] Same-volume check at first deploy; on violation, offer stock-folder setup or `fs.symlink` fallback (warn on Windows: needs admin/Developer Mode).
- [ ] Deploy/Purge in the Mod List header; hidden when an external manager owns deployment.
- [ ] "Launch Game" command — deploy → switch to Plugin List → launch the configured executable from the game directory → purge on exit.

## Tests

- [ ] Unit/integration: deploy creates hardlinks for winners and a manifest; vanilla files are never overwritten.
- [ ] Unit/integration: purge removes only manifested links and collects stray `Data/` files into `mods/overwrite/`.
- [ ] Unit: same-volume violation triggers the fallback path.

## Open questions

Overwrite-folder UX (reassign/discard surface) and confirming the manifest covers crash-recovery (dangling links). See spec "Open Questions".

## Proof

*To be filled in on completion. Paste `npm run test:unit` / `test:integration` output and commit hash here.*
