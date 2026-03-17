using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using SKAuto.Core.DTOs;
using SKAuto.Core.Interfaces;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = remoteFileName ?? Path.GetFileName(localPath),
                Parents = new[] { _settings.FolderId } // upload to specified folder
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
                    throw new Exception($"The folder with ID '{_settings.FolderId}' was not found. Please check the folder ID and try again.", ex);
                }
            }
        }
        public async Task<bool> TestFolderAccessAsync(string folderId)
        {
            if (_driveService == null) throw new InvalidOperationException("Not authenticated");
            try
            {
                var request = _driveService.Files.Get(folderId);
                request.Fields = "id, name, mimeType";
                var file = await request.ExecuteAsync();
                // If it's a folder, mimeType should be application/vnd.google-apps.folder
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

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = remoteFileName ?? Path.GetFileName(localPath)
            };
            if (!string.IsNullOrEmpty(folderId))
                fileMetadata.Parents = new[] { folderId };

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
            if (result.Files.Any())
                return result.Files.First().Id;

            // Create folder if not found
            var folderMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };
            var createRequest = _driveService.Files.Create(folderMetadata);
            createRequest.Fields = "id";
            var folder = await createRequest.ExecuteAsync();
            return folder.Id;
        }
    }
}