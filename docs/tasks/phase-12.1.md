# Phase 12.1 — Flag Enum / Bitmask Support

**Status: Not Started**

*Goal: fields backed by C# `[Flags]` enums (NPC flags, weapon flags, item flags, etc.) render as multi-select checkboxes in edit mode and as comma-separated active flag names in read mode, rather than a useless raw integer.*

---

## Backend

### `SchemaReflector`

- [ ] In the enum-detection branch of `SchemaReflector` (where `ApiType = "enum"` is set), add a check for `typeof([Flags])` attribute on the C# enum type via `enumType.GetCustomAttribute<FlagsAttribute>() != null`
- [ ] Add `bool IsBitmask` to `FieldMetadata` record in `Queries/Models.cs` — `false` by default; `true` for flag enums
- [ ] Propagate `IsBitmask` through `ColumnSpec` and `FieldMetadataMapper` wherever `FieldMetadata` is constructed for enum columns
- [ ] Run `npm run generate-api` — `isBitmask` appears in the generated TypeScript `FieldMetadata` type

---

## Extension / Webview

### `types.ts`

- [ ] Add `isBitmask?: boolean` to the `FieldMetadata` interface (will also appear in generated `api.ts` after regeneration)

### `FlagCell` component (new file: `webview/src/FlagCell.tsx`)

- [ ] Props: `{ value: unknown; meta: FieldMetadata; editMode: boolean; onCommit: (v: number) => void }`
- [ ] Read mode: parse the stored integer value; iterate `meta.enumValues`; display comma-separated names of active flags (bit set). Show `—` if value is null/undefined.
- [ ] Edit mode: render a checkbox per flag name from `meta.enumValues`; each checkbox reflects its bit in the current integer value; toggling a checkbox XORs that bit and calls `onCommit` with the new integer
- [ ] Guard against `meta.enumValues` being empty

### `renderCell` dispatch (`RecordPanel.tsx`)

- [ ] In `renderCell()`, before the existing `enum` path in `ScalarCell`, check `meta.isBitmask === true` → render `<FlagCell>` instead of `<ScalarCell>`

---

## Tests

### Backend

- [ ] `SchemaReflector` emits `IsBitmask = true` for a known FO4 flag enum (e.g. `Npc.NpcFlag` — confirm with `wbDefinitionsFO4.pas` that the NPC flags field uses a `[Flags]` enum in Mutagen)
- [ ] `SchemaReflector` emits `IsBitmask = false` for a regular (non-flags) enum

### Webview (`FlagCell.test.tsx`)

- [ ] Read mode: value `0b0101` with `enumValues: ['A','B','C','D']` renders `"A, C"`
- [ ] Read mode: null value renders `"—"`
- [ ] Edit mode: renders one checkbox per flag; `A` and `C` checked, `B` and `D` unchecked
- [ ] Edit mode: unchecking `A` calls `onCommit(0b0100)` (only bit 2 remains)
- [ ] Edit mode: checking `B` calls `onCommit(0b0111)` (adds bit 1)

---

## Proof

*To be filled in on completion. Paste `dotnet test` output, `npm run test:unit` output, and commit hash here.*
