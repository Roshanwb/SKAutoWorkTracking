using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

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

        public IAsyncRelayCommand SelectBackupFolderCommand { get; }
        public IAsyncRelayCommand SelectExportFolderCommand { get; }
        public IAsyncRelayCommand SelectImportFileCommand { get; }
        public IAsyncRelayCommand BackupCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public IAsyncRelayCommand RefreshBackupsCommand { get; }
        public IAsyncRelayCommand RestoreSelectedBackupCommand { get; }

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

            _ = RefreshBackupsAsync();
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
            var dialog = new OpenFileDialog
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
                MessageBox.Show($"Backup saved to:\n{path}", "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshBackupsAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Backup failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExportAsync()
        {
            try
            {
                var path = await _backupService.ExportDataAsync(ExportFolder);
                StatusMessage = $"Export created: {path}";
                _loggingService.LogInfo($"Data export created at {path}");
                MessageBox.Show($"Export saved to:\n{path}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Export failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ImportAsync()
        {
            if (string.IsNullOrEmpty(ImportFilePath))
            {
                MessageBox.Show("Please select an import file first.", "No File", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show(msg, IsDryRun ? "Dry Run Result" : "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"Import failed: {result.ErrorMessage}";
                    _loggingService.LogError($"Import failed: {result.ErrorMessage}");
                    MessageBox.Show($"Import failed:\n{result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Import exception", ex);
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Import error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow()
        {
            foreach (Window w in Application.Current.Windows)
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
                MessageBox.Show($"Error listing backups: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Please select a backup file to restore.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Restore database from backup '{SelectedBackupFile.FileName}' (created {SelectedBackupFile.CreatedAt:dd/MM/yyyy HH:mm})?\n\nThis will replace the current database. A backup of the current database will be created automatically before restore.",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                StatusMessage = "Restoring database...";
                var currentBackupPath = await _backupService.RestoreDatabaseAsync(SelectedBackupFile.FilePath);
                StatusMessage = $"Database restored from {SelectedBackupFile.FileName}. Previous database backed up to {currentBackupPath}";
                _loggingService.LogInfo(StatusMessage);
                MessageBox.Show($"Restore successful.\n\nCurrent database before restore was backed up to:\n{currentBackupPath}", "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshBackupsAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Restore failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Restore failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        partial void OnSelectedBackupFileChanged(BackupFileInfo value)
        {
            RestoreSelectedBackupCommand.NotifyCanExecuteChanged();
        }
    }
}