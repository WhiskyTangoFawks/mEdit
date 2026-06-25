using MEditService.Core.Queries;

namespace MEditService.Core.Records;

public interface IRecordReader
{
    PagedResult<RecordSummary> GetRecords(string tableName, string? plugin, string? search, int limit, int offset);
    RecordDetail? GetRecord(string tableName, string formKey, string? plugin, bool winnerOnly);
    IReadOnlyList<RecordDetail> GetAllOverrides(string tableName, string formKey);
    VmadData? GetVmad(string formKey, string plugin);
    int CountRecordsForPlugin(string tableName, string plugin);
    string? FindRecordType(string formKey);
    PagedResult<RecordSummary> SearchRecords(IReadOnlyList<string> tableNames, string? plugin, string? search, int limit, int offset);
    IReadOnlySet<string> GetPluginsWithMatchingRecords(IEnumerable<string> tableNames);
    IReadOnlyList<ReferenceResult> GetReferences(string targetFormKey);

    // Phase 16 — worldspace tree reads (from the placement / cell_location side tables).
    // Returns every cell under the worldspace; a TopCell has null Block/Sub coordinates.
    IReadOnlyList<CellLocationSummary> GetWorldspaceCells(string plugin, string worldspaceFormKey);
    PagedResult<CellSummary> GetInteriorCells(string plugin, int limit, int offset);
    CellReferences GetCellReferences(string plugin, string cellFormKey);
}
