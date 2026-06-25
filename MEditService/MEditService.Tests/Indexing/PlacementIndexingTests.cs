using System.Globalization;
using DuckDB.NET.Data;
using MEditService.Core.Records;
using MEditService.Core.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Noggog;

namespace MEditService.Tests.Indexing;

// Phase 16: the structural indexing pass must populate the `placement` and `cell_location`
// side tables that back the per-plugin worldspace tree.
public class PlacementIndexingTests
{
    private static readonly ISchemaReflector _reflector = new SchemaReflector();
    private static readonly ITableDdlBuilder _ddl = new TableDdlBuilder(_reflector);

    private sealed record Built(
        DuckDbRecordRepository Repo,
        string WorldspaceFk,
        string ExtCellFk,
        string IntCellFk,
        string BarrelFk,
        string RaiderFk) : IDisposable
    {
        public void Dispose() => Repo.Dispose();
    }

    private static float ToF(object? v) => Convert.ToSingle(v, CultureInfo.InvariantCulture);
    private static int ToI(object? v) => Convert.ToInt32(v, CultureInfo.InvariantCulture);
    private static bool ToB(object? v) => Convert.ToBoolean(v, CultureInfo.InvariantCulture);

    private static Built IndexFixture()
    {
        var mod = new Fallout4Mod(ModKey.FromFileName("TestWorld.esp"), Fallout4Release.Fallout4);

        var wrld = mod.Worldspaces.AddNew("CommonwealthTest");

        var extCell = new Cell(mod) { EditorID = "ExtCell", Grid = new CellGrid { Point = new P2Int(12, -5) } };
        var barrel = new PlacedObject(mod) { EditorID = "barrelRef", Position = new P3Float(10f, 20f, 30f) };
        var raider = new PlacedObject(mod) { EditorID = "raiderRef" };
        extCell.Persistent.Add(barrel);
        extCell.Temporary.Add(raider);

        var subBlock = new WorldspaceSubBlock { BlockNumberX = 0, BlockNumberY = 0 };
        subBlock.Items.Add(extCell);
        var block = new WorldspaceBlock { BlockNumberX = 0, BlockNumberY = 0 };
        block.Items.Add(subBlock);
        wrld.SubCells.Add(block);

        var intCell = new Cell(mod) { EditorID = "IntCell", Grid = new CellGrid { Point = new P2Int(0, 0) } };
        var intSub = new CellSubBlock { BlockNumber = 0 };
        intSub.Cells.Add(intCell);
        var intBlock = new CellBlock { BlockNumber = 0 };
        intBlock.SubBlocks.Add(intSub);
        mod.Cells.Records.Add(intBlock);

        var repo = new DuckDbRecordRepository(_reflector, _ddl, NullLogger.Instance);
        repo.Initialize(GameRelease.Fallout4);
        repo.Index((IModGetter)mod, 0);
        repo.UpdateWinners();

        return new Built(repo, wrld.FormKey.ToString(), extCell.FormKey.ToString(),
            intCell.FormKey.ToString(), barrel.FormKey.ToString(), raider.FormKey.ToString());
    }

    private static List<Dictionary<string, object?>> Query(DuckDbRecordRepository repo, string sql, string param)
    {
        using var cmd = repo.Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new DuckDBParameter { Value = param });
        using var reader = cmd.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    [Fact]
    public void Index_PersistentPlacedObject_WritesPlacementRow()
    {
        using var b = IndexFixture();
        var rows = Query(b.Repo,
            "SELECT parent_cell, placement_group, pos_x, pos_y, pos_z FROM placement WHERE form_key = $1", b.BarrelFk);
        var row = Assert.Single(rows);
        Assert.Equal(b.ExtCellFk, row["parent_cell"]);
        Assert.Equal("persistent", row["placement_group"]);
        Assert.Equal(10f, ToF(row["pos_x"]));
        Assert.Equal(20f, ToF(row["pos_y"]));
        Assert.Equal(30f, ToF(row["pos_z"]));
    }

    [Fact]
    public void Index_TemporaryPlacedObject_WritesTemporaryPlacementRow()
    {
        using var b = IndexFixture();
        var rows = Query(b.Repo,
            "SELECT parent_cell, placement_group FROM placement WHERE form_key = $1", b.RaiderFk);
        var row = Assert.Single(rows);
        Assert.Equal(b.ExtCellFk, row["parent_cell"]);
        Assert.Equal("temporary", row["placement_group"]);
    }

    [Fact]
    public void Index_ExteriorCell_WritesCellLocationWithWorldspaceBlockAndGrid()
    {
        using var b = IndexFixture();
        var rows = Query(b.Repo,
            "SELECT parent_worldspace, block_x, block_y, sub_x, sub_y, grid_x, grid_y, is_interior FROM cell_location WHERE cell_form_key = $1",
            b.ExtCellFk);
        var row = Assert.Single(rows);
        Assert.Equal(b.WorldspaceFk, row["parent_worldspace"]);
        Assert.Equal(0, ToI(row["block_x"]));
        Assert.Equal(0, ToI(row["block_y"]));
        Assert.Equal(12, ToI(row["grid_x"]));
        Assert.Equal(-5, ToI(row["grid_y"]));
        Assert.False(ToB(row["is_interior"]));
    }

    [Fact]
    public void Index_InteriorCell_WritesCellLocationWithNullWorldspaceAndInteriorFlag()
    {
        using var b = IndexFixture();
        var rows = Query(b.Repo,
            "SELECT parent_worldspace, is_interior FROM cell_location WHERE cell_form_key = $1", b.IntCellFk);
        var row = Assert.Single(rows);
        Assert.Null(row["parent_worldspace"]);
        Assert.True(ToB(row["is_interior"]));
    }

    [Fact]
    public void Index_PlacedObjects_AreAlsoIndexedAsRefrRecords()
    {
        using var b = IndexFixture();
        // refr is now a normal record table; the placed objects appear there too.
        var result = b.Repo.GetRecords("refr", "TestWorld.esp", null, 100, 0);
        Assert.Equal(2, result.Total);
    }

    // ── repository read methods (back the worldspace tree) ─────────────────────

    [Fact]
    public void GetCellReferences_SplitsPersistentAndTemporary()
    {
        using var b = IndexFixture();
        var refs = b.Repo.GetCellReferences("TestWorld.esp", b.ExtCellFk);
        Assert.Equal("barrelRef", Assert.Single(refs.Persistent).EditorId);
        Assert.Equal("raiderRef", Assert.Single(refs.Temporary).EditorId);
        Assert.Equal("refr", refs.Persistent[0].RecordType);
    }

    [Fact]
    public void GetWorldspaceCells_ReturnsExteriorCellWithBlockAndGrid()
    {
        using var b = IndexFixture();
        var cells = b.Repo.GetWorldspaceCells("TestWorld.esp", b.WorldspaceFk);
        var cell = Assert.Single(cells);
        Assert.Equal(b.ExtCellFk, cell.FormKey);
        Assert.Equal(0, cell.BlockX);
        Assert.Equal(12, cell.CellX);
        Assert.Equal(-5, cell.CellY);
    }

    [Fact]
    public void GetInteriorCells_ReturnsOnlyInteriorCells()
    {
        using var b = IndexFixture();
        var page = b.Repo.GetInteriorCells("TestWorld.esp", 50, 0);
        var cell = Assert.Single(page.Items);
        Assert.Equal(b.IntCellFk, cell.FormKey);
        Assert.Equal(1, page.Total);
    }
}
