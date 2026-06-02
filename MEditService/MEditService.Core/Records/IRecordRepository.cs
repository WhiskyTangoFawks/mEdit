using DuckDB.NET.Data;

namespace MEditService.Core.Records;

public interface IRecordRepository : IRecordIndexer, IRecordReader
{
    DuckDBConnection Connection { get; }
}
