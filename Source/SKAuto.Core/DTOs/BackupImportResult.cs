namespace SKAuto.Core.DTOs
{
    public class BackupImportResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int RowsInserted { get; set; }
        public int RowsUpdated { get; set; }
        public int RowsSkipped { get; set; }
        public List<Conflict> Conflicts { get; set; } = new();
    }

    public class Conflict
    {
        public string Table { get; set; }
        public string Key { get; set; }
        public string ExistingValue { get; set; }
        public string ImportedValue { get; set; }
    }
}