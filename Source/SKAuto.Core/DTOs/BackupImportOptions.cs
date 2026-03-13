namespace SKAuto.Core.DTOs
{
    public enum ConflictResolution
    {
        Prompt,     // Ask user each time (default)
        Skip,       // Ignore conflicting rows
        Overwrite,  // Replace existing data
        Merge       // Update if exists, insert if new (based on business key)
    }

    public class BackupImportOptions
    {
        public ConflictResolution ClientConflict { get; set; } = ConflictResolution.Prompt;
        public ConflictResolution VehicleConflict { get; set; } = ConflictResolution.Prompt;
        public ConflictResolution AccessoryConflict { get; set; } = ConflictResolution.Prompt;
        public ConflictResolution WorkOrderConflict { get; set; } = ConflictResolution.Prompt;
        public ConflictResolution WorkTaskConflict { get; set; } = ConflictResolution.Prompt;
        public ConflictResolution TravelConflict { get; set; } = ConflictResolution.Prompt;
        public ConflictResolution ProtectedRateConflict { get; set; } = ConflictResolution.Prompt;
        public ConflictResolution UserConflict { get; set; } = ConflictResolution.Prompt;
        public ConflictResolution SourceDocumentConflict { get; set; } = ConflictResolution.Prompt;
        public bool DryRun { get; set; } = false; // Preview changes without saving
    }
}