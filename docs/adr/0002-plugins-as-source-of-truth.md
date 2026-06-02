# Plugins are the source of truth

The `.esp`/`.esm`/`.esl` binary files on disk are the authoritative source of record data. No intermediate format is introduced. The plugin is what the game reads, what every other tool in the ecosystem understands, and what the user ships — there is no drift problem, no synchronization problem, and no format translation cost.

## Considered options

**YAML via Spriggit** — Spriggit serializes plugins to YAML for version control. Using YAML as a working format introduces three representations of the data (binary, YAML, index), a hard runtime dependency on a .NET CLI tool, 3–5× disk amplification, and an unclear session lifetime. Rejected.

**SQLite as source of truth** — A database that can be deleted and rebuilt from the plugins in under 30 seconds is a cache, not a source of truth. Treating it as authoritative inverts the dependency. Rejected.
