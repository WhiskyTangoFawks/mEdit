# Prefer scripts over direct API calls for complex agent tasks

For multi-record or complex automated edits, the preferred agent output is a Phase-15-format script (SQL selection + Python body) rather than a sequence of PATCH requests. Scripts are readable, rerunnable, and deterministic — the user gets two review gates: the script itself, then the pending changes it produces.

Agents may still call the API directly for simple, single-record operations where generating a script would be heavier than the task warrants. The distinction is: use scripts when the agent's intent is hard to verify from a list of field diffs alone.

## Consequences

The scripting engine (Phase 15) is a meaningful prerequisite for the agentic workflow — not just a power-user feature — because it provides the inspection surface for complex agent tasks.
