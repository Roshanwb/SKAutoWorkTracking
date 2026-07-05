using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SKAuto.UI.ViewModels
{
    public partial class BackupViewModel : ObservableObject
    {
        private readonly IBackupService _backupService;
        private readonly ILoggingService _loggingService;

        [ObservableProperty]
        private string _backupFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        [ObservableProperty]
        private string _exportFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        [ObservableProperty]
        private string _importFilePath;

        [ObservableProperty]
        private bool _isDryRun = true;

        [ObservableProperty]
        private ConflictResolution _conflict = ConflictResolution.Skip;

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private BackupImportResult _lastImportResult;

        [ObservableProperty]
        private ObservableCollection<BackupFileInfo> _backupFiles = new();

        [ObservableProperty]
        private BackupFileInfo _selectedBackupFile;

        [ObservableProperty]
        private bool _isRefreshingBackups;

        // Drive properties
        [ObservableProperty]
        private ObservableCollection<BackupFileInfo> _driveBackupFiles = new();

        [ObservableProperty]
        private BackupFileInfo _selectedDriveBackupFile;

        [ObservableProperty]
        private bool _isRefreshingDriveBackups;

        [ObservableProperty]
        private bool _isDriveConnected;

        // NEW: Busy indicator
        [ObservableProperty]
        private bool _isBusy;

        public IAsyncRelayCommand SelectBackupFolderCommand { get; }
        public IAsyncRelayCommand SelectExportFolderCommand { get; }
        public IAsyncRelayCommand SelectImportFileCommand { get; }
        public IAsyncRelayCommand BackupCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public IAsyncRelayCommand RefreshBackupsCommand { get; }
        public IAsyncRelayCommand RestoreSelectedBackupCommand { get; }

        // Drive commands
        public IAsyncRelayCommand RefreshDriveBackupsCommand { get; }
        public IAsyncRelayCommand RestoreSelectedDriveBackupCommand { get; }

        private string _backupsListFolder;

        public BackupViewModel(IBackupService backupService, ILoggingService loggingService)
        {
            _backupService = backupService;
            _loggingService = loggingService;

            _backupsListFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SKAuto",
                "Backups");

            SelectBackupFolderCommand = new AsyncRelayCommand(SelectBackupFolderAsync);
            SelectExportFolderCommand = new AsyncRelayCommand(SelectExportFolderAsync);
            SelectImportFileCommand = new AsyncRelayCommand(SelectImportFileAsync);
            BackupCommand = new AsyncRelayCommand(BackupAsync);
            ExportCommand = new AsyncRelayCommand(ExportAsync);
            ImportCommand = new AsyncRelayCommand(ImportAsync);
            CloseCommand = new RelayCommand(() => CloseWindow());

            RefreshBackupsCommand = new AsyncRelayCommand(RefreshBackupsAsync);
            RestoreSelectedBackupCommand = new AsyncRelayCommand(RestoreSelectedBackupAsync, () => SelectedBackupFile != null);

            RefreshDriveBackupsCommand = new AsyncRelayCommand(RefreshDriveBackupsAsync);
            RestoreSelectedDriveBackupCommand = new AsyncRelayCommand(RestoreSelectedDriveBackupAsync, () => SelectedDriveBackupFile != null && IsDriveConnected);

            _ = RefreshBackupsAsync();
        }

        private async Task RefreshDriveBackupsAsync()
        {
            if (IsRefreshingDriveBackups) return;
            IsRefreshingDriveBackups = true;
            try
            {
                var driveService = App.GetService<IGoogleDriveService>();
                IsDriveConnected = await driveService.TestConnectionAsync();
                if (!IsDriveConnected)
                {
                    StatusMessage = "Google Drive not connected. Please authenticate first.";
                    return;
                }
                var files = await driveService.ListDriveBackupsAsync();
                DriveBackupFiles = new ObservableCollection<BackupFileInfo>(files);
                if (DriveBackupFiles.Count > 0)
                    SelectedDriveBackupFile = DriveBackupFiles[0];
                StatusMessage = $"Found {DriveBackupFiles.Count} backup files in Google Drive.";
                _loggingService.LogInfo(StatusMessage);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to list Drive backups", ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Error listing Drive backups: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsRefreshingDriveBackups = false;
                RestoreSelectedDriveBackupCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task RestoreSelectedDriveBackupAsync()
        {
            if (SelectedDriveBackupFile == null)
            {
                System.Windows.MessageBox.Show("Please select a Drive backup to restore.", "No Selection", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Restore database from Drive backup '{SelectedDriveBackupFile.FileName}' (created {SelectedDriveBackupFile.CreatedAt:dd/MM/yyyy HH:mm})?\n\n" +
                "This will:\n1. Create a local backup of the current database.\n2. Download the Drive backup.\n3. Restore the downloaded backup.",
                "Confirm Restore from Drive",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            IsBusy = true;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            StatusMessage = "Step 1: Creating local backup...";

            try
            {
                // Step 1: Create local backup in the standard backup folder
                string backupFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SKAuto",
                    "Backups");
                var localBackupPath = await _backupService.BackupDatabaseAsync(backupFolder);
                _loggingService.LogInfo($"Local backup created before Drive restore: {localBackupPath}");
                StatusMessage = $"Local backup saved to: {localBackupPath}";

                // Step 2: Download Drive backup to a temporary folder
                StatusMessage = "Step 2: Downloading Drive backup...";
                var driveService = App.GetService<IGoogleDriveService>();
                string driveFileId = SelectedDriveBackupFile.FilePath;
                string downloadedPath = await driveService.DownloadDriveBackupAsync(driveFileId);
                _loggingService.LogInfo($"Drive backup downloaded to: {downloadedPath}");

                // Step 3: Import the downloaded ZIP
                StatusMessage = "Step 3: Importing backup...";
                var options = new BackupImportOptions
                {
                    DryRun = IsDryRun,
                    Conflict = Conflict
                };
                var importResult = await _backupService.ImportDataAsync(downloadedPath, options);

                if (importResult.Success)
                {
                    StatusMessage = $"Database restored from Drive backup '{SelectedDriveBackupFile.FileName}'.\n" +
                                    $"Inserted: {importResult.RowsInserted}, Updated: {importResult.RowsUpdated}, Skipped: {importResult.RowsSkipped}";
                    _loggingService.LogInfo(StatusMessage);
                    System.Windows.MessageBox.Show($"Restore successful.\n\nLocal backup created before restore:\n{localBackupPath}", "Restore Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"Restore failed: {importResult.ErrorMessage}";
                    _loggingService.LogError(StatusMessage);
                    System.Windows.MessageBox.Show($"Restore failed:\n{importResult.ErrorMessage}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }

                // Clean up downloaded file
                try { File.Delete(downloadedPath); } catch { }

                // Refresh the Drive backup list
                await RefreshDriveBackupsAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Restore from Drive failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Restore from Drive failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                Mouse.OverrideCursor = null;
                RestoreSelectedDriveBackupCommand.NotifyCanExecuteChanged();
            }
        }

        partial void OnSelectedDriveBackupFileChanged(BackupFileInfo value)
        {
            RestoreSelectedDriveBackupCommand.NotifyCanExecuteChanged();
        }

        private async Task SelectBackupFolderAsync()
        {
            var dialog = new OpenFolderDialog { Title = "Select backup folder" };
            if (dialog.ShowDialog() == true)
                BackupFolder = dialog.FolderName;
            await Task.CompletedTask;
        }

        private async Task SelectExportFolderAsync()
        {
            var dialog = new OpenFolderDialog { Title = "Select export folder" };
            if (dialog.ShowDialog() == true)
                ExportFolder = dialog.FolderName;
            await Task.CompletedTask;
        }

        private async Task SelectImportFileAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select import zip file",
                Filter = "Zip files|*.zip"
            };
            if (dialog.ShowDialog() == true)
                ImportFilePath = dialog.FileName;
            await Task.CompletedTask;
        }

        private async Task BackupAsync()
        {
            try
            {
                var path = await _backupService.BackupDatabaseAsync(BackupFolder);
                StatusMessage = $"Backup created: {path}";
                _loggingService.LogInfo($"Database backup created at {path}");
                System.Windows.MessageBox.Show($"Backup saved to:\n{path}", "Backup Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                await RefreshBackupsAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Backup failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Backup failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task ExportAsync()
        {
            try
            {
                var path = await _backupService.ExportDataAsync(ExportFolder);
                StatusMessage = $"Export created: {path}";
                _loggingService.LogInfo($"Data export created at {path}");
                System.Windows.MessageBox.Show($"Export saved to:\n{path}", "Export Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Export failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task ImportAsync()
        {
            if (string.IsNullOrEmpty(ImportFilePath))
            {
                System.Windows.MessageBox.Show("Please select an import file first.", "No File", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var options = new BackupImportOptions
            {
                DryRun = IsDryRun,
                Conflict = Conflict
            };

            try
            {
                var result = await _backupService.ImportDataAsync(ImportFilePath, options);
                LastImportResult = result;

                if (result.Success)
                {
                    var msg = IsDryRun ? "Dry run completed." : "Import completed.";
                    msg += $"\nInserted: {result.RowsInserted}, Updated: {result.RowsUpdated}, Skipped: {result.RowsSkipped}";
                    if (result.Conflicts.Count > 0)
                        msg += $"\nConflicts: {result.Conflicts.Count} (see log)";
                    StatusMessage = msg;
                    _loggingService.LogInfo(msg);
                    System.Windows.MessageBox.Show(msg, IsDryRun ? "Dry Run Result" : "Import Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"Import failed: {result.ErrorMessage}";
                    _loggingService.LogError($"Import failed: {result.ErrorMessage}");
                    System.Windows.MessageBox.Show($"Import failed:\n{result.ErrorMessage}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Import exception", ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Import error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void CloseWindow()
        {
            foreach (Window w in System.Windows.Application.Current.Windows)
                if (w.DataContext == this)
                {
                    w.Close();
                    break;
                }
        }

        private async Task RefreshBackupsAsync()
        {
            if (IsRefreshingBackups) return;
            IsRefreshingBackups = true;
            try
            {
                var files = await _backupService.GetBackupFilesAsync(_backupsListFolder);
                BackupFiles = new ObservableCollection<BackupFileInfo>(files);
                if (BackupFiles.Count > 0)
                    SelectedBackupFile = BackupFiles[0];
                StatusMessage = $"Found {BackupFiles.Count} backup files in {_backupsListFolder}";
                _loggingService.LogInfo(StatusMessage);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to list backup files", ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Error listing backups: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsRefreshingBackups = false;
                RestoreSelectedBackupCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task RestoreSelectedBackupAsync()
        {
            if (SelectedBackupFile == null)
            {
                System.Windows.MessageBox.Show("Please select a backup file to restore.", "No Selection", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Restore database from backup '{SelectedBackupFile.FileName}' (created {SelectedBackupFile.CreatedAt:dd/MM/yyyy HH:mm})?\n\nThis will replace the current database. A backup of the current database will be created automatically before restore.",
                "Confirm Restore",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                StatusMessage = "Restoring database...";
                var currentBackupPath = await _backupService.RestoreDatabaseAsync(SelectedBackupFile.FilePath);
                StatusMessage = $"Database restored from {SelectedBackupFile.FileName}. Previous database backed up to {currentBackupPath}";
                _loggingService.LogInfo(StatusMessage);
                System.Windows.MessageBox.Show($"Restore successful.\n\nCurrent database before restore was backed up to:\n{currentBackupPath}", "Restore Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                await RefreshBackupsAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Restore failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Restore failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        partial void OnSelectedBackupFileChanged(BackupFileInfo value)
        {
            RestoreSelectedBackupCommand.NotifyCanExecuteChanged();
        }
    }
}