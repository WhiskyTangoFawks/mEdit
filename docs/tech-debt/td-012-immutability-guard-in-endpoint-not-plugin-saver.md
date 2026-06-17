# TD-012: Immutability Guard Lives in the HTTP Endpoint, Not in `PluginSaver`

**Severity:** Medium (correctness-bearing)
**Area:** `ChangeEndpoints.SaveGroups` / `PluginSaver.Save` / `ISessionManager`
**Introduced:** Phase 11 (group-based save, replacing TD-002)

## What's happening

Before calling `saver.Save(groupId)`, the `POST /changes/groups/save` endpoint manually reads
every group's changes from DuckDB, extracts distinct plugin names, looks each one up in the session,
and checks `meta.IsImmutable`
([ChangeEndpoints.cs ~L222–236](../../MEditService/MEditService.Api/Endpoints/ChangeEndpoints.cs)):

```csharp
foreach (var groupId in groupIds)
{
    var groupChanges = changes.GetChanges(groupId: groupId);
    foreach (var pluginName in groupChanges.Select(c => c.Plugin).Distinct(...))
    {
        var meta = s.Plugins.FirstOrDefault(p => p.Name.Equals(pluginName, ...));
        if (meta == null) return Results.NotFound();
        if (meta.IsImmutable)
            return Results.Problem($"'{pluginName}' is a base-game plugin ...", statusCode: 409);
    }
}
```

The invariant being enforced — "you may not save changes to an immutable plugin" — is a domain
rule that belongs in `PluginSaver`, not in an HTTP handler. `PluginSaver` already knows `session`
(constructor-injected) and already iterates `byPlugin` inside `ExecuteGroupSaveAsync`'s callback,
so the guard would add one cheap lookup with no new I/O.

## Impact

**Any future call path that reaches `PluginSaver.Save()` bypasses the guard silently.** A batch
job, a migration utility, or a second endpoint that calls `saver.Save(groupId)` directly would
write base-game plugins to disk without any check.

**Double DB read per group.** The pre-validation pass calls `changes.GetChanges(groupId:)` for
every group, then `saver.Save()` calls `changes.ExecuteGroupSaveAsync` which runs
`SelectChangesForGroup` — the same `SELECT … WHERE group_id = ?` again. The data fetched for
validation is thrown away and re-fetched identically inside the saver. Two `SemaphoreSlim`
acquisitions and two full table scans per group where one suffices.

**Unnecessary injection.** `IPendingChangeService changes` is injected into the endpoint purely
for the pre-flight read. `PluginSaver` already holds it. Once the guard moves inside `PluginSaver`,
`changes` can be removed from the endpoint's parameter list entirely.

## Fix Plan

1. Add `SaveGroupResult.ImmutablePlugin(string PluginName)` to the discriminated union in
   `PluginSaver.cs`.
2. In `PluginSaver.Save`, after `byPlugin` is populated inside the `ExecuteGroupSaveAsync`
   callback, iterate `byPlugin.Keys`, look up each plugin in `session.Session!.Plugins`, and
   return `SaveGroupResult.ImmutablePlugin(pluginName)` (throwing from the callback will roll
   back the DB transaction).
3. Map the new case to `Results.Problem(..., 409)` in the endpoint.
4. Remove the pre-validation loop and the `changes` parameter from the endpoint.

The pattern already exists: `StageEditResult.PluginImmutable` is returned by the PATCH endpoint
flow and mapped to 409 — apply the same shape here.

## Decisions to make before implementing

1. Should `ImmutablePlugin` carry the plugin name or a list of all immutable plugins that were
   found? A single name is simpler; a list lets the caller report all violations at once.
2. The check happens inside the `writeAll` callback, which runs inside the DB transaction. An
   immutability hit should roll back the delete-from-pending-changes. Confirm the callback throwing
   triggers the existing `catch { await txn.RollbackAsync(); throw; }` path — it does, but verify
   the transaction is still in a rollbackable state after `DeleteChangesForGroup` ran.

## Related

- [ChangeEndpoints.cs](../../MEditService/MEditService.Api/Endpoints/ChangeEndpoints.cs) — `SaveGroups` endpoint
- [PluginSaver.cs](../../MEditService/MEditService.Core/Edits/PluginSaver.cs) — `Save(Guid)`
- [DuckDbPendingChangeService.cs](../../MEditService/MEditService.Core/Edits/DuckDbPendingChangeService.cs) — `ExecuteGroupSaveAsync`
- [TD-002](td-002-plugin-save-not-atomic.md) — predecessor issue (now resolved); see how it was closed for context
- [TD-003](td-003-result-to-http-mapping-duplicated.md) — sibling: result-to-HTTP mapping duplication
