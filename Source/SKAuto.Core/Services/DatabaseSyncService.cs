using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SKAuto.Core.Interfaces;

namespace SKAuto.Core.Services
{
    public class DatabaseSyncService
    {
        private readonly IGoogleDriveService _driveService;
        private readonly ILoggingService _logger;
        private readonly string _dbPath;
        private readonly string _versionFileName = "db_version.json";
        private readonly string _backupFolder;

        public DatabaseSyncService(IGoogleDriveService driveService, ILoggingService logger, string dbPath)
        {
            _driveService = driveService;
            _logger = logger;
            _dbPath = dbPath;
            _backupFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SKAuto",
                "Backups");
            Directory.CreateDirectory(_backupFolder);
        }

        private async Task<int> GetLocalVersionAsync()
        {
            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS SystemInfo (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                )";
            const string getVersionSql = "SELECT Value FROM SystemInfo WHERE Key = 'DbVersion'";

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = createTableSql;
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = getVersionSql;
            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
            {
                const string insertSql = "INSERT INTO SystemInfo (Key, Value) VALUES ('DbVersion', '1')";
                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = insertSql;
                await insertCmd.ExecuteNonQueryAsync();
                return 1;
            }

            return int.TryParse(result.ToString(), out int version) ? version : 1;
        }

