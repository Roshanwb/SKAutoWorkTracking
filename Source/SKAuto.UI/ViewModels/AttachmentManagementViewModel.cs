using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.UI.Localization;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Cursor = System.Windows.Input.Cursor;
using Cursors = System.Windows.Input.Cursors;

namespace SKAuto.UI.ViewModels
{
    public partial class AttachmentManagementViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGoogleDriveService _driveService;
        private readonly ILoggingService _logger;
        private readonly int _workOrderId;

        [ObservableProperty]
        private ObservableCollection<SourceDocument> _attachments = new();

        [ObservableProperty]
        private SourceDocument? _selectedAttachment;

        [ObservableProperty]
        private bool _isBusy;

        // Manually implemented properties to avoid ambiguity
        private Cursor _cursor = Cursors.Arrow;
        public Cursor Cursor
        {
            get => _cursor;
            set => SetProperty(ref _cursor, value);
        }

        private bool _canClose = true;
        public bool CanClose
        {
            get => _canClose;
            set => SetProperty(ref _canClose, value);
        }

        public IAsyncRelayCommand LoadAttachmentsCommand { get; }
        public IAsyncRelayCommand AddAttachmentCommand { get; }
        public IAsyncRelayCommand<SourceDocument> RemoveAttachmentCommand { get; }
        public IAsyncRelayCommand<SourceDocument> DownloadAttachmentCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public AttachmentManagementViewModel(IUnitOfWork unitOfWork, IGoogleDriveService driveService, ILoggingService logger, int workOrderId)
        {
            _unitOfWork = unitOfWork ?? throw new System.ArgumentNullException(nameof(unitOfWork));
            _driveService = driveService ?? throw new System.ArgumentNullException(nameof(driveService));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _workOrderId = workOrderId;

            LoadAttachmentsCommand = new AsyncRelayCommand(LoadAttachmentsAsync);
            AddAttachmentCommand = new AsyncRelayCommand(AddAttachmentAsync);
            RemoveAttachmentCommand = new AsyncRelayCommand<SourceDocument>(RemoveAttachmentAsync);
            DownloadAttachmentCommand = new AsyncRelayCommand<SourceDocument>(DownloadAttachmentAsync);
            CloseCommand = new RelayCommand(() => CloseWindow(), () => CanClose);

            LoadAttachmentsCommand.Execute(null);
        }

        private async Task LoadAttachmentsAsync()
        {
            try
            {
                var docs = await _unitOfWork.SourceDocuments.FindAsync(d => d.WorkOrderId == _workOrderId);
                Attachments = new ObservableCollection<SourceDocument>(docs.OrderByDescending(d => d.UploadDate ?? System.DateTime.MinValue));
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to load attachments", ex);
                System.Windows.MessageBox.Show($"Error loading attachments: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddAttachmentAsync()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Title = "Select files to attach"
            };

            if (openFileDialog.ShowDialog() != true) return;

            IsBusy = true;
            CanClose = false;
            Cursor = System.Windows.Input.Cursors.Wait;

            try
            {
                var baseFolderId = await _driveService.GetFolderIdAsync("SKAuto Attachments");
                var workOrderFolderId = await _driveService.GetSubFolderIdAsync(baseFolderId, _workOrderId.ToString());

                foreach (var filePath in openFileDialog.FileNames)
                {
                    var fileName = System.IO.Path.GetFileName(filePath);
                    var fileId = await _driveService.UploadFileAsync(filePath, fileName, workOrderFolderId);

                    var doc = new SourceDocument
                    {
                        WorkOrderId = _workOrderId,
                        OriginalFilename = fileName,
                        GoogleDriveFileId = fileId,
                        FileHash = ComputeFileHash(filePath),
                        FileSize = new System.IO.FileInfo(filePath).Length,
                        UploadDate = System.DateTime.UtcNow,
                        ContentType = GetMimeType(filePath),
                        DocumentType = "Attachment"
                    };
                    await _unitOfWork.SourceDocuments.AddAsync(doc);
                }
                await _unitOfWork.CompleteAsync();
                await LoadAttachmentsAsync();
                System.Windows.MessageBox.Show($"Successfully added {openFileDialog.FileNames.Length} file(s).", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to add attachment", ex);
                System.Windows.MessageBox.Show($"Error adding attachment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                CanClose = true;
                Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private string ComputeFileHash(string filePath)
        {
            using var stream = System.IO.File.OpenRead(filePath);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(stream);
            return System.Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private string GetMimeType(string filePath)
        {
            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".txt" => "text/plain",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }

        private async Task RemoveAttachmentAsync(SourceDocument? doc)
        {
            if (doc == null) return;
            if (System.Windows.MessageBox.Show($"Delete attachment '{doc.OriginalFilename}'? This action cannot be undone.", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            IsBusy = true;
            CanClose = false;
            Cursor = System.Windows.Input.Cursors.Wait;

            try
            {
                if (!string.IsNullOrEmpty(doc.GoogleDriveFileId))
                    await _driveService.DeleteFileAsync(doc.GoogleDriveFileId);

                await _unitOfWork.SourceDocuments.DeleteAsync(doc);
                await _unitOfWork.CompleteAsync();
                await LoadAttachmentsAsync();
                _logger.LogInfo($"Deleted attachment {doc.OriginalFilename} (ID {doc.Id})");
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to delete attachment", ex);
                System.Windows.MessageBox.Show($"Error deleting attachment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                CanClose = true;
                Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private async Task DownloadAttachmentAsync(SourceDocument? doc)
        {
            if (doc == null) return;
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = doc.OriginalFilename,
                Filter = "All files|*.*"
            };
            if (saveDialog.ShowDialog() != true) return;

            IsBusy = true;
            CanClose = false;
            Cursor = System.Windows.Input.Cursors.Wait;

            try
            {
                await _driveService.DownloadFileAsync(doc.GoogleDriveFileId, saveDialog.FileName);
                System.Windows.MessageBox.Show($"File downloaded to {saveDialog.FileName}", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to download attachment", ex);
                System.Windows.MessageBox.Show($"Error downloading attachment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                CanClose = true;
                Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private void CloseWindow()
        {
            if (!CanClose) return;
            foreach (Window window in System.Windows.Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.DialogResult = true;
                    window.Close();
                    break;
                }
        }
    }
}