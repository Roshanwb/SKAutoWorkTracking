using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using SKAuto.Core.DTOs;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#pragma warning disable CS0618 // CreatedTime is obsolete

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

        public async Task<bool> IsConnectedAsync() => await TestConnectionAsync();

        public async Task<string> UploadFileAsync(string localPath, string remoteFileName = null)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            string folderId = await GetFolderIdAsync("SKAuto Backups");

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = remoteFileName ?? Path.GetFileName(localPath),
                Parents = new[] { folderId }
            };

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

        public async Task<string> GetFolderIdAsync(string folderName)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");

            var request = _driveService.Files.List();
            request.Q = $"mimeType='application/vnd.google-apps.folder' and name='{folderName}' and trashed=false";
            request.Fields = "files(id, name)";
            var result = await request.ExecuteAsync();

            var folder = result.Files.FirstOrDefault();
            if (folder != null)
                return folder.Id;

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

        public Task<DateTime?> GetLastSyncAsync() => Task.FromResult(_settings?.LastSync);
        public void SetLastSync(DateTime time) { if (_settings != null) _settings.LastSync = time; }

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

        public async Task<string> UploadFileAsync(string localPath, string remoteFileName = null, string folderId = null)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            string targetFolderId = folderId ?? await GetFolderIdAsync("SKAuto Backups");

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = remoteFileName ?? Path.GetFileName(localPath),
                Parents = new[] { targetFolderId }
            };

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

        public async Task<string> UploadOrReplaceFileAsync(string localPath, string remoteFileName, string folderName)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");

            string folderId = await GetFolderIdAsync(folderName);

            var listRequest = _driveService.Files.List();
            listRequest.Q = $"name='{remoteFileName}' and '{folderId}' in parents and trashed=false";
            listRequest.Fields = "files(id, name)";
            var listResult = await listRequest.ExecuteAsync();

            var existing = listResult.Files.FirstOrDefault();
            if (existing != null)
            {
                await _driveService.Files.Delete(existing.Id).ExecuteAsync();
                _logger.LogInfo($"Deleted existing remote file '{remoteFileName}' (ID {existing.Id}) before replacement upload.");
            }

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = remoteFileName,
                Parents = new[] { folderId }
            };

            using (var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read))
            {
                var uploadRequest = _driveService.Files.Create(fileMetadata, stream, GetMimeType(localPath));
                uploadRequest.Fields = "id";
                var uploadResult = await uploadRequest.UploadAsync();

                if (uploadResult.Status == Google.Apis.Upload.UploadStatus.Completed)
                {
                    _logger.LogInfo($"Uploaded '{remoteFileName}' to folder '{folderName}' (ID {uploadRequest.ResponseBody.Id}).");
                    return uploadRequest.ResponseBody.Id;
                }

                throw new Exception($"Upload failed: {uploadResult.Exception?.Message}");
            }
        }

        // NEW: Get file by name inside a specific folder (legacy fallback)
        public async Task<Google.Apis.Drive.v3.Data.File?> GetFileByNameInFolderAsync(string fileName, string folderName)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");

            string folderId = await GetFolderIdAsync(folderName);

            var request = _driveService.Files.List();
            request.Q = $"name='{fileName}' and '{folderId}' in parents and trashed=false";
            request.Fields = "files(id, name, mimeType, createdTime)";
            request.OrderBy = "createdTime desc";

            var result = await request.ExecuteAsync();
            return result.Files.FirstOrDefault();
        }

        public async Task<List<BackupFileInfo>> ListDriveBackupsAsync()
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            var result = new List<BackupFileInfo>();
            string folderId = await GetFolderIdAsync("SKAuto Backups");
            if (string.IsNullOrEmpty(folderId)) return result;

            var request = _driveService.Files.List();
            request.Q = $"'{folderId}' in parents and trashed=false";
            request.Fields = "files(id, name, createdTime, size, mimeType)";
            request.OrderBy = "createdTime desc";

            var files = await request.ExecuteAsync();
            var backupFiles = files.Files
                .Where(f => f.MimeType == "application/zip" ||
                            f.Name?.IndexOf(".zip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            f.Name?.IndexOf(".db", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

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

        public async Task<string> DownloadDriveBackupAsync(string fileId)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            var getRequest = _driveService.Files.Get(fileId);
            getRequest.Fields = "name";
            var fileMeta = await getRequest.ExecuteAsync();
            string fileName = fileMeta.Name ?? "backup.zip";

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

        public async Task<Google.Apis.Drive.v3.Data.File?> GetFileByNameAsync(string fileName)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            string folderId = await GetFolderIdAsync("SKAuto Data");
            var request = _driveService.Files.List();
            request.Q = $"name='{fileName}' and '{folderId}' in parents and trashed=false";
            request.Fields = "files(id, name, mimeType)";
            var result = await request.ExecuteAsync();
            return result.Files.FirstOrDefault();
        }

        public async Task<string> DownloadFileContentAsync(string fileId)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            var request = _driveService.Files.Get(fileId);
            using var stream = new MemoryStream();
            await request.DownloadAsync(stream);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        public async Task UploadFileContentAsync(string fileName, string content)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            string folderId = await GetFolderIdAsync("SKAuto Data");
            var existing = await GetFileByNameAsync(fileName);

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

            if (existing != null)
            {
                await DeleteFileAsync(existing.Id);
            }

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = fileName,
                Parents = new[] { folderId }
            };
            var request = _driveService.Files.Create(fileMetadata, stream, "text/plain");
            request.Fields = "id";
            await request.UploadAsync();
        }

        public async Task DeleteFileAsync(string fileId)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            await _driveService.Files.Delete(fileId).ExecuteAsync();
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

        public async Task<string> GetSubFolderIdAsync(string parentFolderId, string folderName)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");

            var request = _driveService.Files.List();
            request.Q = $"mimeType='application/vnd.google-apps.folder' and name='{folderName}' and '{parentFolderId}' in parents and trashed=false";
            request.Fields = "files(id, name)";
            var result = await request.ExecuteAsync();

            var folder = result.Files.FirstOrDefault();
            if (folder != null)
                return folder.Id;

            var folderMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder",
                Parents = new[] { parentFolderId }
            };
            var createRequest = _driveService.Files.Create(folderMetadata);
            createRequest.Fields = "id";
            var newFolder = await createRequest.ExecuteAsync();
            return newFolder.Id;
        }
    }
}
#pragma warning restore CS0618