namespace MEditService.Core.Session;

public record PluginMetadata(
    string Name,
    string Path,
    int LoadOrderIndex,
    bool IsLight,
    bool IsMaster,
    IReadOnlyList<string> Masters,
    int RecordCount,
    bool IsImmutable
);

/// <summary>A plugin that could not be loaded into the session (e.g. an unparseable record);
/// it is skipped so the rest of the load order still loads, and reported here.</summary>
public record PluginLoadFailure(string Name, string Reason);
