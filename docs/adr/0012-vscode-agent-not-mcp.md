# Agent integration uses VS Code Language Model API, not a standalone MCP server

Agent-driven mod editing uses VS Code's built-in chat and Language Model API. The extension registers tools that a VS Code agent (Copilot or similar) can call; those tools invoke `SessionController` methods, which hit the C# backend. Agent edits land in `PendingChangeService` exactly like manual edits — the user reviews and approves or rejects them in the VS Code UI before anything is written to disk.

The key reason to prefer this over MCP: the agent capability is wanted *inside VS Code*, where the user is already working. Building a separate MCP server would duplicate the integration surface that the VS Code extension already provides, and require running and maintaining a third process.

`SessionController` must remain free of VS Code types so its methods can be called directly from VS Code chat tool handlers without pulling in the extension host.

## Considered options

**Standalone MCP server** — exposes the C# backend as an MCP tool surface any agent can call. More portable (any MCP-compatible agent could use it), but requires building and running a third process alongside the backend and extension, and duplicates the command surface the extension already owns. Deferred — revisit if the tool needs to be driven by agents outside VS Code.

**Direct HTTP from agent** — agent calls the C# API directly without going through the extension. Loses the VS Code UI integration (pending change panel, conflict view) that makes the human-review loop work. Rejected.
