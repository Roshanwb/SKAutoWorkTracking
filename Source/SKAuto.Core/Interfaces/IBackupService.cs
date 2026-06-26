using SKAuto.Core.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SKAuto.Core.Interfaces
{
    public interface IBackupService
    {
        Task<string> BackupDatabaseAsync(string backupFolder);
        Task<string> ExportDataAsync(string exportFolder);
        Task<BackupImportResult> ImportDataAsync(string zipPath, BackupImportOptions options);
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