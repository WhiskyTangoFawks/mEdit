# Modbench-8 — Nexus update check (was M-7)

**Status: Not Started**
**Recommended model: Haiku 4.5** — small and self-contained: one API call, a version compare, a badge; lowest complexity in the track.

*Goal: surface "update available" when the Nexus version exceeds the installed version.*

Spec: [mod-manager.md](../mod-manager.md) (Feature Specs §5, "↓ Update available"). Prereq: Modbench-3 (status badges), Modbench-7 (API key). Effort: ~2 days.

## Extension

- [ ] Query the Nexus API for the latest file version per mod (using the `meta.ini` Nexus id).
- [ ] Compare against the installed version in `meta.ini`; set the `↓ Update available` badge in `StatusChecker`.
- [ ] Requires an API key (Modbench-7); degrade gracefully when absent.

## Tests

- [ ] Unit: a higher Nexus version than `meta.ini` sets the update badge; equal/lower does not.
- [ ] Unit: missing API key disables the check without error.

## Proof

*To be filled in on completion. Paste `npm run test:unit` output and commit hash here.*
