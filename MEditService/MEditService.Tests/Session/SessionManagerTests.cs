using System.Text.Json;
using MEditService.Core.Edits;
using MEditService.Core.Queries;
using MEditService.Core.Records;
using MEditService.Core.Schema;
using MEditService.Core.Session;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;

namespace MEditService.Tests.Session;

public class SessionManagerTests : IClassFixture<TestPluginFixture>
{
    private readonly TestPluginFixture _fixture;

    private static readonly ISchemaReflector _reflector = new SchemaReflector();
    private static readonly ITableDdlBuilder _ddl = new TableDdlBuilder(_reflector);
    private static readonly IFieldMetadataMapper _mapper = new FieldMetadataMapper();

    public SessionManagerTests(TestPluginFixture fixture) => _fixture = fixture;

    private static JsonElement J(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private static PendingChange MakePendingChange(string formKey, string plugin, string fieldPath, string recordType, string json) =>
        new(Guid.NewGuid(), formKey, plugin, fieldPath, recordType,
            J("null"), J(json), "user", null, DateTime.UtcNow);

    private SessionManager MakeManager() => new(_reflector, _ddl, _mapper, new PluginWriter(_reflector));

    [Fact]
    public void Load_PopulatesSessionAndRepository()
    {
        using var manager = MakeManager();
        manager.Load(_fixture.DataFolder, _fixture.PluginsTxtPath, GameRelease.Fallout4);

        Assert.NotNull(manager.Session);
        Assert.NotNull(manager.Repository);
        Assert.Single(manager.Session.Plugins);
        Assert.Equal(TestPluginFixture.PluginName, manager.Session.Plugins[0].Name);
    }

    [Fact]
    public void Load_IndexesRecordsIntoRepository()
    {
        using var manager = MakeManager();
        manager.Load(_fixture.DataFolder, _fixture.PluginsTxtPath, GameRelease.Fallout4);

        var count = manager.Repository!.CountRecordsForPlugin("npc_", TestPluginFixture.PluginName);

        Assert.Equal(TestPluginFixture.RecordCount, count);
    }

    [Fact]
    public void Load_SetsIsWinnerOnSinglePlugin()
    {
        using var manager = MakeManager();
        manager.Load(_fixture.DataFolder, _fixture.PluginsTxtPath, GameRelease.Fallout4);

        var result = manager.Repository!.GetRecords("npc_", null, null, 100, 0);

        Assert.Equal(TestPluginFixture.RecordCount, result.Total);
        Assert.All(result.Items, r => Assert.True(r.IsWinner));
    }

    [Fact]
    public void Unload_ClearsSessionAndRepository()
    {
        using var manager = MakeManager();
        manager.Load(_fixture.DataFolder, _fixture.PluginsTxtPath, GameRelease.Fallout4);
        manager.Unload();

        Assert.Null(manager.Session);
        Assert.Null(manager.Repository);
    }

    [Fact]
    public void Load_ReplacesExistingSession()
    {
        using var manager = MakeManager();
        manager.Load(_fixture.DataFolder, _fixture.PluginsTxtPath, GameRelease.Fallout4);
        var firstRepo = manager.Repository;

        manager.Load(_fixture.DataFolder, _fixture.PluginsTxtPath, GameRelease.Fallout4);

        Assert.NotSame(firstRepo, manager.Repository);
        Assert.NotNull(manager.Session);
    }

    [Fact]
    public void Load_WithGameRelease_SessionHasCorrectGameRelease()
    {
        using var manager = MakeManager();
        manager.Load(_fixture.DataFolder, _fixture.PluginsTxtPath, GameRelease.Fallout4);

        Assert.Equal(GameRelease.Fallout4, manager.Session!.GameRelease);
    }

    // --- SavePlugin ---

    [Fact]
    public async Task SavePlugin_WritableField_ReturnsSaveResultWithApplied()
    {
        FormKey npcKey = default;
        var data = new PluginFixtureBuilder("sm-save")
            .WithPlugin("TestPlugin.esp", mod =>
                npcKey = mod.Npcs.AddNew("SaveTestNPC").FormKey)
            .Build();
        using (data)
        {
            using var manager = MakeManager();
            manager.Load(data.DataFolder, data.PluginsTxtPath, GameRelease.Fallout4);
            var change = MakePendingChange(npcKey.ToString(), "TestPlugin.esp", "aggression", "npc_", "\"Frenzied\"");

            var result = await manager.SavePlugin("TestPlugin.esp", [change]);

            Assert.Contains("aggression", result.Applied);
            Assert.Empty(result.ReadOnly);
            Assert.Empty(result.NotFound);
        }
    }

    [Fact]
    public async Task SavePlugin_AfterSave_RepositoryReflectsNewFieldValue()
    {
        FormKey npcKey = default;
        var data = new PluginFixtureBuilder("sm-reindex")
            .WithPlugin("TestPlugin.esp", mod =>
            {
                var npc = mod.Npcs.AddNew("ReindexTestNPC");
                npc.Aggression = Npc.AggressionType.Unaggressive;
                npcKey = npc.FormKey;
            })
            .Build();
        using (data)
        {
            using var manager = MakeManager();
            manager.Load(data.DataFolder, data.PluginsTxtPath, GameRelease.Fallout4);
            var schema = _reflector.GetSchemas(GameRelease.Fallout4)["npc_"];
            var change = MakePendingChange(npcKey.ToString(), "TestPlugin.esp", "aggression", "npc_", "\"Frenzied\"");

            await manager.SavePlugin("TestPlugin.esp", [change]);

            var detail = manager.Repository!.GetRecord("npc_", schema, npcKey.ToString(), "TestPlugin.esp", winnerOnly: false)!;
            var aggressionValue = detail.Fields.First(f => f.Metadata.Name == "aggression").Value?.ToString();
            Assert.Equal("Frenzied", aggressionValue);
        }
    }
}
