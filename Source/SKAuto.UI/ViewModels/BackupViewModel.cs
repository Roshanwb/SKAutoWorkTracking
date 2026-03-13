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

        // Conflict resolution properties
        [ObservableProperty]
        private ConflictResolution _clientConflict = ConflictResolution.Prompt;

        [ObservableProperty]
        private ConflictResolution _vehicleConflict = ConflictResolution.Prompt;

        [ObservableProperty]
        private ConflictResolution _accessoryConflict = ConflictResolution.Prompt;

        [ObservableProperty]
        private ConflictResolution _workOrderConflict = ConflictResolution.Prompt;

        [ObservableProperty]
        private ConflictResolution _workTaskConflict = ConflictResolution.Prompt;

        [ObservableProperty]
        private ConflictResolution _travelConflict = ConflictResolution.Prompt;

        [ObservableProperty]
        private ConflictResolution _protectedRateConflict = ConflictResolution.Prompt;

        [ObservableProperty]
        private ConflictResolution _userConflict = ConflictResolution.Prompt;

        [ObservableProperty]
        private ConflictResolution _sourceDocumentConflict = ConflictResolution.Prompt;

        
        [ObservableProperty]
        private string _statusMessage;
        [ObservableProperty]
        private BackupImportResult _lastImportResult;

        // Enum lists for binding
        public Array ConflictResolutions => Enum.GetValues(typeof(ConflictResolution));

        public IAsyncRelayCommand SelectBackupFolderCommand { get; }
        public IAsyncRelayCommand SelectExportFolderCommand { get; }
        public IAsyncRelayCommand SelectImportFileCommand { get; }
        public IAsyncRelayCommand BackupCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public BackupViewModel(IBackupService backupService, ILoggingService loggingService)
        {
            _backupService = backupService;
            _loggingService = loggingService;

            SelectBackupFolderCommand = new AsyncRelayCommand(SelectBackupFolderAsync);
            SelectExportFolderCommand = new AsyncRelayCommand(SelectExportFolderAsync);
            SelectImportFileCommand = new AsyncRelayCommand(SelectImportFileAsync);
            BackupCommand = new AsyncRelayCommand(BackupAsync);
            ExportCommand = new AsyncRelayCommand(ExportAsync);
            ImportCommand = new AsyncRelayCommand(ImportAsync);
            CloseCommand = new RelayCommand(CloseWindow);
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
                ClientConflict = ClientConflict,
                VehicleConflict = VehicleConflict,
                AccessoryConflict = AccessoryConflict,
                WorkOrderConflict = WorkOrderConflict,
                WorkTaskConflict = WorkTaskConflict,
                TravelConflict = TravelConflict,
                ProtectedRateConflict = ProtectedRateConflict,
                UserConflict = UserConflict,
                SourceDocumentConflict = SourceDocumentConflict
            };

            try
            {
                var result = await _backupService.ImportDataAsync(ImportFilePath, options);
                LastImportResult = result;

                if (result.Success)
                {
                    var msg = IsDryRun ? "Dry run completed." : "Import completed.";
                    msg += $"\nInserted: {result.RowsInserted}, Updated: {result.RowsUpdated}, Skipped: {result.RowsSkipped}";
                    if (result.Conflicts.Any())
                        msg += $"\nConflicts detected: {result.Conflicts.Count} (see log)";
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
    }
}