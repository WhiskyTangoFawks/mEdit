# Modbench-7 — Nexus download integration (was M-6)

**Status: Not Started**
**Recommended model: Sonnet 4.6** — standard API/protocol-handler integration (`nxm://`, CDN exchange, queue, SecretStorage); moderate, well-trodden ground.

*Goal: install mods directly from Nexus via the `nxm://` protocol.*

Spec: [mod-manager.md](../mod-manager.md) (Feature Specs §6 "Nexus Download Integration"). Prereq: Modbench-6 (install flow). Effort: ~1 wk.

> **Planning prerequisite — request screenshots.** Before planning the queue UI, ask the user for a screenshot of MO2's **Downloads** tab so the download/queue presentation tracks the MO2 target rather than being invented.

## Extension

- [ ] Register the extension as OS handler for `nxm://` at install.
- [ ] `DownloadManager.Enqueue` — receive `nxm://fallout4/mods/{id}/files/{fileId}`, exchange for a CDN URL via the Nexus API, download to `downloads/` with status-bar progress.
- [ ] "Install now?" → hand off to the Modbench-6 install flow.
- [ ] API key in `vscode.SecretStorage`.
- [ ] Queue UI — status-bar item (`↓ N downloading`) opening a quick pick.

## Open question

Premium vs free download-link API differs (premium direct CDN, free a redirect) — handle both (spec "Open Questions").

## Tests

- [ ] Unit: `nxm://` URI parses to `{game, modId, fileId}`.
- [ ] Unit: download queue transitions (enqueue → downloading → done → install prompt) with the API mocked.

## Proof

*To be filled in on completion. Paste `npm run test:unit` output and commit hash here.*
