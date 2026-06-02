# DuckDB schema is generated at startup via C# reflection

Rather than maintaining a hand-written SQL schema that mirrors Mutagen's C# types, the schema is generated at startup by reflecting over Mutagen's record type definitions (`typeof(Npc) → properties → CREATE TABLE npc (...)`). When Mutagen adds or changes fields, the schema updates automatically on next startup. Scalar fields become typed columns; arrays and deeply nested structs become JSON columns.

If the Mutagen assembly version hash changes, all generated tables are dropped and recreated; plugin data is reindexed from binary files.

## Type mapping

| C# type | DuckDB column |
|---|---|
| `string`, `TranslatedString` | `VARCHAR` |
| `int`, `short` | `INTEGER` |
| `float`, `double` | `FLOAT` |
| `bool` | `BOOLEAN` |
| `enum` | `VARCHAR` (name) |
| `FormLink<T>` | `VARCHAR` (FormKey string) |
| Nested struct (1 level) | Inline columns with prefix |
| Nested struct (2+ levels) | `JSON` |
| `ExtendedList<T>` | `JSON` |

## Considered options

**Hand-written schema** — Doubles the maintenance surface; any Mutagen update that adds a field requires a migration. Rejected.

**Pure JSON blob per record** — Schema-stable but sacrifices field-level queryability (`WHERE health > 200` requires JSON path functions, which are slower than typed columns). Rejected in favor of the hybrid: typed columns for scalars, JSON for arrays.
