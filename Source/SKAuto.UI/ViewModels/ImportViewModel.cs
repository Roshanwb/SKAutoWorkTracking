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

        public IAsyncRelayCommand SelectFolderCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }

        public ImportViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
            ImportCommand = new AsyncRelayCommand(ImportAsync, () => PreviewOrders.Any() && !IsImporting);
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
                // Create a temporary working directory
                string tempDir = Path.Combine(Path.GetTempPath(), "SKAuto_PDF_Import_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                // Path to the Python script (assumed to be in the app's startup folder)
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdf_accessories_extractor.py");
                if (!File.Exists(scriptPath))
                {
                    // Fallback: look in a "Scripts" subfolder
                    scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "pdf_accessories_extractor.py");
                }

                if (!File.Exists(scriptPath))
                {
                    throw new FileNotFoundException("Python extractor script not found. Please ensure pdf_accessories_extractor.py is in the application folder.");
                }

                // Copy script to temp dir to avoid file locks and ensure output.txt is written there
                string tempScriptPath = Path.Combine(tempDir, "pdf_accessories_extractor.py");
                File.Copy(scriptPath, tempScriptPath, true);

                // Build arguments: folder path
                string args = $"\"{folder}\"";

                // Run Python script
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "python",  // assumes python is in PATH
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

                // Read the generated output.txt
                string outputFile = Path.Combine(tempDir, "output.txt");
                if (!File.Exists(outputFile))
                {
                    throw new Exception("Python script did not produce output.txt");
                }

                var lines = await File.ReadAllLinesAsync(outputFile);
                var orders = lines
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Split(','))
                    .Where(parts => parts.Length >= 4) // date,vin,model,client
                    .Select(parts => new PdfWorkOrder
                    {
                        OrderDate = ParseDate(parts[0]),
                        Chassis = parts[1].Trim(),
                        Model = parts[2].Trim(),
                        ClientName = parts[3].Trim()
                    })
                    .ToList();

                PreviewOrders = new ObservableCollection<PdfWorkOrder>(orders);
                StatusMessage = $"Found {orders.Count} work orders.";

                // Clean up temp directory (optional, can leave for debugging)
                // Directory.Delete(tempDir, true);
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
            // Python outputs as MM/DD/YYYY
            if (DateTime.TryParseExact(dateStr, "MM/dd/yyyy", null, System.Globalization.DateTimeStyles.None, out var date))
                return date;
            return DateTime.Today; // fallback
        }

        private async Task ImportAsync()
        {
            IsImporting = true;
            try
            {
                int created = 0;
                foreach (var item in PreviewOrders)
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
                            Type = ClientType.Direct, // default; can be adjusted later
                            IsActive = true
                        };
                        await _unitOfWork.Clients.AddAsync(client);
                    }
                    else if (client == null)
                    {
                        // Fallback to "Unknown" client
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

    // DTO for preview – matches Python output
    public class PdfWorkOrder
    {
        public string Chassis { get; set; } = "";
        public string Model { get; set; } = "";
        public string ClientName { get; set; } = "";
        public DateTime OrderDate { get; set; }
    }
}