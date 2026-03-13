using SKAuto.Core.DTOs;
using System.Threading.Tasks;

namespace SKAuto.Core.Interfaces
{
    public interface IBackupService
    {
        Task<string> BackupDatabaseAsync(string backupFolder);
        Task<string> ExportDataAsync(string exportFolder);
        Task<BackupImportResult> ImportDataAsync(string zipPath, BackupImportOptions options);
    }
}