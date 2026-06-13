# Phase 13 — VMAD (Papyrus Script Data)

**Status: Not Started**

*Goal: Papyrus script attachments (VMAD subrecord) appear in the compare grid as first-class fields — scripts sorted by name, properties within each script sorted by name — read-only in this phase, with editing deferred.*

---

## Background

VMAD (Virtual Machine Adapter) is the Papyrus scripting subrecord present on NPC_, QUST, PERK, PACK, SCEN, INFO, and others. Its structure:

```
VMAD
  Version          (int16, cpIgnore)
  Object Format    (int16, cpIgnore)
  Scripts[]        sorted array, sort key = ScriptName
    ScriptName     (string)
    Flags          (enum: Local / Inherited / Removed / Inherited and Removed)
    Properties[]   sorted array, sort key = propertyName
      propertyName (string)
      Type         (enum: 13 variants)
      Flags        (enum)
      Value        (union — dispatched on Type)
```

For PERK, PACK, QUST, SCEN, INFO: a `ScriptFragments` section follows Scripts (not in scope for this phase).

**Why VMAD is not in the generic reflection pipeline**: the property `Value` is a polymorphic union (13 types: Object, String, Int32, Float, Bool, Variable, Struct, and array variants). Mutagen represents this via custom binary translation in `AVirtualMachineAdapter` — it does not surface as standard getter interface properties that `SchemaReflector` can walk. Trying to force VMAD through the generic pipeline would require teaching `SchemaReflector` about union types, which is a larger scope than the VMAD feature itself.

**Architecture reference**: `SFRecordCompareEngine/` (sibling project at repo root) solved this identically — dedicated tables per VMAD entity with a typed flat schema, separate import + hydration services. See:
- `SFRecordCompareEngine.Core/Services/ScriptingAdapterImportService.cs`
- `SFRecordCompareEngine.Core/Services/ScriptingAdapterHydrationService.cs`
- `SFRecordCompareEngine.Core/DTOs/Records/ScriptingAdapterPropertyDTO.cs`

---

## Backend

### DuckDB Tables

Add three tables to `TableDdlBuilder.CreateTables()`:

```sql
CREATE TABLE IF NOT EXISTS vmad_scripts (
    form_key     VARCHAR NOT NULL,
    plugin       VARCHAR NOT NULL,
    script_name  VARCHAR NOT NULL,
    script_index INTEGER NOT NULL,
    record_type  VARCHAR NOT NULL
);

CREATE TABLE IF NOT EXISTS vmad_properties (
    form_key       VARCHAR NOT NULL,
    plugin         VARCHAR NOT NULL,
    script_name    VARCHAR NOT NULL,
    property_name  VARCHAR NOT NULL,
    property_index INTEGER NOT NULL,
    record_type    VARCHAR NOT NULL,
    type           VARCHAR NOT NULL,   -- Mutagen type name e.g. "Bool", "Int", "Float", "String", "Object", "ArrayOfString"
    bool_value     BOOLEAN,
    int_value      INTEGER,
    float_value    FLOAT,
    string_value   VARCHAR,
    form_key_value VARCHAR,
    alias_value    SMALLINT
);

CREATE TABLE IF NOT EXISTS vmad_property_list_items (
    form_key        VARCHAR NOT NULL,
    plugin          VARCHAR NOT NULL,
    script_name     VARCHAR NOT NULL,
    property_name   VARCHAR NOT NULL,
    property_index  INTEGER NOT NULL,
    list_item_index INTEGER NOT NULL,
    record_type     VARCHAR NOT NULL,
    type            VARCHAR NOT NULL,
    bool_value      BOOLEAN,
    int_value       INTEGER,
    float_value     FLOAT,
    string_value    VARCHAR,
    form_key_value  VARCHAR
);
```

Add indexes on `(form_key, plugin)` for all three tables.

### Indexing — `DuckDbRecordRepository`

- [ ] At the top of `Index()`, before the per-type loop, delete existing VMAD rows for the plugin being indexed:
  ```csharp
  DeleteVmadForPlugin(plugin);
  ```
- [ ] After indexing all record types, walk all major records that implement `IHasVirtualMachineAdapterGetter` (check via `Supports()`) and populate the three tables using a DuckDB appender per table. For each script entry: extract `ScriptName`, `Flags`, and `Properties`. For each property: extract `Name`, `Type` (via the C# type of the value — use `switch (property.Data)` pattern from `AVirtualMachineAdapter.cs`), and populate the appropriate nullable column. For list-typed properties (arrays), insert one row per item into `vmad_property_list_items`.

### Compare / Query

- [ ] Add `VmadScript` DTO to `Queries/Models.cs`:
  ```csharp
  record VmadProperty(string Name, string Type, object? Value, IReadOnlyList<object?>? ListItems = null);
  record VmadScript(string Name, string Flags, IReadOnlyList<VmadProperty> Properties);
  record VmadData(IReadOnlyList<VmadScript> Scripts);
  ```
- [ ] Add `GetVmad(string formKey, string plugin)` to `IRecordReader` / `DuckDbRecordRepository` — queries the three tables and assembles `VmadData`.
- [ ] In `RecordQueryService.GetCompareResult()` (or the compare endpoint), include VMAD alongside normal diffs: fetch `VmadData` for each plugin that has VMAD, and return it as a separate `vmadByPlugin: Record<string, VmadData>` field in the compare response. Do not attempt to fold VMAD into the generic `diffs` array.

### API

- [ ] Update `CompareResult` model and its OpenAPI shape to include `vmadByPlugin`
- [ ] Run `npm run generate-api` — update generated TypeScript client

---

## Extension / Webview

VMAD appears in the compare grid as a dedicated section below the normal field rows, following the unified tree model (ADR-0019):

- [ ] **Scripts** — sorted array section, sort key = ScriptName. One row per unique ScriptName across all plugins. Plugin column is empty if that plugin has no script with that name.
- [ ] **Properties** — each script row expands into its property sub-rows. Properties are a sorted array, sort key = propertyName. `Value` displayed as a readable string (FormKey as link for Object type; list types show `[N items]`).
- [ ] Read-only in this phase — no edit inputs for VMAD fields. Edit mode shows no edit affordances in the VMAD section.
- [ ] Pending column for VMAD: always empty in this phase (no VMAD pending changes).
- [ ] Empty state: if no plugin has VMAD for this record, the VMAD section is omitted entirely.

---

## Tests

- [ ] Backend: index an NPC_ record with a known script; assert `vmad_scripts` row exists with correct `script_name`
- [ ] Backend: index same plugin twice; assert `vmad_scripts` row count for that plugin does not double (delete-before-insert pattern)
- [ ] Backend: `GetVmad()` returns correct property values for a script with Bool and Object properties
- [ ] Backend: compare response includes `vmadByPlugin` with correct scripts for the winning override plugin
- [ ] Webview: VMAD section renders script name rows; expanding shows property sub-rows
- [ ] Webview: VMAD section absent when record has no VirtualMachineAdapter

---

## Out of Scope (Future)

- **ScriptFragments** (PERK, PACK, QUST, SCEN, INFO variant VMAD sections)
- **Editing** VMAD fields — requires defining change semantics for the polymorphic property union
- **VMAD form references** — tracking Object-type property FormKeys in `form_references` table

---

## Proof

*To be filled in on completion. Paste `dotnet test` output, `npm run test:unit` output, and commit hash here.*
