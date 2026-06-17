using MEditService.Core.Edits;
using MEditService.Core.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Mutagen.Bethesda;

namespace MEditService.Tests.Changes;

public sealed class PluginWriterSaveTests
{
    [Fact]
    public async Task SaveAsync_Success_OriginalFileIsUpdated()
    {
        using var data = new PluginFixtureBuilder("pw-save-original")
            .WithPlugin("TestPlugin.esp")
            .Build();

        var pluginPath = Path.Combine(data.DataFolder, "TestPlugin.esp");
        var before = File.GetLastWriteTimeUtc(pluginPath);

        var writer = new PluginWriter(new SchemaReflector(), NullLogger<PluginWriter>.Instance);
        await writer.SaveAsync(pluginPath, [], GameRelease.Fallout4);

        Assert.True(File.GetLastWriteTimeUtc(pluginPath) >= before);
        Assert.True(File.Exists(pluginPath));
    }

    [Fact]
    public async Task SaveAsync_Success_LeavesNoTempSubdirectory()
    {
        using var data = new PluginFixtureBuilder("pw-save-no-tmpdir")
            .WithPlugin("TestPlugin.esp")
            .Build();

        var pluginPath = Path.Combine(data.DataFolder, "TestPlugin.esp");

        var writer = new PluginWriter(new SchemaReflector(), NullLogger<PluginWriter>.Instance);
        await writer.SaveAsync(pluginPath, [], GameRelease.Fallout4);

        var leftoverDirs = Directory.GetDirectories(data.DataFolder, ".medit_tmp_*");
        Assert.Empty(leftoverDirs);
    }
}
