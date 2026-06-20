using System.Text.Json;
using MEditService.Core.Queries;
using Microsoft.Extensions.Logging;

namespace MEditService.Tests.Query;

public class ArrayChildDiffTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoMasters =
        new Dictionary<string, IReadOnlyList<string>>();

    private static ClassifyResult Classify(
        IReadOnlyList<RecordDetail> records,
        IConflictClassifier? classifier = null) =>
        (classifier ?? new ConflictClassifier()).Classify(records, NoMasters);

    private static FieldMetadata SortedArrayMeta(string name) =>
        new(name, "array", true, [], [],
            ElementType: new FieldMetadata("", "formKey", false, [], [], IsSortable: true));

    private static FieldMetadata UnsortedArrayMeta(string name) =>
        new(name, "array", true, [], [],
            ElementType: new FieldMetadata("", "string", false, [], []));

    private static FieldMetadata StructArrayMeta(string name, params FieldMetadata[] subFields) =>
        new(name, "array", true, [], [],
            ElementType: new FieldMetadata("", "struct", false, [], [],
                Fields: subFields.ToList()));

    private static RecordDetail MakeRecord(string plugin, int loadOrder, bool isWinner,
        FieldMetadata meta, object? value) =>
        new("000001:Test.esp", plugin, loadOrder, isWinner, null,
            [new FieldValue(meta, value)]);

    // ── Sorted array tests ───────────────────────────────────────────────────

    [Fact]
    public void SortedArray_TwoPluginsOverlap_ProducesUnionChildren()
    {
        var meta = SortedArrayMeta("Keywords");
        var arrayA = JsonSerializer.Deserialize<JsonElement>("[\"KwdA\",\"KwdB\"]");
        var arrayB = JsonSerializer.Deserialize<JsonElement>("[\"KwdA\",\"KwdC\"]");

        var master = MakeRecord("A.esp", 0, false, meta, arrayA);
        var override1 = MakeRecord("B.esp", 1, true, meta, arrayB);

        var result = Classify([master, override1]);

        var kwdDiff = result.Diffs.First(d => d.FieldName == "Keywords");
        Assert.NotNull(kwdDiff.Children);
        Assert.Equal(3, kwdDiff.Children!.Count);

        var kwdA = kwdDiff.Children.First(c => c.FieldName == "KwdA");
        Assert.NotNull(kwdA.Values["A.esp"]);
        Assert.NotNull(kwdA.Values["B.esp"]);

        var kwdB = kwdDiff.Children.First(c => c.FieldName == "KwdB");
        Assert.NotNull(kwdB.Values["A.esp"]);
        Assert.Null(kwdB.Values["B.esp"]);
        Assert.False(kwdB.CellStates.ContainsKey("B.esp")); // absent → omitted

        var kwdC = kwdDiff.Children.First(c => c.FieldName == "KwdC");
        Assert.Null(kwdC.Values["A.esp"]);
        Assert.NotNull(kwdC.Values["B.esp"]);
    }

    [Fact]
    public void SortedArray_UnionOrderIsFirstSeenAcrossPluginsInLoadOrder()
    {
        // A=[KwdB,KwdA], B=[KwdC,KwdA] → first-seen order: KwdB, KwdA, KwdC
        // (kills "sort lexicographically" mutant — alphabetical order would be KwdA,KwdB,KwdC)
        var meta = SortedArrayMeta("Keywords");
        var arrayA = JsonSerializer.Deserialize<JsonElement>("[\"KwdB\",\"KwdA\"]");
        var arrayB = JsonSerializer.Deserialize<JsonElement>("[\"KwdC\",\"KwdA\"]");

        var master = MakeRecord("A.esp", 0, false, meta, arrayA);
        var override1 = MakeRecord("B.esp", 1, true, meta, arrayB);

        var result = Classify([master, override1]);

        var children = result.Diffs.First(d => d.FieldName == "Keywords").Children!;
        Assert.Equal(["KwdB", "KwdA", "KwdC"], children.Select(c => c.FieldName).ToList());
    }

    // ── Unsorted array tests ─────────────────────────────────────────────────

    [Fact]
    public void UnsortedArray_RaggedLengths_ChildCountIsMax_ShortPluginGetsNullForMissingIndex()
    {
        var meta = UnsortedArrayMeta("Items");
        var arrayA = JsonSerializer.Deserialize<JsonElement>("[\"x\",\"y\",\"z\"]"); // 3 elements
        var arrayB = JsonSerializer.Deserialize<JsonElement>("[\"x\",\"y\"]");      // 2 elements

        var master = MakeRecord("A.esp", 0, false, meta, arrayA);
        var override1 = MakeRecord("B.esp", 1, true, meta, arrayB);

        var result = Classify([master, override1]);

        var children = result.Diffs.First(d => d.FieldName == "Items").Children!;
        Assert.Equal(3, children.Count);
        Assert.Equal("[0]", children[0].FieldName);
        Assert.Equal("[1]", children[1].FieldName);
        Assert.Equal("[2]", children[2].FieldName);

        // B.esp's third element is absent
        Assert.Null(children[2].Values["B.esp"]);
        Assert.False(children[2].CellStates.ContainsKey("B.esp"));
    }

    // ── Overflow test ────────────────────────────────────────────────────────

    [Fact]
    public void Array_ExceedingMaxArrayChildCount_ReturnsNullChildren_LogsWarning()
    {
        var logLines = new List<string>();
        using var loggerFactory = LoggerFactory.Create(b =>
            b.AddProvider(new CollectingLoggerProvider(logLines)));
        var classifier = new ConflictClassifier(
            loggerFactory.CreateLogger<ConflictClassifier>());

        var meta = UnsortedArrayMeta("Items");
        // 501 elements — one over the limit
        var bigArray = JsonSerializer.Deserialize<JsonElement>(
            "[" + string.Join(",", Enumerable.Range(0, 501).Select(i => $"\"{i}\"")) + "]");

        var master = MakeRecord("A.esp", 0, false, meta, bigArray);
        var override1 = MakeRecord("B.esp", 1, true, meta, bigArray);

        var result = Classify([master, override1], classifier);

        var kwdDiff = result.Diffs.First(d => d.FieldName == "Items");
        Assert.Null(kwdDiff.Children);
        Assert.Contains(logLines, l => l.Contains("MaxArrayChildCount"));
    }

    // ── Struct-typed element test ─────────────────────────────────────────────

    [Fact]
    public void StructTypedArrayElement_ProducesSubFieldChildrenForEachArrayChild()
    {
        var meta = StructArrayMeta("Ranks",
            new FieldMetadata("rank", "int", false, [], []));

        var arrayA = JsonSerializer.Deserialize<JsonElement>("[{\"rank\":1},{\"rank\":2}]");
        var arrayB = JsonSerializer.Deserialize<JsonElement>("[{\"rank\":1},{\"rank\":9}]");

        var master = MakeRecord("A.esp", 0, false, meta, arrayA);
        var override1 = MakeRecord("B.esp", 1, true, meta, arrayB);

        var result = Classify([master, override1]);

        var ranksDiff = result.Diffs.First(d => d.FieldName == "Ranks");
        Assert.NotNull(ranksDiff.Children);
        Assert.Equal(2, ranksDiff.Children!.Count);

        // Each array child should have a "rank" sub-field child
        foreach (var child in ranksDiff.Children)
        {
            Assert.NotNull(child.Children);
            Assert.Contains(child.Children!, c => c.FieldName == "rank");
        }

        // Second element differs — should have conflict state on B.esp
        var secondChild = ranksDiff.Children[1];
        var rankSubField = secondChild.Children!.First(c => c.FieldName == "rank");
        Assert.True(rankSubField.CellStates.ContainsKey("B.esp"));
        Assert.Equal(ConflictThis.Override, rankSubField.CellStates["B.esp"]);
    }
}

// Minimal logger provider for the overflow test
file sealed class CollectingLoggerProvider(List<string> lines) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CollectingLogger(lines);
    public void Dispose() { }
}

file sealed class CollectingLogger(List<string> lines) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => lines.Add(formatter(state, exception));
}