        private async Task SetLocalVersionAsync(int version)
        {
            const string updateSql = @"
                INSERT OR REPLACE INTO SystemInfo (Key, Value)
                VALUES ('DbVersion', @version)";

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = updateSql;
            cmd.Parameters.AddWithValue("@version", version.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        private string ComputeFileHash(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private async Task<(int Version, DateTime UpdatedAt, string Machine, string Hash)> GetRemoteVersionAsync()
        {
            try
            {
                var versionFile = await _driveService.GetFileByNameAsync(_versionFileName);
                if (versionFile == null)
                    return (0, DateTime.MinValue, "", "");

                var json = await _driveService.DownloadFileContentAsync(versionFile.Id);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                return (
                    root.GetProperty("VersionNumber").GetInt32(),
                    root.GetProperty("LastUpdatedUtc").GetDateTime(),
                    root.GetProperty("UpdatedByMachine").GetString() ?? "",
                    root.GetProperty("FileHashSha256").GetString() ?? ""
                );
            }
            catch
            {
                return (0, DateTime.MinValue, "", "");
            }
        }

        private async Task UploadRemoteVersionAsync(int version, string machineName)
        {
            var hash = ComputeFileHash(_dbPath);
            var json = JsonSerializer.Serialize(new
            {
                VersionNumber = version,
                LastUpdatedUtc = DateTime.UtcNow,
                UpdatedByMachine = machineName,
                FileHashSha256 = hash
            });

            await _driveService.UploadFileContentAsync(_versionFileName, json);
        }

        private async Task VacuumDatabaseAsync()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            using var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA journal_mode=DELETE";
            await pragmaCmd.ExecuteNonQueryAsync();

            using var vacuumCmd = connection.CreateCommand();
            vacuumCmd.CommandText = "VACUUM";
            await vacuumCmd.ExecuteNonQueryAsync();
        }

        private async Task DownloadRemoteDatabaseAsync()
        {
            var dbFile = await _driveService.GetFileByNameAsync("SKAuto.db");
            if (dbFile == null)
                throw new Exception("Remote database file not found on Google Drive.");

            if (File.Exists(_dbPath))
            {
                var backupPath = Path.Combine(_backupFolder, $"SKAuto_PRE_SYNC_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                File.Copy(_dbPath, backupPath, true);
                _logger.LogInfo($"Local DB backed up to {backupPath}");
            }

            await _driveService.DownloadFileAsync(dbFile.Id, _dbPath);
            _logger.LogInfo($"Remote database downloaded to {_dbPath}");
        }

        // ========== FIXED: Upload with connection pool clearing ==========
        private async Task UploadLocalDatabaseAsync()
        {
            // Force close all pooled connections to release file lock
            SqliteConnection.ClearAllPools();
            await Task.Delay(100); // allow OS to release file handles

            await VacuumDatabaseAsync();

            // Clear pools again after vacuum (in case vacuum left something open)
            SqliteConnection.ClearAllPools();
            await Task.Delay(100);

            await _driveService.UploadFileAsync(_dbPath, "SKAuto.db");
            _logger.LogInfo($"Local database uploaded to Google Drive");
        }

        public async Task<SyncResultInfo> SyncOnStartupAsync(string machineName)
        {
            var result = new SyncResultInfo
            {
                Result = SyncResult.Success,
                LocalVersion = await GetLocalVersionAsync(),
                RemoteVersion = 0
            };

            _logger.LogInfo($"Startup sync - LocalVersion: {result.LocalVersion}");

            try
            {
                (int remoteVersion, _, string remoteMachine, string remoteHash) = await GetRemoteVersionAsync();
                result.RemoteVersion = remoteVersion;

                _logger.LogInfo($"RemoteVersion: {remoteVersion} (last by {remoteMachine})");

                if (remoteVersion == 0)
                {
                    _logger.LogInfo("No remote version found – uploading local DB as master");
                    await UploadLocalDatabaseAsync();
                    await UploadRemoteVersionAsync(result.LocalVersion, machineName);
                    return result;
                }

                if (result.LocalVersion == remoteVersion)
                {
                    _logger.LogInfo("Local and remote versions are in sync");
                    return result;
                }

                if (result.LocalVersion > remoteVersion)
                {
                    _logger.LogInfo($"Local version {result.LocalVersion} > remote {remoteVersion} – uploading local DB (crash recovery)");
                    await UploadLocalDatabaseAsync();
                    await UploadRemoteVersionAsync(result.LocalVersion, machineName);
                    return result;
                }

                if (remoteVersion > result.LocalVersion)
                {
                    if (result.LocalVersion > 1)
                    {
                        _logger.LogWarning($"Conflict: Local {result.LocalVersion}, Remote {remoteVersion}");
                        result.Result = SyncResult.ConflictDetected;

                        var crashBackupPath = Path.Combine(_backupFolder, $"SKAuto_CRASH_BACKUP_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                        File.Copy(_dbPath, crashBackupPath, true);
                        result.LocalBackupPath = crashBackupPath;
                        _logger.LogInfo($"Local DB backed up to {crashBackupPath}");

                        return result;
                    }

                    _logger.LogInfo($"Remote version {remoteVersion} > local {result.LocalVersion} – downloading remote DB");
                    await DownloadRemoteDatabaseAsync();
                    await SetLocalVersionAsync(remoteVersion);
                    result.LocalVersion = remoteVersion;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("SyncOnStartupAsync failed", ex);
                result.Result = SyncResult.DatabaseError;
                result.Message = ex.Message;
                return result;
            }
        }

        public async Task<SyncResultInfo> SyncOnExitAsync(string machineName)
        {
            var result = new SyncResultInfo
            {
                Result = SyncResult.Success,
                LocalVersion = await GetLocalVersionAsync(),
                RemoteVersion = 0
            };

            _logger.LogInfo($"Exit sync - LocalVersion: {result.LocalVersion}");

            try
            {
                int newVersion = result.LocalVersion + 1;
                await SetLocalVersionAsync(newVersion);
                result.LocalVersion = newVersion;

                await UploadLocalDatabaseAsync();
                await UploadRemoteVersionAsync(newVersion, machineName);

                _logger.LogInfo($"Exit sync complete - Version {newVersion} uploaded");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("SyncOnExitAsync failed", ex);
                result.Result = SyncResult.DatabaseError;
                result.Message = ex.Message;
                return result;
            }
        }

        public async Task<SyncResultInfo> ResolveConflictAsync(bool useRemote)
        {
            var result = new SyncResultInfo
            {
                Result = SyncResult.Success,
                LocalVersion = await GetLocalVersionAsync()
            };

            try
            {
                var (remoteVersion, _, _, _) = await GetRemoteVersionAsync();
                result.RemoteVersion = remoteVersion;

                if (useRemote)
                {
                    _logger.LogInfo("User chose to download remote DB (keeping remote changes)");
                    await DownloadRemoteDatabaseAsync();
                    await SetLocalVersionAsync(remoteVersion);
                    result.LocalVersion = remoteVersion;
                }
                else
                {
                    _logger.LogInfo("User chose to upload local DB (overwriting remote)");
                    await UploadLocalDatabaseAsync();
                    await UploadRemoteVersionAsync(result.LocalVersion, Environment.MachineName);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("ResolveConflictAsync failed", ex);
                result.Result = SyncResult.DatabaseError;
                result.Message = ex.Message;
                return result;
            }
        }
    }

    public class SyncResultInfo
    {
        public SyncResult Result { get; set; }
        public string? Message { get; set; }
        public string? LocalBackupPath { get; set; }
        public int RemoteVersion { get; set; }
        public int LocalVersion { get; set; }
    }

    public enum SyncResult
    {
        Success,
        ConflictDetected,
        LocalNewer,
        RemoteNewer,
        NetworkError,
        DatabaseError,
        UserCancelled,
        UnknownError
    }
}