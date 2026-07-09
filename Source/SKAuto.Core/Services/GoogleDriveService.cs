using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using SKAuto.Core.DTOs;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS0618 // CreatedTime is obsolete but still works

namespace SKAuto.Core.Services
{
    public class GoogleDriveService : IGoogleDriveService
    {
        private DriveService _driveService;
        private readonly ILoggingService _logger;
        private readonly string _tokenFolder;
        private GoogleDriveSettings _settings;

        public GoogleDriveService(ILoggingService logger)
        {
            _logger = logger;
            _tokenFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SKAuto", "DriveTokens");
            Directory.CreateDirectory(_tokenFolder);
        }

        public async Task<bool> AuthenticateAsync(GoogleDriveSettings settings)
        {
            _settings = settings;
            try
            {
                UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new ClientSecrets
                    {
                        ClientId = settings.ClientId,
                        ClientSecret = settings.ClientSecret
                    },
                    new[] { DriveService.Scope.DriveFile },
                    settings.UserEmail ?? "user",
                    CancellationToken.None,
                    new FileDataStore(_tokenFolder, true));

                _driveService = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "SK Auto Work Tracking",
                });

                _settings.IsConnected = true;
                _logger.LogInfo("Google Drive authenticated successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Google Drive authentication failed", ex);
                _settings.IsConnected = false;
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (_driveService == null) return false;
            try
            {
                var request = _driveService.About.Get();
                request.Fields = "user";
                var about = await request.ExecuteAsync();
                return about != null;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> UploadFileAsync(string localPath, string remoteFileName = null)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            // Always use the correct folder ID
            string folderId = await GetFolderIdAsync("SKAuto Backups");

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = remoteFileName ?? Path.GetFileName(localPath),
                Parents = new[] { folderId }
            };

