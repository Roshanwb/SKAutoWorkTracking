using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.DTOs;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.UI.Localization;
using System.IO;
namespace SKAuto.UI.ViewModels
{
    public partial class GoogleDriveSettingsViewModel : ObservableObject
    {
        private readonly IGoogleDriveService _driveService;
        private readonly IConfigurationService _config;
        private readonly IBackupService _backupService;
        private readonly ILoggingService _logger;

        [ObservableProperty]
        private string _clientId;

        [ObservableProperty]
        private string _clientSecret;

        [ObservableProperty]
        private string _userEmail;

        [ObservableProperty]
        private string _folderId;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private DateTime? _lastSync;

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private bool _isAdmin;

        public IAsyncRelayCommand AuthenticateCommand { get; }
        public IAsyncRelayCommand TestConnectionCommand { get; }
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand SyncNowCommand { get; }
        public IAsyncRelayCommand BrowseFolderCommand { get; } // NEW

        public GoogleDriveSettingsViewModel(
            IGoogleDriveService driveService,
            IConfigurationService config,
            IBackupService backupService,
            ILoggingService logger)
        {
            _driveService = driveService;
            _config = config;
            _backupService = backupService;
            _logger = logger;

            // Set IsAdmin based on the logged-in user
            IsAdmin = App.CurrentUser?.Role == UserRole.Admin;

            Task.Run(async () => await LoadSettingsAsync());

            AuthenticateCommand = new AsyncRelayCommand(AuthenticateAsync);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, () => IsConnected);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            SyncNowCommand = new AsyncRelayCommand(SyncNowAsync, () => IsConnected);
            BrowseFolderCommand = new AsyncRelayCommand(BrowseFolderAsync);
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                var settings = await _config.GetAsync<GoogleDriveSettings>("GoogleDrive") ?? new GoogleDriveSettings();
                ClientId = settings.ClientId;
                ClientSecret = settings.ClientSecret;
                UserEmail = settings.UserEmail;
                FolderId = settings.FolderId;
                IsConnected = settings.IsConnected;
                LastSync = settings.LastSync;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading settings: {ex.Message}";
                _logger.LogError(LocalizationManager.Instance["LoadSettingsFailed"], ex);
            }
        }

        private async Task AuthenticateAsync()
        {
            var settings = new GoogleDriveSettings
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                UserEmail = UserEmail,
                FolderId = FolderId
            };
            IsConnected = await _driveService.AuthenticateAsync(settings);
            if (IsConnected)
            {
                StatusMessage = LocalizationManager.Instance["AuthenticationSuccessful"];
                await SaveSettingsAsync();
            }
            else
            {
                StatusMessage = LocalizationManager.Instance["AuthenticationFailed"];
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                if (await _driveService.TestConnectionAsync())
                    StatusMessage = LocalizationManager.Instance["ConnectionOK"];
                else
                    StatusMessage = LocalizationManager.Instance["ConnectionFailedUnknownReason"];
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(LocalizationManager.Instance["TestConnectionFailed"], ex);
                System.Windows.MessageBox.Show($"Connection test failed:\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task SaveSettingsAsync()
        {
            var settings = new GoogleDriveSettings
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                UserEmail = UserEmail,
                FolderId = FolderId,
                IsConnected = IsConnected,
                LastSync = LastSync
            };
            await _config.SetAsync("GoogleDrive", settings);
            StatusMessage = LocalizationManager.Instance["SettingsSaved"];
            _logger.LogInfo(LocalizationManager.Instance["GoogleDriveSettingsSaved"]);
        }

        private async Task SyncNowAsync()
        {
            if (!IsConnected)
            {
                StatusMessage = LocalizationManager.Instance["NotConnectedToGoogleDrive"];
                System.Windows.MessageBox.Show(LocalizationManager.Instance["PleaseAuthenticateWithGoogleDriveFirst"], "Not Connected", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                const string folderName = "SKAuto Backups";
                string folderId = await _driveService.GetFolderIdAsync(folderName);

                StatusMessage = LocalizationManager.Instance["CreatingBackup"];
                _logger.LogInfo(LocalizationManager.Instance["StartingManualSyncToGoogleDrive"]);

                var tempFolder = Path.Combine(Path.GetTempPath(), "SKAuto_Sync_" + Guid.NewGuid());
                Directory.CreateDirectory(tempFolder);

                var backupPath = await _backupService.ExportDataAsync(tempFolder);
                // Alternative: var backupPath = await _backupService.BackupDatabaseAsync(tempFolder);

                StatusMessage = LocalizationManager.Instance["UploadingToGoogleDrive"];

                string fileName = Path.GetFileName(backupPath);
                string fileId = await _driveService.UploadFileAsync(backupPath, fileName, folderId);

                LastSync = DateTime.Now;
                await SaveSettingsAsync();

                StatusMessage = $"Sync completed. File ID: {fileId}";
                _logger.LogInfo($"Sync successful: {fileName} uploaded to folder '{folderName}'");

                try { File.Delete(backupPath); } catch { }

                System.Windows.MessageBox.Show($"Backup successfully uploaded to Google Drive.\nFile: {fileName}", "Sync Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(LocalizationManager.Instance["SyncFailed"], ex);
                StatusMessage = $"Sync failed: {ex.Message}";
                System.Windows.MessageBox.Show($"Sync failed:\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // NEW: Browse folder command – finds or creates the default folder
        private async Task BrowseFolderAsync()
        {
            try
            {
                const string folderName = "SKAuto Backups";
                string folderId = await _driveService.GetFolderIdAsync(folderName);
                if (!string.IsNullOrEmpty(folderId))
                {
                    FolderId = folderId;
                    StatusMessage = $"Folder '{folderName}' selected (ID: {folderId})";
                    _logger.LogInfo($"Folder selected: {folderName} -> {folderId}");
                }
                else
                {
                    StatusMessage = LocalizationManager.Instance["CouldNotFindOrCreateFolder"];
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error browsing folder: {ex.Message}";
                _logger.LogError(LocalizationManager.Instance["BrowseFolderFailed"], ex);
                System.Windows.MessageBox.Show($"Error selecting folder:\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}