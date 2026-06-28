# Scripts are Python, not TypeScript

The Phase 15 scripting engine uses Python for script bodies, despite TypeScript being the other language in the stack.

Python is the scripting language modders and data-heavy workflow users are most likely to know. It is also the language AI agents produce most reliably for data-manipulation tasks. Scripts are short, iterative, and data-focused — exactly the domain Python is strongest in. TypeScript would require either a Node subprocess or a bundling step, and is a worse fit for the user profile.

## Consequences

- Python must be available in the user's environment (not bundled).
- Agents generating scripts target Python.
- Scripts execute as HTTP clients of the backend, not a backend-spawned subprocess — see [ADR-0024](0024-python-scripts-are-http-clients.md). (This supersedes an earlier consequence that had the backend spawn a Python subprocess via `POST /script/run`.)

## Considered options

**TypeScript / JavaScript** — already in the stack. Would require spawning Node or a bundling step. Not the natural scripting language for the target user, and agents produce less idiomatic data-manipulation code in JS than Python. Rejected.
