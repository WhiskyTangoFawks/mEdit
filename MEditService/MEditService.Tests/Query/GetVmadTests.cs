using MEditService.Core.Records;
using MEditService.Core.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;

namespace MEditService.Tests.Query;

public sealed class GetVmadTests : IDisposable
{
    private static readonly ISchemaReflector _reflector = new SchemaReflector();
    private static readonly ITableDdlBuilder _ddl = new TableDdlBuilder(_reflector);

    private readonly FormKey _npcFormKey;
    private readonly FormKey _targetFormKey;
    private readonly FormKey _plainNpcFormKey;
    private readonly PluginFixtureData _fixture;

    public GetVmadTests()
    {
        FormKey npcFk = default, targetFk = default, plainFk = default;
        _fixture = new PluginFixtureBuilder()
            .WithPlugin("VmadQuery.esp", mod =>
            {
                var target = mod.Npcs.AddNew("Target");
                targetFk = target.FormKey;

                var plain = mod.Npcs.AddNew("PlainNpc"); // no VMAD
                plainFk = plain.FormKey;

                var npc = mod.Npcs.AddNew("ScriptedNpc");
                npcFk = npc.FormKey;
                npc.VirtualMachineAdapter = BuildVmad(target.FormKey);
            })
            .Build();
        _npcFormKey = npcFk;
        _targetFormKey = targetFk;
        _plainNpcFormKey = plainFk;
    }

    private static VirtualMachineAdapter BuildVmad(FormKey targetFk)
    {
        var vmad = new VirtualMachineAdapter();
        var script = new ScriptEntry { Name = "DefaultScript", Flags = ScriptEntry.Flag.Local };

        script.Properties.Add(new ScriptBoolProperty { Name = "IsActive", Data = true });

        var objProp = new ScriptObjectProperty { Name = "TargetActor", Alias = -1 };
        objProp.Object.SetTo(targetFk);
        script.Properties.Add(objProp);

        var intList = new ScriptIntListProperty { Name = "Scores" };
        intList.Data.Add(10);
        intList.Data.Add(20);
        intList.Data.Add(30);
        script.Properties.Add(intList);

        var structProp = new ScriptStructProperty { Name = "Config" };
        var member = new ScriptEntry { Name = "SubScript" };
        member.Properties.Add(new ScriptFloatProperty { Name = "Factor", Data = 1.5f });
        structProp.Members.Add(member);
        script.Properties.Add(structProp);

        vmad.Scripts.Add(script);
        return vmad;
    }

    public void Dispose() => _fixture.Dispose();

    private DuckDbRecordRepository LoadedRepository()
    {
        var repo = new DuckDbRecordRepository(_reflector, _ddl, NullLogger.Instance);
        repo.Initialize(GameRelease.Fallout4);
        var modPath = new ModPath(
            ModKey.FromFileName("VmadQuery.esp"),
            Path.Combine(_fixture.DataFolder, "VmadQuery.esp"));
        var mod = (IModGetter)Fallout4Mod.CreateFromBinaryOverlay(modPath, Fallout4Release.Fallout4);
        repo.Index(mod, 0);
        repo.UpdateWinners();
        return repo;
    }

    [Fact]
    public void GetVmad_ReturnsScript_WithBoolProperty()
    {
        using var repo = LoadedRepository();

        var vmad = repo.GetVmad(_npcFormKey.ToString(), "VmadQuery.esp");

        Assert.NotNull(vmad);
        var script = Assert.Single(vmad!.Scripts);
        Assert.Equal("DefaultScript", script.Name);
        Assert.Equal("Local", script.Flags);

        var (name, value) = script.Properties.First(p => p.Name == "IsActive");
        Assert.Equal("IsActive", name);
        Assert.Equal("Bool", value.Type);
        Assert.Equal(true, value.Value);
    }

    [Fact]
    public void GetVmad_ReturnsObjectProperty_WithFormKeyAndAlias()
    {
        using var repo = LoadedRepository();

        var vmad = repo.GetVmad(_npcFormKey.ToString(), "VmadQuery.esp");

        var prop = vmad!.Scripts[0].Properties.First(p => p.Name == "TargetActor").Value;
        Assert.Equal("Object", prop.Type);
        Assert.Equal(_targetFormKey.ToString(), prop.Value);
        Assert.Equal((short)-1, prop.Alias);
    }

    [Fact]
    public void GetVmad_ReconstructsScalarArray_InOrder()
    {
        using var repo = LoadedRepository();

        var vmad = repo.GetVmad(_npcFormKey.ToString(), "VmadQuery.esp");

        var prop = vmad!.Scripts[0].Properties.First(p => p.Name == "Scores").Value;
        Assert.Equal("ArrayOfInt", prop.Type);
        Assert.NotNull(prop.ListItems);
        Assert.Equal(new object?[] { 10, 20, 30 }, prop.ListItems!.Select(i => i.Value));
    }

    [Fact]
    public void GetVmad_ReconstructsStructMembers_FromStructJson()
    {
        using var repo = LoadedRepository();

        var vmad = repo.GetVmad(_npcFormKey.ToString(), "VmadQuery.esp");

        var config = vmad!.Scripts[0].Properties.First(p => p.Name == "Config").Value;
        Assert.Equal("Struct", config.Type);
        Assert.NotNull(config.Members);

        // Struct fields surface as named members (the unnamed binary ScriptEntry wrapper is flattened away).
        var factor = config.Members!.First(m => m.Name == "Factor").Value;
        Assert.Equal("Float", factor.Type);
        Assert.Equal(1.5f, factor.Value);
    }

    [Fact]
    public void GetVmad_ReturnsNull_WhenRecordHasNoVmad()
    {
        using var repo = LoadedRepository();

        Assert.Null(repo.GetVmad(_plainNpcFormKey.ToString(), "VmadQuery.esp"));
    }
}
