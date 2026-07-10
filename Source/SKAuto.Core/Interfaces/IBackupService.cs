using SKAuto.Core.DTOs;

namespace SKAuto.Core.Interfaces
{
    public interface IBackupService
    {
        Task<string> BackupDatabaseAsync(string backupFolder, IProgress<BackupProgress>? progress = null);
        Task<string> ExportDataAsync(string exportFolder, IProgress<BackupProgress>? progress = null);
        Task<BackupImportResult> ImportDataAsync(string zipPath, BackupImportOptions options, IProgress<BackupProgress>? progress = null);
        Task<List<BackupFileInfo>> GetBackupFilesAsync(string backupFolder);
        Task<string> RestoreDatabaseAsync(string backupFilePath);
    }

    public class BackupFileInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public DateTime CreatedAt { get; set; }
        public long SizeInBytes { get; set; }
        public string SizeDisplay => SizeInBytes > 1024 * 1024
            ? $"{SizeInBytes / (1024 * 1024):F1} MB"
            : $"{SizeInBytes / 1024:F1} KB";
    }
}