# mEdit

A VS Code extension + local C# service for viewing, editing, and comparing Bethesda plugin files (`.esp`/`.esm`/`.esl`). Targets Fallout 4 in v1.

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.x | Backend service |
| [Node.js](https://nodejs.org/) | 20 LTS or later | VS Code extension build |
| [VS Code](https://code.visualstudio.com/) | Latest | Required for the extension |

On Ubuntu/Debian:
```bash
sudo apt-get install -y dotnet-sdk-9.0 nodejs npm
```

## Repository layout

```
BethesdaPluginService/              C# backend (ASP.NET Core minimal API)
  BethesdaPluginService.sln         Solution file
  BethesdaPluginService.Core/       Domain logic, services, DuckDB index
  BethesdaPluginService.Api/        HTTP host, route handlers, Swashbuckle
bethesda-plugin-editor/             VS Code extension + React webviews
  src/                              Extension host (TypeScript / VS Code API)
  webview/                          React UI (Vite build, outputs to out/webview)
```

## Backend setup

```bash
dotnet restore
dotnet build BethesdaPluginService/BethesdaPluginService.sln
dotnet run --project BethesdaPluginService/BethesdaPluginService.Api
```

Service runs at `http://localhost:5172`.
- Health check: `GET /health`
- OpenAPI spec: `GET /openapi.json`
- Swagger UI: `/swagger`

## VS Code extension setup

```bash
cd bethesda-plugin-editor
npm install
npm run build          # compile extension + webview
npm run generate-api   # regenerate typed client from OpenAPI spec (backend must be running)
```

Press **F5** in VS Code to launch the Extension Development Host.

## Development

See [mEdit.md](mEdit.md) for architecture decisions and [TASKS.md](TASKS.md) for the phased build plan.

Tests follow TDD — write a failing test before any implementation. Run the test suite:

```bash
dotnet test BethesdaPluginService.sln
```
