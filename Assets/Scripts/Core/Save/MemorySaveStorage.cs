namespace Deepshaft.Save
{
    /// <summary>Keeps the save in memory. For tests.</summary>
    public sealed class MemorySaveStorage : ISaveStorage
    {
        public string Primary { get; set; }
        public string Backup { get; set; }

        public bool TryReadPrimary(out string text)
        {
            text = Primary;
            return text != null;
        }

        public bool TryReadBackup(out string text)
        {
            text = Backup;
            return text != null;
        }

        public void Write(string text)
        {
            if (Primary != null) Backup = Primary;
            Primary = text;
        }

        public void DeleteAll()
        {
            Primary = null;
            Backup = null;
        }
    }
}
