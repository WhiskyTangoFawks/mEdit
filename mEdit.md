# Bethesda Plugin Editor — Architecture Overview

## Project Goal

A modern, agent-friendly replacement for xEdit. Core workflows are **viewing**, **editing**, and **comparing** Bethesda plugin records across a load order. Everything else (conflict detection, ITM cleaning, patch creation) is a projection of those three operations.

---

## Key Architectural Decisions

### 1. Source of Truth: Binary Plugins

The `.esp`/`.esm`/`.esl` files on disk are the source of truth. Always.

No intermediate format is introduced into the data flow. The plugin is what the game reads, what every other tool in the ecosystem understands, and what the user ships. There is no drift problem, no synchronization problem, and no format translation cost.

**Alternatives excluded:**

- **YAML via Spriggit** — Spriggit's use case is version control for mod authors, not editing. YAML as a working format introduces three representations of the data (binary, YAML, index), a hard runtime dependency on a .NET CLI tool, 3–5x disk amplification, and an unclear session lifetime. It was considered and rejected.
- **SQLite as source of truth** — A database that can be deleted and rebuilt from the plugins in under 30 seconds is a cache, not a source of truth. Treating it as authoritative inverts the dependency.

---

### 2. Plugin Parsing: Mutagen (Direct Library)

Mutagen is used as a C# NuGet library, not shelled out via CLI. It provides strongly-typed C# objects for every record type in every supported game. It handles binary I/O, FormID↔FormKey resolution, master list management, and record schema validation.

**Alternatives excluded:**

- **Spriggit CLI** — A CLI wrapper around Mutagen, designed for YAML serialization. Adds process boundary overhead and constrains the interface to what Spriggit exposes. Using Mutagen directly gives full programmatic access.
- **xedit-lib** — The Delphi/Pascal library underlying xEdit and zEdit. Requires a native interop layer, is not idiomatic in any modern language, and has a narrow maintainer surface. zEdit's abandonment is partly attributable to this dependency.
- **Custom binary parser** — No justification for writing one when Mutagen exists, is actively maintained, and covers all target games.

---

### 3. Index Layer: DuckDB

DuckDB is used as an in-process analytical query engine. It holds a queryable index of all loaded records, derived from the plugins via Mutagen. It is a **cache**, not a source of truth — deleting it loses nothing, and it can be rebuilt from the plugins at any time.

The schema is **generated at startup via C# reflection** over Mutagen's record type definitions. There is no hand-written schema to maintain. Scalar fields become typed columns. Arrays and deeply nested structs become JSON columns. When Mutagen adds or changes fields, the schema updates automatically on next startup.

**Why DuckDB over SQLite:**

- Columnar storage makes GROUP BY and aggregation across hundreds of thousands of records fast enough to feel instant. Conflict detection across a 200-mod load order is a GROUP BY — SQLite makes this slow, DuckDB makes it sub-100ms.
- Native JSON column support with path queries (`data->>'$.field'`), not bolted on.
- Parallel query execution across cores.
- Recursive CTE support for graph traversal (circular leveled list detection, reachability).
- In-process like SQLite — no separate server process.

**Alternatives excluded:**

