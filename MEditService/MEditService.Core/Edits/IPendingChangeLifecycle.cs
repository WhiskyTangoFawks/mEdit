using DuckDB.NET.Data;

namespace MEditService.Core.Edits;

internal interface IPendingChangeLifecycle
{
    void OnSessionLoaded(DuckDBConnection connection);
    void OnSessionUnloaded();
}
