# Validate

After any task. Stop at first failure and fix before continuing.

## Scope

```bash
git diff --name-only HEAD && git diff --name-only --cached
```

Backend = `MEditService/**/*.cs` | Frontend = `medit-vscode/**/*.ts|tsx`, `package.json` | API = `src/generated/api.ts`

## Gates

| Gate | Run when | How |
|------|----------|-----|
| 1 — Simplify | Always | `/simplify` |
| 2 — Backend lint | Backend | `/lint-backend` |
| 3 — Backend tests | Backend | `cd MEditService && dotnet test -v minimal` |
| 4 — Frontend lint | Frontend | `cd medit-vscode && npm run lint` |
| 5 — Frontend tests | Frontend or api.ts | `cd medit-vscode && npm run test:unit && npm run test:integration` |
| 6 — Mutation tests | Core `*.cs`, after Gate 3 | `cd MEditService && bash ../.claude/skills/mutation-test/run.sh` |
| 7 — Code review | Always | `/code-review` |

Config/docs only → skip gates 2–6.
Gate 6 survivors → triage per `/mutation-test`.
Can't pass a gate → document why; don't skip silently.

## Static analysis notes

- C# analyzer violations (Roslynator, SonarAnalyzer) surface during `dotnet build` via `EnforceCodeStyleInBuild=true`; Gate 3 catches them.
- Gate 4 (`npm run lint`) is the exhaustive ESLint check; the VS Code ESLint extension only runs on open files.
