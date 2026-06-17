namespace MEditService.Core.Edits;

public sealed class PreparedPluginSave(string tmpPath, string finalPath, SaveResult result) : IDisposable
{
    private bool _committed;

    public SaveResult Result => result;

    public void Commit()
    {
        File.Move(tmpPath, finalPath, overwrite: true);
        _committed = true;
    }

    public void Dispose()
    {
        try
        {
            if (!_committed)
                File.Delete(tmpPath);
            var tmpDir = Path.GetDirectoryName(tmpPath);
            if (tmpDir != null && Directory.Exists(tmpDir))
                Directory.Delete(tmpDir);
        }
        catch (IOException) { /* best-effort; temp file will remain on disk */ }
        catch (UnauthorizedAccessException) { /* Windows file lock (AV/game); temp file will remain */ }
    }
}
