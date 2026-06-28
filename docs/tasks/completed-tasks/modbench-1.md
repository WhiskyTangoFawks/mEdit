# Modbench-1 — `load-explicit` session spike

**Status: Complete** (branch `modbench-1-load-explicit`)
**Recommended model: Opus 4.8** — small but high-stakes and ambiguous: a cross-cutting session/lifecycle change that must hold against the single-session invariant; needs design judgment, not volume.

*Spike. Goal: de-risk the one cross-cutting architectural unknown in the mod manager — constructing a single backend session from a scattered, ordered list of physical plugin paths — before any mod-manager UI is built on top of it.*

Spec: [mod-manager.md](../mod-manager.md) ("Backend lifecycle"). Architecture: [ADR-0022](../adr/0022-extension-owns-backend-lifecycle.md), [ADR-0015](../adr/0015-single-session.md).

## Why this is first

- It is the **only backend change** in the entire mod-manager effort; everything else (Modbench-2→4, 6→9) is extension-side TS/filesystem work that does not touch the session model.
- It validates the riskiest claim cheaply: that a whole ordered session can be built from scattered physical paths, profile-bound, without violating single-session ([ADR-0015](../adr/0015-single-session.md)).
- It is the foundation for the future delta / overlay-editing feature.

Keep it a spike: a **hand-crafted** ordered path list pointed at a real MO2 instance's files. No tree UI, no `modlist.txt` parsing, no write-back, no deployer — those are Modbench-2 and Modbench-5.

## Backend

- [ ] Add a `load-explicit` session source: accept an ordered `{ name, physicalPath }[]` (enabled plugins + vanilla masters) and build the full ordered session + DuckDB index from it. Generalises the existing `GameSession.AddPlugin(filePath)` (arbitrary-path load) to construct the *whole* session from scattered paths, alongside the existing single-data-folder scan.
- [ ] Vanilla masters resolve from the configured game directory (included in the explicit list, or resolved as implicit masters).

## Validation

- [ ] Point at a real MO2 instance's files via a hand-written ordered list; confirm indexing succeeds and the existing read / compare / edit endpoints work against the resulting session.
- [ ] Confirm cross-plugin references and winners resolve per the explicit load order.

## Out of scope (→ Modbench-5)

`BackendManager` spawn/teardown, warm-across-toggle lifecycle, Mod List ⇄ Plugin List wiring, reading the path list from `modlist.txt`/`plugins.txt`.

## Tests

- [ ] Backend: a `load-explicit` session from N ordered paths indexes all records and resolves cross-plugin references / winners in load order.

## What was built

- `GameSession.LoadExplicit(gameDirectory, IReadOnlyList<(Name, Path)> plugins, gameRelease)` — generalises the data-folder scan. The overlay-open / metadata / link-cache loop was extracted into a private constructor shared by both sources; the only difference is how the ordered `(fileName, filePath, isImmutable)` list is resolved. Implicit masters resolve from `gameDirectory` (Mutagen `Implicits.Get`), immutability inferred from that set — no per-entry flag.
- `SessionManager.LoadExplicit(...)` — parallels `Load`; both share a private `IndexAndStore` and the same `lock`/`DisposeCurrentSession` swap, so the single-session invariant ([ADR-0015](../adr/0015-single-session.md)) holds.
- `POST /session/load-explicit` — `SessionLoadExplicitRequest(Plugins[], GameDirectory, GameRelease)` → named `SessionLoadResponse`. Regenerated `api.ts`.
- Test infra: `PluginFixtureBuilder.BuildScattered()` writes each plugin to its own folder (implicit masters → a game dir) and returns the ordered explicit list — self-contained, no dependency on any real install.

## Reality grounding (real LitR MO2 instance)

Hand-built the ordered list from the active profile's `plugins.txt` (enabled plugins resolved to their scattered `mods/<name>/` paths; vanilla masters implicit from `Stock Game Folder/Data`) via an uncommitted harness.

- **Full instance loads: 610 plugins indexed** (603 enabled + 7 implicit vanilla), `Fallout4.esm` alone ≈1.55M records. `GET /plugins` and record queries serve from the resulting session.
- **Cross-plugin winners resolve in load order**: UFO4P (loaded from `mods/Unofficial Fallout 4 Patch/`) wins 151 NPC overrides over vanilla `Fallout4.esm` (e.g. AmeliaStockton, ArlenGlass) — winner = highest load-order plugin, across scattered physical paths.
- **Finding → fixed (resilient load):** some user-authored plugins (`DangerousDeathclaws.esp`, `Lunar-UniqueCreatures.esp`) override creature races (`DeathclawRace`, `DLC03_FogCrawlerRace`) with `<32` biped-object-name `NAME` subrecords; Mutagen's FO4 RACE parser hardcodes exactly 32 ([Race.cs:116-123](../../Mutagen/Mutagen.Bethesda.Fallout4/Records/Major%20Records/Race.cs#L116-L123)) and throws (xEdit tolerates these). Originally this aborted the **whole** session load. Now session load is **resilient at whole-plugin granularity**: a plugin that can't be opened/parsed is skipped, logged (`LogWarning`), and returned in `SessionLoadResponse.Failures` (added to both `/session/load` and `/session/load-explicit`). The extension surfaces these via a VS Code **warning** + output log in `SessionWizard` — a skipped plugin is never silent. Verified: full LitR loads **611 plugins, 1 reported failure** (`Lunar-UniqueCreatures.esp`), HTTP 200.

  Granularity is per-plugin by deliberate choice (simplicity over chasing a Mutagen bug): per-(plugin,type) or per-record skip were considered and rejected — Mutagen's enumerator can't resume past a throw, and `race` *is* an indexed type, so finer granularity would mean fighting Mutagen. Open follow-ups: (a) upstream/patch the Mutagen FO4 RACE parser; (b) an in-ecosystem record-repair path (xEdit script) for the agent workflow.

## Proof

`dotnet test -v minimal` → **758 passed, 0 failed**. Frontend: `test:unit` 268 passed, `test:integration` 4 passing, `build` clean. New tests: `GameSessionLoadExplicitTests` (5, incl. resilient skip + missing-file warn-not-fail), `SessionManagerLoadExplicitTests` (3), `SessionApiTests` load-explicit + failure reporting (3), `SessionWizard.test.ts` failure surfacing (2).

Branched from `4d80d64`. Commits: `2910c2c` (implementation), `028af12` (mutation triage).
