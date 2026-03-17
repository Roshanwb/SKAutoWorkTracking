namespace SKAuto.Core.DTOs
{
    public class GoogleDriveSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string UserEmail { get; set; } // optional, for display
        public string FolderId { get; set; } // ID of the folder to store backups
        public bool IsConnected { get; set; }
        public DateTime? LastSync { get; set; }
        public int SyncIntervalDays { get; set; } = 7; // weekly default
    }
}