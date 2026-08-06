namespace SKAuto.Core.Entities
{
    public class SourceDocument : BaseEntity
    {
        public int WorkOrderId { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty;
        public string OriginalFilename { get; set; } = string.Empty;

        // NEW fields for Google Drive storage
        public string GoogleDriveFileId { get; set; } = string.Empty;
        public long? FileSize { get; set; }
        public DateTime? UploadDate { get; set; }
        public string? ContentType { get; set; }

        public virtual WorkOrder WorkOrder { get; set; } = null!;
    }
}