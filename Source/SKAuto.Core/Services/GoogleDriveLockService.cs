using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SKAuto.Core.Interfaces;

namespace SKAuto.Core.Services
{
    public class GoogleDriveLockService : IDisposable
    {
        private readonly IGoogleDriveService _driveService;
        private readonly ILoggingService _logger;
        private readonly string _lockFileName = "app_lock.json";
        private readonly int _heartbeatIntervalSeconds = 120;
        private readonly int _staleLockTimeoutMinutes = 5;
        private CancellationTokenSource? _heartbeatCts;
        private Task? _heartbeatTask;
        private bool _isLockAcquired;
        private LockInfo? _currentLockInfo;

        public GoogleDriveLockService(IGoogleDriveService driveService, ILoggingService logger)
        {
            _driveService = driveService;
            _logger = logger;
        }

        public async Task<LockInfo> TryAcquireLockAsync(string machineName, string userName, bool forceUnlock = false)
        {
            try
            {
                _logger.LogInfo($"Attempting to acquire lock for {machineName} ({userName})");

                var lockFile = await _driveService.GetFileByNameAsync(_lockFileName);
                if (lockFile != null && !forceUnlock)
                {
                    var lockJson = await _driveService.DownloadFileContentAsync(lockFile.Id);
                    var existingLock = JsonSerializer.Deserialize<LockInfo>(lockJson);

                    if (existingLock != null && existingLock.LastHeartbeatUtc.HasValue)
                    {
                        var timeSinceHeartbeat = DateTime.UtcNow - existingLock.LastHeartbeatUtc.Value;
                        if (timeSinceHeartbeat.TotalMinutes < _staleLockTimeoutMinutes)
                        {
                            _logger.LogWarning($"Lock active by {existingLock.MachineName} ({existingLock.UserName}) - heartbeat {timeSinceHeartbeat.TotalMinutes:F1} min ago");
                            return new LockInfo
                            {
                                Result = LockResult.LockedByOtherUser,
                                MachineName = existingLock.MachineName,
                                UserName = existingLock.UserName,
                                SessionId = existingLock.SessionId,
                                LastHeartbeatUtc = existingLock.LastHeartbeatUtc
                            };
                        }
                        else
                        {
                            _logger.LogWarning($"Stale lock detected from {existingLock.MachineName} - last heartbeat {timeSinceHeartbeat.TotalMinutes:F1} min ago");
                            return new LockInfo
                            {
                                Result = LockResult.StaleLockDetected,
                                MachineName = existingLock.MachineName,
                                UserName = existingLock.UserName,
                                SessionId = existingLock.SessionId,
                                LastHeartbeatUtc = existingLock.LastHeartbeatUtc
                            };
                        }
                    }
                }

                var newLock = new LockInfo
                {
                    Result = LockResult.Success,
                    MachineName = machineName,
                    UserName = userName,
                    SessionId = Guid.NewGuid().ToString(),
                    LockedAtUtc = DateTime.UtcNow,
                    LastHeartbeatUtc = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(newLock);
                await _driveService.UploadFileContentAsync(_lockFileName, json);

                _currentLockInfo = newLock;
                _isLockAcquired = true;
                _logger.LogInfo($"Lock acquired successfully for {machineName} (Session: {newLock.SessionId})");

                return newLock;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to acquire cloud lock", ex);
                return new LockInfo { Result = LockResult.NetworkError };
            }
        }

        public void StartHeartbeat()
        {
            if (!_isLockAcquired || _heartbeatTask != null) return;

            _heartbeatCts = new CancellationTokenSource();
            _heartbeatTask = Task.Run(async () =>
            {
                while (!_heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_heartbeatIntervalSeconds), _heartbeatCts.Token);
                        await SendHeartbeatAsync();
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Heartbeat failed", ex);
                    }
                }
            }, _heartbeatCts.Token);
        }

        public async Task SendHeartbeatAsync()
        {
            if (!_isLockAcquired || _currentLockInfo == null) return;

            try
            {
                _currentLockInfo.LastHeartbeatUtc = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(_currentLockInfo);
                await _driveService.UploadFileContentAsync(_lockFileName, json);
                _logger.LogInfo($"Heartbeat sent for session {_currentLockInfo.SessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send heartbeat", ex);
            }
        }

        public async Task ReleaseLockAsync()
        {
            if (!_isLockAcquired) return;

            try
            {
                _heartbeatCts?.Cancel();
                if (_heartbeatTask != null)
                {
                    try { await _heartbeatTask; } catch { }
                    _heartbeatTask = null;
                }

                var lockFile = await _driveService.GetFileByNameAsync(_lockFileName);
                if (lockFile != null)
                {
                    await _driveService.DeleteFileAsync(lockFile.Id);
                    _logger.LogInfo($"Lock released for session {_currentLockInfo?.SessionId}");
                }

                _isLockAcquired = false;
                _currentLockInfo = null;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to release lock", ex);
            }
            finally
            {
                _heartbeatCts?.Dispose();
                _heartbeatCts = null;
            }
        }

        public void Dispose()
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts?.Dispose();
        }
    }

    public class LockInfo
    {
        public LockResult Result { get; set; }
        public string? MachineName { get; set; }
        public string? UserName { get; set; }
        public string? SessionId { get; set; }
        public DateTime? LockedAtUtc { get; set; }
        public DateTime? LastHeartbeatUtc { get; set; }
    }

    public enum LockResult
    {
        Success,
        LockedByOtherUser,
        StaleLockDetected,
        NetworkError,
        UnknownError
    }
}