using SKAuto.Core.DTOs;
using System.Threading.Tasks;

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
    }
}