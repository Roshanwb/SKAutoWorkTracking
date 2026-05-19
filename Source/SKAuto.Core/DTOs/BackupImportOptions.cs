namespace SKAuto.Core.DTOs
{
    public enum ConflictResolution
    {
        Skip,      // Skip existing rows
        Overwrite  // Replace existing data
    }

    public class BackupImportOptions
    {
        public ConflictResolution Conflict { get; set; } = ConflictResolution.Skip;
        public bool DryRun { get; set; } = false;
    }
}