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

        // --- Sync methods ---
        Task<bool> IsConnectedAsync();
        Task<Google.Apis.Drive.v3.Data.File?> GetFileByNameAsync(string fileName);
        Task<string> DownloadFileContentAsync(string fileId);
        Task UploadFileContentAsync(string fileName, string content);
        Task DeleteFileAsync(string fileId);

        // NEW: Get subfolder ID under a parent folder
        Task<string> GetSubFolderIdAsync(string parentFolderId, string folderName);
    }
}