- **SQLite** — Adequate for single-plugin editing at modest scale. Falls short on analytical queries over full load orders. JSON support is an afterthought. Excluded in favor of DuckDB.
- **A graph database (Kuzu)** — The reference graph is genuinely a graph problem, and Kuzu (an embeddable graph DB with Python/C# bindings) would handle reachability queries more elegantly. Excluded for v1 in favor of DuckDB recursive CTEs, which are sufficient. Revisit if reachability analysis becomes a priority.
- **PostgreSQL / external databases** — Inappropriate for a local desktop tool. No server process should be required.

---

### 4. Schema Generation: Reflection-Driven at Startup

Rather than maintaining a hand-written SQL schema that mirrors Mutagen's C# types, the schema is generated at startup by reflecting over Mutagen's record type definitions.

```
typeof(Npc) → properties → CREATE TABLE npc (...)
```

**Type mapping:**

| C# Type | DuckDB Column |
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

The schema generator runs in milliseconds. If the Mutagen assembly version hash changes, all generated tables are dropped and recreated. Plugin data is reindexed from the binary files. Nothing is lost.

**Alternatives excluded:**

- **Hand-written schema** — Doubles the maintenance surface. Any Mutagen update that adds a field requires a migration. Excluded as unnecessary duplication.
- **Pure JSON blob per record** — Simple and schema-stable, but sacrifices field-level queryability. You can't write `WHERE health > 200` against a JSON blob without DuckDB JSON path functions, which are slower than typed columns. Excluded in favor of the hybrid: typed columns for scalars, JSON for arrays.

---

### 5. Override Model: One Row Per (FormKey, Plugin)

The records table uses `(form_key, plugin)` as its composite primary key. The same FormKey appears once per plugin that contains it — the original definition and every override.

```sql
-- Goblin NPC across three plugins
form_key              plugin              load_order_idx  is_winner
000984:Skyrim.esm     Skyrim.esm          0               false
000984:Skyrim.esm     SomeOverhaul.esp    2               false
000984:Skyrim.esm     MyPatch.esp         3               true
```

This structure makes every major operation a direct SQL query:

- **Winning record** — `WHERE form_key = ? AND is_winner = true`
- **Full override stack** — `WHERE form_key = ? ORDER BY load_order_idx`
- **Conflict detection** — `GROUP BY form_key HAVING COUNT(*) > 1`
- **ITM detection** — self-join where `data` is identical across two rows for the same FormKey
- **Field-level conflicts** — join two rows and compare individual typed columns

---

### 6. Session Model: mtime-Cached Index

On first load, Mutagen reads all plugins and populates DuckDB. On subsequent sessions, each plugin's `file_mtime` is compared against a stored timestamp. Unchanged plugins are not reindexed. Only modified plugins are reprocessed.

This makes repeat sessions feel instant for typical workflows (editing one plugin in a stable load order).

```sql
CREATE TABLE plugins (
    plugin          VARCHAR PRIMARY KEY,
    load_order_idx  INTEGER,
    is_master       BOOLEAN,
    is_light        BOOLEAN,
    is_writable     BOOLEAN,
    masters         VARCHAR[],
    record_count    INTEGER,
    file_mtime      TIMESTAMP
);

CREATE TABLE index_state (
    indexed_at        TIMESTAMP,
    load_order_hash   VARCHAR    -- hash of plugin list + all mtimes
);
```

---

### 7. Undo: Timestamped Binary Backups

Before any write operation, the target plugin is copied to a timestamped backup:

```
MyMod.esp.2024-01-15T14-32-00.bak
```

Within a session, in-memory undo is available via Mutagen's object graph (pre-edit state held in memory, flushed only on explicit save). Cross-session undo is the `.bak` file. The last N backups are kept; older ones are pruned.

**Alternatives excluded:**

- **Git on YAML** — Git is valuable for text. Binary plugins are opaque to Git. Meaningful diffs are not possible. Excluded when YAML was excluded.
- **Event sourcing / operation log** — Correct but overengineered for v1. The `.bak` pattern is what xEdit itself uses and it's sufficient.

---

## Language Choices

### C# — Backend Service

Everything that touches Mutagen or DuckDB is C#. This is not a preference — Mutagen is a C# NuGet library. Using it from any other language requires either a native interop layer or a process boundary, both of which add complexity for no benefit.

The backend is an **ASP.NET Core minimal API** running as a local process on localhost. It exposes a REST API and emits an OpenAPI spec automatically via Swashbuckle.

**Project structure:**

```
BethesdaPluginService/
  BethesdaPluginService.Core/
    Services/          ← Mutagen interaction, record loading
    Repositories/      ← DuckDB read/write
    Schema/            ← Reflection-driven schema generation
    Models/            ← DTOs, record representations
    Diff/              ← Field-level comparison logic
  BethesdaPluginService.Api/
    Program.cs         ← ASP.NET Core host, route registration
    Routes/            ← Endpoint handlers
```

**Key dependencies:**

- `Mutagen.Bethesda.*` — record parsing and writing
- `DuckDB.NET` — in-process DuckDB
- `Swashbuckle.AspNetCore` — OpenAPI spec generation
- `Autofac` — dependency injection (following SFRecordCompareEngine precedent)
- `Serilog` — structured logging

**Alternatives excluded:**

- **Python (FastAPI)** — Considered as the backend language. Excluded because it cannot use Mutagen directly. A Python layer sitting in front of a C# Mutagen service is a pure proxy — it adds latency and a language context switch with no benefit.
- **Node.js** — Same problem as Python. Also the approach zEdit took (Electron + native Node addon wrapping xedit-lib), and zEdit is abandoned.
- **C / C++** — No justification. Mutagen is C#. The ecosystem is C#. C interop would be a significant maintenance burden.

---

### TypeScript — VS Code Extension and Webview

VS Code extensions are TypeScript. The webview panels (React) are also TypeScript. This is not a choice — it is the VS Code extension model.

The TypeScript frontend consumes the C# service's OpenAPI spec to generate a fully typed API client at build time. No manual type maintenance.

**Extension structure:**

```
bethesda-plugin-editor/
  src/
    extension.ts          ← ~200 lines: commands, webview panels, tree view
    mutagenClient.ts      ← generated from OpenAPI spec
    treeProvider.ts       ← VS Code TreeView for plugin/record navigation
  webview/
    RecordView.tsx        ← recursive field renderer + edit form
    CompareView.tsx       ← side-by-side diff view
    FormKeyPicker.tsx     ← reference search component
```

**Key dependencies:**

- `react`, `react-dom` — webview UI
- `react-arborist` — virtualized tree for record navigation (handles 100k+ nodes)
- `shadcn/ui` + Tailwind — layout primitives (ResizablePanelGroup, Tabs, Command)
- `openapi-typescript` or `hey-api` — typed API client generation from OpenAPI spec

---

## The Two Workflows

### Single Plugin Editing

The user is authoring or modifying one plugin. Masters are loaded read-only for reference resolution (FormKey → EditorID lookups) but are not indexed deeply unless already cached.

```
Load MyMod.esp + masters
  → Mutagen reads all into memory
  → DuckDB indexed (masters: metadata only if uncached; working plugin: full)
  → Session live

Edit record
  → PATCH /records/{formKey}
  → Mutagen updates in-memory object graph
  → DuckDB row updated

Save
  → POST /plugins/save
  → Mutagen writes MyMod.esp to disk
  → .bak created before write
  → DuckDB mtime updated
```

### Modlist Editing / Conflict Resolution

The user is working across a full load order, identifying conflicts, and writing overrides into a patch plugin.

```
Load full load order (N plugins)
  → Mutagen reads all
  → DuckDB indexed: one row per (FormKey, plugin)
  → Conflicts immediately queryable

View conflict
  → GET /records/{formKey}/compare
  → Returns all override rows ordered by load_order_idx
  → CompareView renders side-by-side with field-level diff coloring

Resolve conflict
  → POST /records/{formKey}/copy-to/MyPatch.esp
  → Mutagen copies winning record into patch plugin
  → User edits specific fields
  → PATCH /records/{formKey}?plugin=MyPatch.esp

Save
  → POST /plugins/save?plugin=MyPatch.esp
  → Only the patch plugin is written to disk
  → Source plugins are never modified
```

---

## Visualization Layer

### What Is Not Built

- 3D rendering of any kind
- BSA/BA2 archive browser
- Worldspace/cell map viewer
- Texture/mesh preview

These are separate tool concerns. LOD generation belongs to xLODGen. Asset browsing belongs to dedicated asset tools.

### What Is Built (Minimum Viable Surface)

**Record Tree** — left panel. Plugin → record type → record hierarchy. Virtualized via `react-arborist`. Handles 100k+ nodes without performance degradation.

**Record View** — right panel, single plugin mode. A recursive React component that switches on field type:

```
RecordView
  ├── ScalarField     (typed input: string, int, float, bool)
  ├── FormKeyField    (clickable link → navigation; search picker in edit mode)
  ├── FlagField       (checkbox group for enum flags)
  ├── StructField     (collapsible section → recurse)
  └── ArrayField      (list of items → recurse; reorderable)
```

Driven by the field type metadata returned by the API alongside the record data. The component does not need to know what an NPC is — it knows that `Health` is an integer and `Race` is a FormKey reference to RACE records.

**Compare View** — right panel, conflict resolution mode. The same recursive component rendered N times horizontally, one column per plugin, with per-cell background coloring:

- **Green** — this plugin's value matches the winning override
- **Red** — this plugin's value conflicts with the winner
- **Uncolored** — only one plugin touches this field (no conflict)

Field-level diff coloring is computed by the API (`GET /records/{formKey}/compare`), not in the frontend. The frontend receives a structured diff object and renders it.

**FormKey Picker** — the most important single UX element. Inline search input, debounced, backed by `GET /records?search=&type=`. Filters by valid target record types for the field (you cannot put a WEAP in a Race slot — the C# service knows the valid types from Mutagen's generic parameter). Renders as `Gold Jeweled Necklace [087835:Skyrim.esm]` — EditorID prominent, FormKey in muted text.

### VS Code Extension Strategy

**Do not fork an existing DuckDB viewer.** Install `ChuckJonas/duckdb-vscode` (or equivalent) as a peer extension. It handles all SQL browsing, schema inspection, and ad-hoc querying. The custom extension adds only the three domain-specific panels above, wired to the C# service.

The "VS Code extension" portion is ~200 lines of TypeScript registering commands and webview panels. The actual UI is a React app. This codebase is portable to Electron or Tauri with minimal changes if a standalone app is wanted later.

---

## Agent Friendliness

The architecture is agent-friendly by construction, not by bolting on agent features afterward.

| Property | How achieved |
|---|---|
| Discoverable API | OpenAPI spec auto-generated by Swashbuckle; `GET /openapi.json` gives full surface |
| Structured data | Every operation is a typed HTTP request/response; no screen-scraping required |
| Inspectable state | DuckDB queryable directly via peer extension; agents can write SQL |
| Atomic operations | Each API call validates and completes or returns a structured error |
| Recoverable | `.bak` files before every write; no operation is permanently destructive |
| Scriptable | Any language that speaks HTTP can automate the editor; no custom scripting framework |

The agent workflow: read via SQL against DuckDB, write via HTTP to the C# service. Both paths are available simultaneously and are consistent with each other — a write through the API immediately reflects in DuckDB.
