using System.Runtime.InteropServices;
using Deepshaft.Save;

/// <summary>
/// The save storage for the app (SCRUM-35). On every platform it's the normal <see cref="FileSaveStorage"/>.
/// In a WebGL build it also syncs the files to the browser's storage (IndexedDB) after each write,
/// because a browser keeps <c>persistentDataPath</c> in memory until it's synced.
/// </summary>
public sealed class WebSaveStorage : ISaveStorage
{
    readonly FileSaveStorage files = FileSaveStorage.CreateDefault();

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void DeepshaftSyncFiles();
#else
    static void DeepshaftSyncFiles() { }
#endif

    public bool TryReadPrimary(out string text) => files.TryReadPrimary(out text);

    public bool TryReadBackup(out string text) => files.TryReadBackup(out text);

    public void Write(string text)
    {
        files.Write(text);
        DeepshaftSyncFiles();
    }

    public void DeleteAll()
    {
        files.DeleteAll();
        DeepshaftSyncFiles();
    }
}
