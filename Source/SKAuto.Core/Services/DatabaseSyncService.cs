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
        private const string DataFolderName = "SKAuto Data";
        private const string LegacyFolderName = "SKAuto Backups";

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

        // FIXED: now tries SKAuto Data first, then falls back to legacy SKAuto Backups.
        // Before overwriting the local DB, releases all pooled SQLite connections so the file lock is dropped.
        private async Task DownloadRemoteDatabaseAsync()
        {
            // 1. Locate the remote DB file
            var dbFile = await _driveService.GetFileByNameAsync("SKAuto.db");

            // 2. Legacy fallback – machines running the old code uploaded SKAuto.db here
            if (dbFile == null)
            {
                _logger.LogWarning($"SKAuto.db not found in '{DataFolderName}'. Checking legacy '{LegacyFolderName}' folder.");
                dbFile = await _driveService.GetFileByNameInFolderAsync("SKAuto.db", LegacyFolderName);
            }

            if (dbFile == null)
                throw new Exception(
                    $"Remote database file 'SKAuto.db' not found in '{DataFolderName}' or '{LegacyFolderName}'. " +
                    "Please ensure at least one computer has uploaded the database after applying the sync fix.");

            // 3. Backup the current local DB before overwriting
            if (File.Exists(_dbPath))
            {
                var backupPath = Path.Combine(_backupFolder, $"SKAuto_PRE_SYNC_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                File.Copy(_dbPath, backupPath, true);
                _logger.LogInfo($"Local DB backed up to {backupPath}");
            }

            // 4. Release all pooled SQLite connections + OS file handles before overwriting
            bool released = await ReleaseDatabaseFileAsync(_dbPath);
            if (!released)
            {
                throw new Exception(
                    $"Could not release the local database file at '{_dbPath}'. " +
                    "Please close any other running SKAuto instances and try again.");
            }

            // 5. Download the remote DB (now that the file is unlocked)
            await _driveService.DownloadFileAsync(dbFile.Id, _dbPath);
            _logger.LogInfo($"Remote database downloaded to {_dbPath} (from '{dbFile.Name}')");
        }

        /// <summary>
        /// Clears all pooled SQLite connections and forces GC to release file handles.
        /// Retries a few times to give the OS time to fully release the lock.
        /// </summary>
        private static async Task<bool> ReleaseDatabaseFileAsync(string dbPath, int maxAttempts = 10)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Force SQLite to drop all pooled connections
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(200);

                try
                {
                    if (!File.Exists(dbPath))
                        return true;

                    // Try to open exclusively – this succeeds only if nothing else holds a handle
                    using (var fs = new FileStream(dbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        // If we got here, the file is free
                        return true;
                    }
                }
                catch (IOException)
                {
                    // File still locked – retry
                }
                catch (UnauthorizedAccessException)
                {
                    // File still locked – retry
                }
            }
            return false;
        }

        private async Task UploadLocalDatabaseAsync()
        {
            SqliteConnection.ClearAllPools();
            await Task.Delay(100);
            await VacuumDatabaseAsync();
            SqliteConnection.ClearAllPools();
            await Task.Delay(100);

            await _driveService.UploadOrReplaceFileAsync(_dbPath, "SKAuto.db", DataFolderName);
            _logger.LogInfo($"Local database uploaded to Google Drive ({DataFolderName}/SKAuto.db)");
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

                _logger.LogInfo($"Remote version {remoteVersion} > local {result.LocalVersion} – downloading remote DB");
                await DownloadRemoteDatabaseAsync();
                await SetLocalVersionAsync(remoteVersion);
                result.LocalVersion = remoteVersion;

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