            using (var stream = new FileStream(localPath, FileMode.Open))
            {
                try
                {
                    var request = _driveService.Files.Create(fileMetadata, stream, GetMimeType(localPath));
                    request.Fields = "id";
                    var result = await request.UploadAsync();
                    if (result.Status == Google.Apis.Upload.UploadStatus.Completed)
                    {
                        return request.ResponseBody.Id;
                    }
                    else
                    {
                        throw new Exception($"Upload failed: {result.Exception?.Message}");
                    }
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception($"The folder 'SKAuto Backups' was not found.", ex);
                }
            }
        }

        public async Task<bool> TestFolderAccessAsync(string folderId)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            folderId = ExtractFolderId(folderId);
            try
            {
                var request = _driveService.Files.Get(folderId);
                request.Fields = "id, name, mimeType";
                var file = await request.ExecuteAsync();
                return file.MimeType == "application/vnd.google-apps.folder";
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<bool> DownloadFileAsync(string fileId, string localPath)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");

            var request = _driveService.Files.Get(fileId);
            using (var stream = new FileStream(localPath, FileMode.Create))
            {
                await request.DownloadAsync(stream);
            }
            return true;
        }

        public Task<DateTime?> GetLastSyncAsync()
        {
            return Task.FromResult(_settings?.LastSync);
        }

        public void SetLastSync(DateTime time)
        {
            if (_settings != null)
                _settings.LastSync = time;
        }

        private string GetMimeType(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".db" => "application/x-sqlite3",
                ".zip" => "application/zip",
                ".csv" => "text/csv",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }

        public async Task<string> UploadFileAsync(string localPath, string remoteFileName = null, string folderId = null)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            // If folderId is provided, use it; otherwise use the default folder
            string targetFolderId = folderId;
            if (string.IsNullOrEmpty(targetFolderId))
                targetFolderId = await GetFolderIdAsync("SKAuto Backups");

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = remoteFileName ?? Path.GetFileName(localPath)
            };
            if (!string.IsNullOrEmpty(targetFolderId))
                fileMetadata.Parents = new[] { targetFolderId };

            using (var stream = new FileStream(localPath, FileMode.Open))
            {
                var request = _driveService.Files.Create(fileMetadata, stream, GetMimeType(localPath));
                request.Fields = "id";
                var result = await request.UploadAsync();
                if (result.Status == Google.Apis.Upload.UploadStatus.Completed)
                {
                    return request.ResponseBody.Id;
                }
                else
                {
                    throw new Exception($"Upload failed: {result.Exception?.Message}");
                }
            }
        }

        public async Task<string> GetFolderIdAsync(string folderName)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");

            // Search for existing folder
            var request = _driveService.Files.List();
            request.Q = $"mimeType='application/vnd.google-apps.folder' and name='{folderName}' and trashed=false";
            request.Fields = "files(id, name)";
            var result = await request.ExecuteAsync();

            var folder = result.Files.FirstOrDefault();
            if (folder != null)
                return folder.Id;

            // Create folder if not found
            var folderMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };
            var createRequest = _driveService.Files.Create(folderMetadata);
            createRequest.Fields = "id";
            var newFolder = await createRequest.ExecuteAsync();
            return newFolder.Id;
        }

        // ========== HELPERS ==========
        private string ExtractFolderId(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            if (!input.Contains("/") && !input.Contains("?") && !input.Contains("&"))
                return input;

            var match = Regex.Match(input, @"folders/([a-zA-Z0-9-_]+)");
            if (match.Success)
                return match.Groups[1].Value;

            match = Regex.Match(input, @"[?&]id=([a-zA-Z0-9-_]+)");
            if (match.Success)
                return match.Groups[1].Value;

            return input;
        }

        // ========== LIST BACKUPS FROM DRIVE ==========
        public async Task<List<BackupFileInfo>> ListDriveBackupsAsync()
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");

            var result = new List<BackupFileInfo>();

            // Always get the correct folder ID
            string folderId = await GetFolderIdAsync("SKAuto Backups");

            if (string.IsNullOrEmpty(folderId))
            {
                _logger.LogWarning("Could not find or create folder 'SKAuto Backups'.");
                return result;
            }

            _logger.LogInfo($"Using folder ID: {folderId}");

            // Query files in that folder
            var request = _driveService.Files.List();
            request.Q = $"'{folderId}' in parents and trashed=false";
            request.Fields = "files(id, name, createdTime, size, mimeType)";
            request.OrderBy = "createdTime desc";

            var files = await request.ExecuteAsync();
            _logger.LogInfo($"Folder '{folderId}' contains {files.Files.Count} files.");

            var backupFiles = files.Files
                .Where(f =>
                    f.MimeType == "application/zip" ||
                    f.Name?.IndexOf(".zip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name?.IndexOf(".db", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            _logger.LogInfo($"Found {backupFiles.Count} backup files in folder.");

            foreach (var file in backupFiles)
            {
                DateTime createdAt = file.CreatedTime.HasValue ? file.CreatedTime.Value : DateTime.Now;
                result.Add(new BackupFileInfo
                {
                    FilePath = file.Id,
                    FileName = file.Name,
                    CreatedAt = createdAt,
                    SizeInBytes = file.Size ?? 0
                });
            }
            return result;
        }

        // ========== DOWNLOAD DRIVE BACKUP ==========
        public async Task<string> DownloadDriveBackupAsync(string fileId)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");

            // Get file name
            var getRequest = _driveService.Files.Get(fileId);
            getRequest.Fields = "name";
            var fileMeta = await getRequest.ExecuteAsync();
            string fileName = fileMeta.Name ?? "backup.zip";

            // Use a temporary folder (not Documents)
            string tempDir = Path.Combine(Path.GetTempPath(), "SKAuto_Restore");
            Directory.CreateDirectory(tempDir);
            string localPath = Path.Combine(tempDir, fileName);

            var downloadRequest = _driveService.Files.Get(fileId);
            using (var stream = new FileStream(localPath, FileMode.Create))
            {
                await downloadRequest.DownloadAsync(stream);
            }
            return localPath;
        }
    }
}

#pragma warning restore CS0618