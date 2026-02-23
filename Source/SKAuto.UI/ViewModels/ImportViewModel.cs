using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Import.Parsers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Import.Parsers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Import.Parsers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SKAuto.UI.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;

        [ObservableProperty]
        private ObservableCollection<ImportWorkOrderItem> _previewOrders = new();

        [ObservableProperty]
        private bool _isImporting;

        [ObservableProperty]
        private string _selectedFolder = "";

        [ObservableProperty]
        private string _statusMessage = "";

        [ObservableProperty]
        private bool _allSelected;

        public IAsyncRelayCommand SelectFolderCommand { get; }
        public IAsyncRelayCommand SelectExcelCommand { get; }
        public IRelayCommand ClearAllCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }
        public IRelayCommand SelectAllCommand { get; }
        public IAsyncRelayCommand SelectImportFileCommand { get; }

        public ImportViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
            SelectExcelCommand = new AsyncRelayCommand(SelectExcelAsync);
            ClearAllCommand = new RelayCommand(ClearAll);
            ImportCommand = new AsyncRelayCommand(ImportAsync, () => PreviewOrders.Any(x => x.IsSelected) && !IsImporting);
            SelectAllCommand = new RelayCommand(ToggleSelectAll);
            SelectImportFileCommand = new AsyncRelayCommand(SelectImportFileAsync);
        }

        private async Task SelectImportFileAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Excel or CSV file (ParcCarrières, export rdv)",
                Filter = "Supported files|*.xlsx;*.xls;*.xlsm;*.xltx;*.xltm;*.csv|Excel files|*.xlsx;*.xls;*.xlsm;*.xltx;*.xltm|CSV files|*.csv",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                await ProcessImportFileAsync(dialog.FileName);
            }
        }

        private async Task ProcessImportFileAsync(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            var excelExtensions = new[] { ".xlsx", ".xls", ".xlsm", ".xltx", ".xltm" };
            var csvExtensions = new[] { ".csv" };

            if (!excelExtensions.Contains(extension) && !csvExtensions.Contains(extension))
            {
                MessageBox.Show($"Unsupported file type: {extension}\nPlease select an Excel or CSV file.",
                    "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsImporting = true;
            StatusMessage = "Parsing file...";

            try
            {
                List<ImportWorkOrderDto> orders;

                if (excelExtensions.Contains(extension))
                {
                    var parser = new ExcelImportParser();
                    orders = parser.Parse(filePath);
                }
                else // CSV
                {
                    var parser = new CsvImportParser();
                    orders = parser.Parse(filePath);
                }

                AddOrders(orders);
                StatusMessage = $"Added {orders.Count} work orders from {Path.GetFileName(filePath)}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error parsing file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsImporting = false;
            }
        }

        private async Task SelectFolderAsync()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select folder containing PDF files"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedFolder = dialog.FolderName;
                await RunPythonExtractorAsync(SelectedFolder);
            }
        }

        private async Task RunPythonExtractorAsync(string folder)
        {
            IsImporting = true;
            StatusMessage = "Running Python extractor...";

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "SKAuto_PDF_Import_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdf_accessories_extractor.py");
                if (!File.Exists(scriptPath))
                {
                    scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "pdf_accessories_extractor.py");
                }

                if (!File.Exists(scriptPath))
                {
                    throw new FileNotFoundException("Python extractor script not found. Please ensure pdf_accessories_extractor.py is in the application folder.");
                }

                string tempScriptPath = Path.Combine(tempDir, "pdf_accessories_extractor.py");
                File.Copy(scriptPath, tempScriptPath, true);

                string args = $"\"{folder}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{tempScriptPath}\" {args}",
                        WorkingDirectory = tempDir,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Python script failed: {error}");
                }

                string outputFile = Path.Combine(tempDir, "output.txt");
                if (!File.Exists(outputFile))
                {
                    throw new Exception("Python script did not produce output.txt");
                }

                var lines = await File.ReadAllLinesAsync(outputFile);
                var orders = lines
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Split(','))
                    .Where(parts => parts.Length >= 4)
                    .Select(parts => new ImportWorkOrderDto
                    {
                        OrderDate = ParseDate(parts[0]),
                        Chassis = parts[1].Trim(),
                        Model = parts[2].Trim(),
                        ClientName = parts[3].Trim(),
                        Source = "PDF"
                    })
                    .ToList();

                AddOrders(orders);
                StatusMessage = $"Added {orders.Count} work orders from PDFs.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error running Python extractor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsImporting = false;
            }
        }

        private async Task SelectExcelAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Excel file (ParcCarrières)",
                Filter = "Excel files|*.xlsx;*.xls,*.csv",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                await ProcessExcelFileAsync(dialog.FileName);
            }
        }

        private async Task ProcessExcelFileAsync(string filePath)
        {
            // Validate file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var supported = new[] { ".xlsx", ".xls", ".xlsm", ".xltx", ".xltm" };
            if (!supported.Contains(extension))
            {
                MessageBox.Show($"Unsupported file type: {extension}\nPlease select an Excel file (.xlsx, .xls, .xlsm, .xltx, .xltm).",
                    "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsImporting = true;
            StatusMessage = "Parsing Excel file...";

            try
            {
                var parser = new ExcelImportParser();
                var orders = parser.Parse(filePath);
                AddOrders(orders);
                StatusMessage = $"Added {orders.Count} work orders from Excel.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error parsing Excel file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsImporting = false;
            }
        }

        private void AddOrders(List<ImportWorkOrderDto> newOrders)
        {
            foreach (var dto in newOrders)
            {
                var item = new ImportWorkOrderItem(dto);
                item.SelectionChanged += (s, e) => OnItemSelectionChanged();
                PreviewOrders.Add(item);
            }
            OnItemSelectionChanged();
        }

        private void ClearAll()
        {
            PreviewOrders.Clear();
            OnItemSelectionChanged();
            StatusMessage = "Cleared all items.";
        }

        private DateTime ParseDate(string dateStr)
        {
            if (DateTime.TryParseExact(dateStr, "MM/dd/yyyy", null, System.Globalization.DateTimeStyles.None, out var date))
                return date;
            return DateTime.Today;
        }

        private void ToggleSelectAll()
        {
            bool newState = !AllSelected;
            foreach (var item in PreviewOrders)
            {
                item.IsSelected = newState;
            }
            AllSelected = newState;
            ImportCommand.NotifyCanExecuteChanged();
        }

        partial void OnAllSelectedChanged(bool value)
        {
            if (PreviewOrders != null)
            {
                foreach (var item in PreviewOrders)
                {
                    item.IsSelected = value;
                }
                ImportCommand.NotifyCanExecuteChanged();
            }
        }

        private void OnItemSelectionChanged()
        {
            AllSelected = PreviewOrders.All(x => x.IsSelected);
            ImportCommand.NotifyCanExecuteChanged();
        }

        private async Task ImportAsync()
        {
            var selected = PreviewOrders.Where(x => x.IsSelected).ToList();
            if (!selected.Any()) return;

            IsImporting = true;
            try
            {
                int created = 0;
                foreach (var item in selected)
                {
                    var dto = item.Data;

                    // Ensure vehicle exists
                    var vehicle = (await _unitOfWork.Vehicles.FindAsync(v => v.ChassisNumber == dto.Chassis)).FirstOrDefault();
                    if (vehicle == null)
                    {
                        vehicle = new Vehicle
                        {
                            ChassisNumber = dto.Chassis,
                            Model = dto.Model,
                            IsActive = true
                        };
                        await _unitOfWork.Vehicles.AddAsync(vehicle);
                    }

                    // Ensure client exists
                    var client = (await _unitOfWork.Clients.FindAsync(c => c.Name == dto.ClientName)).FirstOrDefault();
                    if (client == null && !string.IsNullOrWhiteSpace(dto.ClientName))
                    {
                        client = new Client
                        {
                            Name = dto.ClientName,
                            Type = ClientType.Direct,
                            IsActive = true
                        };
                        await _unitOfWork.Clients.AddAsync(client);
                    }
                    else if (client == null)
                    {
                        client = (await _unitOfWork.Clients.FindAsync(c => c.Name == "Unknown")).FirstOrDefault();
                        if (client == null)
                        {
                            client = new Client { Name = "Unknown", Type = ClientType.Direct, IsActive = true };
                            await _unitOfWork.Clients.AddAsync(client);
                        }
                    }

                    var workOrder = new WorkOrder
                    {
                        ClientId = client.Id,
                        VehicleId = vehicle.Id,
                        OrderDate = dto.OrderDate,
                        Status = WorkStatus.Planned,
                        OrderType = OrderType.Direct_Fitting,
                        Notes = $"Imported from {dto.Source}"
                    };
                    await _unitOfWork.WorkOrders.AddAsync(workOrder);
                    created++;
                }

                await _unitOfWork.CompleteAsync();
                MessageBox.Show($"Successfully imported {created} work orders.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during import: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsImporting = false;
            }
        }

        private void CloseWindow(bool success = false)
        {
            foreach (Window window in Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.DialogResult = success;
                    window.Close();
                    break;
                }
        }
    }
}