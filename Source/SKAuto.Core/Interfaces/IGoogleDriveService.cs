using SKAuto.Core.DTOs;

namespace SKAuto.Core.Interfaces
{
    public interface IGoogleDriveService
    {
        Task<bool> AuthenticateAsync(GoogleDriveSettings settings);
        Task<bool> TestConnectionAsync();
        Task<string> UploadFileAsync(string localPath, string remoteFileName = null);
        Task<bool> DownloadFileAsync(string fileId, string localPath);
        Task<string> GetFolderIdAsync(string folderName);
        Task<DateTime?> GetLastSyncAsync();
        void SetLastSync(DateTime time);
        Task<bool> TestFolderAccessAsync(string folderId);
        Task<string> UploadFileAsync(string localPath, string remoteFileName = null, string folderId = null);

        // NEW: List backup files from Google Drive
        Task<List<BackupFileInfo>> ListDriveBackupsAsync();

        // NEW: Download a backup file from Drive to local path (returns local path)
        Task<string> DownloadDriveBackupAsync(string fileId);
    }
}