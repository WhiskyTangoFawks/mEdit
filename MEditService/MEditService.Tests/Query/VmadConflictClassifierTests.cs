using MEditService.Core.Queries;
using MEditService.Core.Records;

namespace MEditService.Tests.Query;

public sealed class VmadConflictClassifierTests
{
    private static VmadPropertyValue Scalar(string type, object? value) => new(type, "", value);

    private static VmadPropertyValue StructVal(params VmadNamedValue[] members) =>
        new("Struct", "", null, Members: members);

    private static VmadNamedValue Prop(string name, VmadPropertyValue value) => new(name, value);

    private static VmadScriptData Script(string name, string flags, params VmadNamedValue[] props) =>
        new(name, flags, props);

    private static VmadPluginInput Input(string plugin, int loadOrder, params VmadScriptData[] scripts) =>
        new(plugin, loadOrder, new VmadData(scripts));

    [Fact]
    public void Classify_DifferingScalarProperty_MarksWinnerAndLoserConflicted()
    {
        var a = Input("A.esp", 0, Script("S", "Local", Prop("Power", Scalar("Int", 10))));
        var b = Input("B.esp", 1, Script("S", "Local", Prop("Power", Scalar("Int", 20))));
        var c = Input("C.esp", 2, Script("S", "Local", Prop("Power", Scalar("Int", 30))));

        var result = VmadConflictClassifier.Classify([a, b, c]);

        var script = Assert.Single(result.Compare.Scripts);
        Assert.Equal("S", script.Name);
        var power = Assert.Single(script.Properties);
        Assert.Equal("Power", power.Name);
        Assert.Equal("C.esp", power.WinnerPlugin);

        Assert.Equal(ConflictThis.ConflictWins, power.CellStates["C.esp"]);
        Assert.Equal(ConflictThis.ConflictLoses, power.CellStates["B.esp"]);
        Assert.False(power.CellStates.ContainsKey("A.esp")); // master omitted

        Assert.Equal(ConflictAll.Conflict, result.ConflictContribution);
    }

    [Fact]
    public void Classify_ScriptPresentInOnePluginOnly_ReflectsAbsenceAndClassifiesOverride()
    {
        var a = Input("A.esp", 0, Script("Keep", "Local"));
        var b = Input("B.esp", 1, Script("Keep", "Local"), Script("Added", "Local"));

        var result = VmadConflictClassifier.Classify([a, b]);

        var added = result.Compare.Scripts.First(s => s.Name == "Added");
        Assert.Null(added.Flags["A.esp"]);          // absent column reflected
        Assert.Equal("Local", added.Flags["B.esp"]);
        Assert.Equal("B.esp", added.WinnerPlugin);
        Assert.Equal(ConflictThis.Override, added.CellStates["B.esp"]);
        Assert.False(added.CellStates.ContainsKey("A.esp"));
    }

    [Fact]
    public void Classify_SamePropertyDifferentType_IsConflict()
    {
        var a = Input("A.esp", 0, Script("S", "Local", Prop("P", Scalar("Int", 5))));
        var b = Input("B.esp", 1, Script("S", "Local", Prop("P", Scalar("Int", 5))));
        var c = Input("C.esp", 2, Script("S", "Local", Prop("P", Scalar("String", "5"))));

        var result = VmadConflictClassifier.Classify([a, b, c]);

        var p = result.Compare.Scripts[0].Properties.First(x => x.Name == "P");
        Assert.Equal("Int", p.Types["A.esp"]);
        Assert.Equal("String", p.Types["C.esp"]);
        Assert.Equal(ConflictThis.ConflictWins, p.CellStates["C.esp"]);
        Assert.Equal(ConflictAll.Conflict, result.ConflictContribution);
    }

    [Fact]
    public void Classify_StructMemberDiffers_ConflictsAtMemberLevel()
    {
        var a = Input("A.esp", 0, Script("S", "Local",
            Prop("Config", StructVal(Prop("Factor", Scalar("Float", 1f)), Prop("Scale", Scalar("Float", 2f))))));
        var b = Input("B.esp", 1, Script("S", "Local",
            Prop("Config", StructVal(Prop("Factor", Scalar("Float", 1f)), Prop("Scale", Scalar("Float", 9f))))));

        var result = VmadConflictClassifier.Classify([a, b]);

        var config = result.Compare.Scripts[0].Properties.First(p => p.Name == "Config");
        Assert.Equal("struct", config.Kind);
        Assert.Equal(ConflictThis.Override, config.CellStates["B.esp"]);     // parent reflects subtree diff
        Assert.NotNull(config.Children);

        var scale = config.Children!.First(c => c.Name == "Scale");
        var factor = config.Children!.First(c => c.Name == "Factor");
        Assert.Equal(ConflictThis.Override, scale.CellStates["B.esp"]);          // differing member
        Assert.Equal(ConflictThis.IdenticalToMaster, factor.CellStates["B.esp"]); // benign sibling
    }

    [Fact]
    public void Classify_ReorderedButEqualScriptsAndProperties_NotFlagged()
    {
        var a = Input("A.esp", 0,
            Script("Alpha", "Local", Prop("P1", Scalar("Int", 1)), Prop("P2", Scalar("Int", 2))),
            Script("Beta", "Local"));
        // Same content, scripts and properties stored in reversed order.
        var b = Input("B.esp", 1,
            Script("Beta", "Local"),
            Script("Alpha", "Local", Prop("P2", Scalar("Int", 2)), Prop("P1", Scalar("Int", 1))));

        var result = VmadConflictClassifier.Classify([a, b]);

        Assert.Equal(ConflictAll.NoConflict, result.ConflictContribution);
        Assert.All(result.Compare.Scripts, s =>
        {
            Assert.True(s.CellStates.Values.All(c => c == ConflictThis.IdenticalToMaster));
            Assert.All(s.Properties, p =>
                Assert.True(p.CellStates.Values.All(c => c == ConflictThis.IdenticalToMaster)));
        });
    }
}
