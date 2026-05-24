# mEdit

A VS Code extension + local C# service for viewing, editing, and comparing Bethesda plugin files (`.esp`/`.esm`/`.esl`).

## Stack

**Backend** — C# ASP.NET Core minimal API (`BethesdaPluginService/`)
- Mutagen: plugin parsing/writing
- DuckDB: in-process record index (cache, not source of truth)
- Swashbuckle: OpenAPI spec auto-generation

**Frontend** — TypeScript VS Code extension + React webviews (`bethesda-plugin-editor/`)
- API client generated from OpenAPI spec at build time

## Conventions

### Test-Driven Development

New functionality starts with a failing test. Write the test first — enough production code to compile and fail, then implement to make it pass.

Order: red → green → refactor. Do not write implementation before a test exists for it.
