using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
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
        private ObservableCollection<PdfWorkOrder> _previewOrders = new();

        [ObservableProperty]
        private bool _isImporting;

        [ObservableProperty]
        private string _selectedFolder = "";

        [ObservableProperty]
        private string _statusMessage = "";

        [ObservableProperty]
        private bool _allSelected;

        public IAsyncRelayCommand SelectFolderCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }
        public IRelayCommand SelectAllCommand { get; }

        public ImportViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
            ImportCommand = new AsyncRelayCommand(ImportAsync, () => PreviewOrders.Any(x => x.IsSelected) && !IsImporting);
            SelectAllCommand = new RelayCommand(ToggleSelectAll);
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
                    .Select(parts => new PdfWorkOrder
                    {
                        OrderDate = ParseDate(parts[0]),
                        Chassis = parts[1].Trim(),
                        Model = parts[2].Trim(),
                        ClientName = parts[3].Trim(),
                        IsSelected = true // default to selected
                    })
                    .ToList();

                // Attach event handler to each order to notify parent when selection changes
                foreach (var order in orders)
                {
                    order.SelectionChanged += (s, e) => OnItemSelectionChanged();
                }

                PreviewOrders = new ObservableCollection<PdfWorkOrder>(orders);
                StatusMessage = $"Found {orders.Count} work orders.";
                AllSelected = true; // all selected by default
                ImportCommand.NotifyCanExecuteChanged();
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
            // When AllSelected changes via checkbox in UI, update all items
            if (PreviewOrders != null)
            {
                foreach (var item in PreviewOrders)
                {
                    item.IsSelected = value;
                }
                ImportCommand.NotifyCanExecuteChanged();
            }
        }

        // Called when any item's IsSelected changes
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
                    // Ensure vehicle exists
                    var vehicle = (await _unitOfWork.Vehicles.FindAsync(v => v.ChassisNumber == item.Chassis)).FirstOrDefault();
                    if (vehicle == null)
                    {
                        vehicle = new Vehicle
                        {
                            ChassisNumber = item.Chassis,
                            Model = item.Model,
                            IsActive = true
                        };
                        await _unitOfWork.Vehicles.AddAsync(vehicle);
                    }

                    // Ensure client exists
                    var client = (await _unitOfWork.Clients.FindAsync(c => c.Name == item.ClientName)).FirstOrDefault();
                    if (client == null && !string.IsNullOrWhiteSpace(item.ClientName))
                    {
                        client = new Client
                        {
                            Name = item.ClientName,
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
                        OrderDate = item.OrderDate,
                        Status = WorkStatus.Planned,
                        OrderType = OrderType.Direct_Fitting,
                        Notes = $"Imported from PDF"
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

    public class PdfWorkOrder : ObservableObject
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string Chassis { get; set; } = "";
        public string Model { get; set; } = "";
        public string ClientName { get; set; } = "";
        public DateTime OrderDate { get; set; }

        public event EventHandler? SelectionChanged;
    }
}