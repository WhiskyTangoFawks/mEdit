# TD-013: `UpdateWinners` Called Once Per Plugin Instead of Once Per Group Save

**Severity:** Low (efficiency)
**Area:** `PluginSaver.Save` / `SessionManager.ReindexPlugin` / `ISessionManager`
**Introduced:** Phase 11 (group-based save)

## What's happening

After a group save, `PluginSaver.Save` reindexes each committed plugin by calling
`session.ReindexPlugin(plugin)` in a sequential loop
([PluginSaver.cs:40–43](../../MEditService/MEditService.Core/Edits/PluginSaver.cs)):

```csharp
if (result is SaveGroupResult.Saved saved)
{
    foreach (var plugin in saved.ByPlugin.Keys)
        await session.ReindexPlugin(plugin);   // Index + UpdateWinners, per plugin
}
```

`SessionManager.ReindexPlugin` calls `repository.Index(mod, metadata.LoadOrderIndex)` then
`repository.UpdateWinners()` for every plugin
([SessionManager.cs](../../MEditService/MEditService.Core/Session/SessionManager.cs)).
`UpdateWinners` rescans the full record table to recompute the winning override for every FormKey.
For a group that touches K plugins, it runs K times when once — after all plugins are re-indexed —
would produce the same final state.

## Impact

For the common single-plugin group (K=1), there is no redundancy. For groups that span multiple
plugins (e.g., a batch create that populates records in a patch and its master), `UpdateWinners`
runs K times. Each extra call re-scans the entire DuckDB records table unnecessarily. At typical
load-order sizes this is measurable but not catastrophic; at large modlists it adds latency to
every multi-plugin save.

## Fix Plan

Split the current `ReindexPlugin` into two responsibilities on `ISessionManager`:

```csharp
/// Re-reads the plugin from disk and calls repository.Index. Does NOT call UpdateWinners.
Task ReindexPlugin(string plugin);

/// Recomputes winners across all indexed plugins. Call once after all ReindexPlugin calls complete.
void UpdateWinners();
```

`PluginSaver.Save` becomes:

```csharp
if (result is SaveGroupResult.Saved saved)
{
    foreach (var plugin in saved.ByPlugin.Keys)
        await session.ReindexPlugin(plugin);
    session.UpdateWinners();
}
```

Alternatively, add a batch overload `Task ReindexPlugins(IReadOnlyList<string> plugins)` that
loops `Index` calls and then calls `UpdateWinners` once — the caller stays a single `await`.

## Decisions to make before implementing

1. **Interface shape.** Separate `UpdateWinners()` vs. batch `ReindexPlugins(IReadOnlyList<string>)`
   — the separate method is more composable; the batch overload is a smaller surface change. Check
   whether any other caller of `ReindexPlugin` expects `UpdateWinners` to have been called on
   return (currently `SavePlugin` calls `await ReindexPlugin` and then returns — it expects winners
   to be fresh).
2. **Stub impact.** `ISessionManager` has four in-test stubs (`DeleteRecordsTests`,
   `EditOrchestratorTests`, `RecordQueryServiceTests`, and any new ones). Adding a member requires
   updating all stubs.

## Also: Stale Index Window

The same split unlocks a correctness fix. Currently `PluginSaver.Save` calls `ReindexPlugin` after
`ExecuteGroupSaveAsync` has already released the semaphore. Between semaphore release and reindex
completion, concurrent reads see a contradictory state: pending changes gone but record index still
reflects pre-save data. If `ReindexPlugin` throws (e.g., Mutagen parse failure on the new file),
the index is permanently stale for the session.

Once `ReindexPlugin` is split into `IndexPlugin` + `UpdateWinners`, move both calls **inside** the
`writeAll` callback (after all `Commit()` calls, before the callback returns). The semaphore is
still held at that point, so the window closes.

## Related

- [PluginSaver.cs](../../MEditService/MEditService.Core/Edits/PluginSaver.cs) — `Save(Guid)`, the call site
- [SessionManager.cs](../../MEditService/MEditService.Core/Session/SessionManager.cs) — `ReindexPlugin`
- [ISessionManager.cs](../../MEditService/MEditService.Core/Session/ISessionManager.cs) — interface to extend
