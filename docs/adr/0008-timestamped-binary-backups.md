# Undo uses timestamped binary backups

Before any write operation, the target plugin is copied to a timestamped backup (`MyMod.esp.2024-01-15T14-32-00.bak`). Within a session, in-memory undo is available via Mutagen's object graph (pre-edit state held in memory, flushed only on explicit save). Cross-session undo is the `.bak` file. The last N backups are kept; older ones are pruned. This is the same pattern xEdit uses.

## Considered options

**Git on YAML** — Git is valuable for text; binary plugins are opaque to Git and produce no meaningful diffs. Excluded when YAML was excluded as an intermediate format.

**Event sourcing / operation log** — Correct but overengineered for v1. The `.bak` pattern is sufficient. Rejected.
