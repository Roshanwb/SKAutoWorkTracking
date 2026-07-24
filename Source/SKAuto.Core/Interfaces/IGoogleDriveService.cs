using SKAuto.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SKAuto.Core.Interfaces
{
    public interface IGoogleDriveService
    {
        // --- Existing methods ---
        Task<bool> AuthenticateAsync(GoogleDriveSettings settings);
        Task<bool> TestConnectionAsync();
        Task<string> UploadFileAsync(string localPath, string remoteFileName = null);
        Task<bool> DownloadFileAsync(string fileId, string localPath);
        Task<string> GetFolderIdAsync(string folderName);
        Task<DateTime?> GetLastSyncAsync();
        void SetLastSync(DateTime time);
        Task<bool> TestFolderAccessAsync(string folderId);
        Task<string> UploadFileAsync(string localPath, string remoteFileName = null, string folderId = null);
        Task<List<BackupFileInfo>> ListDriveBackupsAsync();
        Task<string> DownloadDriveBackupAsync(string fileId);

        // --- NEW methods for sync ---
        Task<bool> IsConnectedAsync();
        Task<Google.Apis.Drive.v3.Data.File?> GetFileByNameAsync(string fileName);
        Task<string> DownloadFileContentAsync(string fileId);
        Task UploadFileContentAsync(string fileName, string content);
        Task DeleteFileAsync(string fileId);
    }
}