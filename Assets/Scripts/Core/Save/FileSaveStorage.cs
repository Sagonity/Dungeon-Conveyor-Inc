using System.IO;
using System.Text;
using UnityEngine;

namespace Deepshaft.Save
{
    /// <summary>
    /// Saves to a file, by default in Application.persistentDataPath.
    /// Writes go to a temporary file first, so a crash mid-write can't destroy the
    /// existing save; the previous save is kept as a backup.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        static readonly Encoding Utf8 = new UTF8Encoding(false);

        readonly string primaryPath;
        readonly string backupPath;
        readonly string tempPath;

        public FileSaveStorage(string directory, string fileName = "save.json")
        {
            Directory.CreateDirectory(directory);
            primaryPath = Path.Combine(directory, fileName);
            backupPath = primaryPath + ".bak";
            tempPath = primaryPath + ".tmp";
        }

        public static FileSaveStorage CreateDefault() => new FileSaveStorage(Application.persistentDataPath);

        public string PrimaryPath => primaryPath;

        public bool TryReadPrimary(out string text) => TryRead(primaryPath, out text);

        public bool TryReadBackup(out string text) => TryRead(backupPath, out text);

        public void Write(string text)
        {
            File.WriteAllText(tempPath, text, Utf8);
            if (File.Exists(primaryPath))
            {
                File.Copy(primaryPath, backupPath, true);
                File.Delete(primaryPath);
            }
            // If the app dies right here, loading falls back to the backup.
            File.Move(tempPath, primaryPath);
        }

        public void DeleteAll()
        {
            foreach (string path in new[] { primaryPath, backupPath, tempPath })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        static bool TryRead(string path, out string text)
        {
            text = null;
            if (!File.Exists(path)) return false;
            try
            {
                text = File.ReadAllText(path, Utf8);
                return true;
            }
            catch (IOException e)
            {
                Debug.LogWarning("Could not read save file " + path + ": " + e.Message);
                return false;
            }
        }
    }
}
