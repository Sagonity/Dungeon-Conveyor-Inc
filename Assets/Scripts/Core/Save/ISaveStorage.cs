namespace Deepshaft.Save
{
    /// <summary>
    /// Where save text lives. The game uses <see cref="FileSaveStorage"/>; tests use
    /// <see cref="MemorySaveStorage"/>. Cloud save (post-MVP, SCRUM-17) would be another one.
    /// </summary>
    public interface ISaveStorage
    {
        bool TryReadPrimary(out string text);

        /// <summary>The previous save, kept in case the latest one is damaged.</summary>
        bool TryReadBackup(out string text);

        /// <summary>Writes a new save. The current one becomes the backup.</summary>
        void Write(string text);

        void DeleteAll();
    }
}
