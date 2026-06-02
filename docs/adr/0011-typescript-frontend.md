# TypeScript for the VS Code extension and webviews

VS Code extensions are TypeScript — this is the VS Code extension model, not a choice. The webview panels (React) are also TypeScript. The frontend consumes the C# service's OpenAPI spec to generate a fully typed API client at build time via `openapi-fetch`, eliminating manual type maintenance.
