using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection; // for IServiceProvider
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Import.Parsers;
using SKAuto.UI.Converters;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using SKAuto.Data.Repository;   


namespace SKAuto.UI.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _loggingService;
        private readonly IServiceProvider _serviceProvider;

        private static string NormalizeChassis(string chassis) => chassis?.Trim().ToUpperInvariant() ?? "";
        private static string NormalizeClientName(string name) => name?.Trim() ?? "";

        [ObservableProperty]
        private ObservableCollection<ImportWorkOrderItem> _previewOrders = new();

        [ObservableProperty]
        private bool _isImporting;

        [ObservableProperty]
        private string _selectedFolder = "";

        [ObservableProperty]
        private string _statusMessage = "";

        [ObservableProperty]
        private bool _allSelected = false;

        [ObservableProperty]
        private int _importProgress;

        [ObservableProperty]
        private string _currentOperation = "";

        public IAsyncRelayCommand SelectFolderCommand { get; }
        public IAsyncRelayCommand SelectImportFileCommand { get; }
        public IAsyncRelayCommand SelectParcCarrieresCommand { get; }
        public IRelayCommand ClearAllCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }
        public IRelayCommand SelectAllCommand { get; }

        public ImportViewModel(IUnitOfWork unitOfWork, ILoggingService loggingService, IServiceProvider serviceProvider)
        {
            _unitOfWork = unitOfWork;
            _loggingService = loggingService;
            _serviceProvider = serviceProvider;

            SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
            SelectImportFileCommand = new AsyncRelayCommand(SelectImportFileAsync);
            SelectParcCarrieresCommand = new AsyncRelayCommand(SelectParcCarrieresAsync);
            ClearAllCommand = new RelayCommand(ClearAll);
            ImportCommand = new AsyncRelayCommand(ImportAsync, () => PreviewOrders.Any(x => x.IsSelected) && !IsImporting);
            SelectAllCommand = new RelayCommand(ToggleSelectAll);
        }

        // ===== PDF FOLDER IMPORT (unchanged, but runs Python in background) =====
        private async Task SelectFolderAsync()
        {
            var dialog = new OpenFolderDialog { Title = "Select folder containing PDF files" };
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
            ImportProgress = 0;
            CurrentOperation = "Starting Python script...";

            IProgress<ProgressReport> progress = new System.Progress<ProgressReport>(p =>
            {
                ImportProgress = p.Percent;
                CurrentOperation = p.Operation;
            });

            try
            {
                List<ImportWorkOrderDto> parsedOrders = await Task.Run(() =>
                {
                    progress.Report(new ProgressReport { Percent = 10, Operation = "Preparing temp directory..." });
                    string tempDir = Path.Combine(Path.GetTempPath(), "SKAuto_PDF_Import_" + Guid.NewGuid());
                    Directory.CreateDirectory(tempDir);

                    string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdf_accessories_extractor.py");
                    if (!File.Exists(scriptPath))
                        scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "pdf_accessories_extractor.py");

                    if (!File.Exists(scriptPath))
                        throw new FileNotFoundException("Python extractor script not found.");

                    string tempScriptPath = Path.Combine(tempDir, "pdf_accessories_extractor.py");
                    File.Copy(scriptPath, tempScriptPath, true);

                    string args = $"\"{folder}\"";
                    progress.Report(new ProgressReport { Percent = 30, Operation = "Running Python (may take a moment)..." });

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
                    string output = process.StandardOutput.ReadToEnd(); // synchronous read
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new Exception($"Python script failed: {error}");

                    string outputFile = Path.Combine(tempDir, "output.txt");
                    if (!File.Exists(outputFile))
                        throw new Exception("Python script did not produce output.txt");

                    var lines = File.ReadAllLines(outputFile);
                    var orders = lines
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => l.Split(','))
                        .Where(p => p.Length >= 4)
                        .Select(p => new ImportWorkOrderDto
                        {
                            OrderDate = ParseDate(p[0]),
                            Chassis = p[1].Trim(),
                            Model = p[2].Trim(),
                            ClientName = p[3].Trim(),
                            Source = "PDF"
                        })
                        .ToList();

                    progress.Report(new ProgressReport { Percent = 90, Operation = "Processing results..." });
                    return orders;
                });

                // Now filter against database (in background, with fresh scope)
                List<ImportWorkOrderDto> filteredOrders;
                using (var scope = _serviceProvider.CreateScope())
                {
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    filteredOrders = await Task.Run(async () =>   // <-- added 'async'
                    {
                        var result = new List<ImportWorkOrderDto>();
                        int total = parsedOrders.Count;
                        int processed = 0;
                        foreach (var dto in parsedOrders)
                        {
                            processed++;
                            // Normalize
                            dto.Chassis = NormalizeChassis(dto.Chassis);
                            dto.ClientName = NormalizeClientName(dto.ClientName);
                            dto.Model = dto.Model?.Trim() ?? "";

                            var exists = (await unitOfWork.WorkOrders
                                .FindAsync(w => w.Vehicle.ChassisNumber == dto.Chassis && w.OrderDate == dto.OrderDate))
                                .Any();
                            if (!exists)
                                result.Add(dto);

                            if (processed % 50 == 0)
                            {
                                int percent = 10 + (int)((double)processed / total * 80);
                                progress.Report(new ProgressReport { Percent = percent, Operation = $"Checked {processed}/{total}" });
                            }
                        }
                        return result;
                    });
                }

                await Application.Current.Dispatcher.InvokeAsync(() => AddOrders(filteredOrders));
                _loggingService.LogInfo($"PDF import: {filteredOrders.Count} new orders from {folder}");
                StatusMessage = $"Added {filteredOrders.Count} new work orders from PDFs.";
            }
            catch (Exception ex)
            {
                _loggingService.LogError("PDF import failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error running Python extractor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsImporting = false;
                ImportProgress = 0;
                CurrentOperation = "";
            }
        }

        // ===== GENERAL CSV/EXCEL IMPORT (exportrdv style) =====
        private async Task SelectImportFileAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Excel or CSV file (export rdv)",
                Filter = "Supported files|*.xlsx;*.xls;*.xlsm;*.xltx;*.xltm;*.csv|Excel files|*.xlsx;*.xls;*.xlsm;*.xltx;*.xltm|CSV files|*.csv",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
                await ProcessImportFileAsync(dialog.FileName);
        }

        private async Task ProcessImportFileAsync(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var excelExtensions = new[] { ".xlsx", ".xls", ".xlsm", ".xltx", ".xltm" };
            var csvExtensions = new[] { ".csv" };

            if (!excelExtensions.Contains(extension) && !csvExtensions.Contains(extension))
            {
                MessageBox.Show($"Unsupported file type: {extension}", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsImporting = true;
            StatusMessage = "Parsing file...";
            ImportProgress = 0;
            CurrentOperation = "Reading file...";

            IProgress<ProgressReport> progress = new Progress<ProgressReport>(p =>
            {
                ImportProgress = p.Percent;
                CurrentOperation = p.Operation;
            });

            try
            {
                List<ImportWorkOrderDto> orders = await Task.Run(() =>
                {
                    progress.Report(new ProgressReport { Percent = 10, Operation = "Parsing..." });
                    if (excelExtensions.Contains(extension))
                    {
                        var parser = new ExcelImportParser();
                        return parser.Parse(filePath);
                    }
                    else
                    {
                        var parser = new CsvImportParser();
                        return parser.Parse(filePath);
                    }
                });

                await Application.Current.Dispatcher.InvokeAsync(() => AddOrders(orders));
                _loggingService.LogInfo($"General import: {orders.Count} orders from {Path.GetFileName(filePath)}");
                StatusMessage = $"Added {orders.Count} work orders.";
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"General import failed: {filePath}", ex);
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error parsing file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsImporting = false;
                ImportProgress = 0;
                CurrentOperation = "";
            }
        }

        // ===== PARCCARRIÈRES CSV IMPORT (with duplicate check) =====
        private async Task SelectParcCarrieresAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select ParcCarrières CSV file",
                Filter = "CSV files|*.csv",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
                await ProcessParcCarrieresFileAsync(dialog.FileName);
        }

        private async Task ProcessParcCarrieresFileAsync(string filePath)
        {
            IsImporting = true;
            StatusMessage = "Processing ParcCarrières file...";
            ImportProgress = 0;
            CurrentOperation = "Reading file...";

            IProgress<ProgressReport> progress = new Progress<ProgressReport>(p =>
            {
                ImportProgress = p.Percent;
                CurrentOperation = p.Operation;
            });

            try
            {
                var cutoffDate = new DateTime(2025, 12, 31);
                List<ImportWorkOrderDto> filteredOrders;

                // Run heavy work in background with a fresh scope for EF
                using (var scope = _serviceProvider.CreateScope())
                {
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    filteredOrders = await Task.Run(async () =>
                    {
                        progress.Report(new ProgressReport { Percent = 10, Operation = "Parsing CSV..." });
                        var parser = new ParcCarrieresCsvParser();
                        var allOrders = parser.Parse(filePath);

                        progress.Report(new ProgressReport { Percent = 30, Operation = $"Filtering {allOrders.Count} rows..." });
                        var result = new List<ImportWorkOrderDto>();
                        int total = allOrders.Count;
                        int processed = 0;

                        foreach (var dto in allOrders)
                        {
                            processed++;
                            if (dto.OrderDate <= cutoffDate)
                                continue;

                            // Duplicate check (VIN + date)
                            var exists = (await unitOfWork.WorkOrders
                                .FindAsync(w => w.Vehicle.ChassisNumber == dto.Chassis && w.OrderDate == dto.OrderDate))
                                .Any();

                            if (!exists)
                                result.Add(dto);

                            if (processed % 50 == 0) // report every 50 rows
                            {
                                int percent = 30 + (int)((double)processed / total * 60);
                                progress.Report(new ProgressReport { Percent = percent, Operation = $"Checked {processed}/{total}" });
                            }
                        }
                        progress.Report(new ProgressReport { Percent = 90, Operation = "Finalizing..." });
                        return result;
                    });
                }

                await Application.Current.Dispatcher.InvokeAsync(() => AddOrders(filteredOrders));
                _loggingService.LogInfo($"ParcCarrières import: {filteredOrders.Count} new orders from {Path.GetFileName(filePath)}");
                StatusMessage = $"Added {filteredOrders.Count} new work orders from ParcCarrières.";
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"ParcCarrières import failed: {filePath}", ex);
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsImporting = false;
                ImportProgress = 0;
                CurrentOperation = "";
            }
        }

        // ===== COMMON METHODS =====
        private void AddOrders(List<ImportWorkOrderDto> newOrders)
        {
            foreach (var dto in newOrders)
            {
                dto.Chassis = NormalizeChassis(dto.Chassis);
                dto.ClientName = NormalizeClientName(dto.ClientName);
                dto.Model = dto.Model?.Trim() ?? "";

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
            _loggingService.LogInfo("Preview cleared by user.");
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
                item.IsSelected = newState;
            AllSelected = newState;
            ImportCommand.NotifyCanExecuteChanged();
        }
        public void MarkSelected(IList<object> selectedItems)
        {
            if (selectedItems == null) return;
            foreach (var item in selectedItems)
            {
                if (item is ImportWorkOrderItem orderItem)
                {
                    orderItem.IsSelected = true;
                }
            }
            // Update AllSelected and command state
            OnItemSelectionChanged();
        }

        partial void OnAllSelectedChanged(bool value)
        {
            if (PreviewOrders != null)
                foreach (var item in PreviewOrders)
                    item.IsSelected = value;
            ImportCommand.NotifyCanExecuteChanged();
        }

        private void OnItemSelectionChanged()
        {
            AllSelected = PreviewOrders.All(x => x.IsSelected);
            ImportCommand.NotifyCanExecuteChanged();
        }

        // ===== MAIN IMPORT (database commit) with background thread and progress =====
        private async Task ImportAsync()
        {
            var selected = PreviewOrders.Where(x => x.IsSelected).ToList();
            if (!selected.Any()) return;

            IsImporting = true;
            ImportProgress = 0;
            CurrentOperation = "Preparing import...";

            IProgress<ProgressReport> progress = new Progress<ProgressReport>(p =>
            {
                ImportProgress = p.Percent;
                CurrentOperation = p.Operation;
            });

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var vehicleRepo = (VehicleRepository)unitOfWork.Vehicles;
                    var clientRepo = (ClientRepository)unitOfWork.Clients;
                    var workOrderRepo = (WorkOrderRepository)unitOfWork.WorkOrders;
                    var workTaskRepo = (WorkTaskRepository)unitOfWork.WorkTasks;

                    // ---- 1. Collect distinct cleaned client names and chassis numbers ----
                    var clientNames = selected
                        .Select(x => CleanClientName(x.Data.ClientName))
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .ToList();

                    var vinList = selected
                        .Select(x => x.Data.Chassis)
                        .Distinct()
                        .ToList();

                    // ---- 2. Fetch existing clients and vehicles ----
                    var existingClients = await clientRepo.GetByNamesAsync(clientNames);
                    var clientDict = existingClients.ToDictionary(c => c.Name, c => c);

                    var existingVehicles = await vehicleRepo.GetByChassisNumbersAsync(vinList);
                    var vehicleDict = existingVehicles.ToDictionary(v => v.ChassisNumber, v => v);

                    // ---- 3. Create new clients (batch) ----
                    var newClients = new List<Client>();
                    foreach (var name in clientNames)
                    {
                        if (!clientDict.ContainsKey(name))
                        {
                            var newClient = new Client
                            {
                                Name = name,
                                Type = ClientType.Direct,
                                IsActive = true
                            };
                            newClients.Add(newClient);
                            clientDict[name] = newClient; // placeholder, will get real ID after save
                        }
                    }
                    if (newClients.Any())
                    {
                        await clientRepo.AddRangeAsync(newClients);
                        await unitOfWork.CompleteAsync(); // now IDs are assigned
                                                          // Update dictionary with the now‑assigned IDs
                        foreach (var c in newClients)
                            clientDict[c.Name] = c;
                    }

                    // ---- 4. Create new vehicles (batch) ----
                    var newVehicles = new List<Vehicle>();
                    foreach (var vin in vinList)
                    {
                        if (!vehicleDict.ContainsKey(vin))
                        {
                            // We need a model – we'll take the first occurrence's model
                            var firstDto = selected.First(x => x.Data.Chassis == vin).Data;
                            var newVehicle = new Vehicle
                            {
                                ChassisNumber = vin,
                                Model = firstDto.Model,
                                IsActive = true
                            };
                            newVehicles.Add(newVehicle);
                            vehicleDict[vin] = newVehicle;
                        }
                    }
                    if (newVehicles.Any())
                    {
                        await vehicleRepo.AddRangeAsync(newVehicles);
                        await unitOfWork.CompleteAsync();
                    }

                    // ---- 5. Ensure Unknown client exists (if needed) ----
                    if (!clientDict.ContainsKey("Unknown"))
                    {
                        var unknown = new Client { Name = "Unknown", Type = ClientType.Direct, IsActive = true };
                        await clientRepo.AddAsync(unknown);
                        await unitOfWork.CompleteAsync();
                        clientDict["Unknown"] = unknown;
                    }

                    // ---- 6. Check if any row needs washing and get/create accessory ----
                    bool anyNeedsWashing = selected.Any(x => NeedsWashing(x.Data.ClientName));
                    int washingAccessoryId = 0;
                    if (anyNeedsWashing)
                    {
                        var washing = (await unitOfWork.Accessories
                            .FindAsync(a => a.Name == "Nettoyage Préparation"))
                            .FirstOrDefault();
                        if (washing == null)
                        {
                            washing = new Accessory
                            {
                                Name = "Nettoyage Préparation",
                                PartNumber = "WASH001",
                                Description = "Washing and preparation service",
                                StandardFittingTime = 30,
                                PSAHourlyRate = 40,    
                                IsActive = true
                            };
                            await unitOfWork.Accessories.AddAsync(washing);
                            await unitOfWork.CompleteAsync();
                        }
                        washingAccessoryId = washing.Id;
                    }

                    // ---- 7. Process each row: create work orders and collect washing flags ----
                    var workOrdersToAdd = new List<WorkOrder>();
                    var needsWashingList = new List<bool>();
                    int total = selected.Count;
                    int processed = 0;
                    int skipped = 0;

                    foreach (var item in selected)
                    {
                        processed++;
                        var dto = item.Data;
                        string originalClient = dto.ClientName;
                        string cleanClient = CleanClientName(originalClient);
                        bool hasWashing = NeedsWashing(originalClient);

                        var vehicle = vehicleDict[dto.Chassis];
                        var client = clientDict.TryGetValue(cleanClient, out var cli) ? cli : clientDict["Unknown"];

                        // If we have a date, create work order (with duplicate check)
                        if (dto.HasDate)
                        {
                            // Duplicate check (VIN + date) – use the DB inside loop because we need per-row check
                            var exists = (await workOrderRepo
                                .FindAsync(w => w.Vehicle.ChassisNumber == dto.Chassis && w.OrderDate == dto.OrderDate))
                                .Any();
                            if (exists)
                            {
                                _loggingService.LogWarning($"Skipped duplicate: {dto.Chassis} on {dto.OrderDate:yyyy-MM-dd}");
                                skipped++;
                                continue;
                            }

                            var workOrder = new WorkOrder
                            {
                                ClientId = client.Id,
                                VehicleId = vehicle.Id,
                                OrderDate = dto.OrderDate,
                                Status = WorkStatus.Done,
                                CompletedDate = dto.OrderDate,
                                OrderType = OrderType.PSA_Contract,
                                Notes = $"Imported from {dto.Source}"
                            };
                            workOrdersToAdd.Add(workOrder);
                            needsWashingList.Add(hasWashing);
                            _loggingService.LogInfo($"Prepared WO for {dto.Chassis} on {dto.OrderDate:yyyy-MM-dd}");
                        }
                        else
                        {
                            _loggingService.LogInfo($"Vehicle-only record: {dto.Chassis} (client {cleanClient})");
                        }

                        int percent = (int)((double)processed / total * 100);
                        progress.Report(new ProgressReport
                        {
                            Percent = percent,
                            Operation = $"Processing {processed}/{total}: {dto.Chassis}"
                        });
                    }

                    // ---- 8. Save all work orders (batch) ----
                    if (workOrdersToAdd.Any())
                    {
                        await workOrderRepo.AddRangeAsync(workOrdersToAdd);
                        await unitOfWork.CompleteAsync(); // IDs assigned
                    }

                    // ---- 9. Add washing tasks (batch) ----
                    if (anyNeedsWashing && workOrdersToAdd.Any())
                    {
                        var washingAccessory = await unitOfWork.Accessories.GetByIdAsync(washingAccessoryId);
                        decimal? fittingPrice = washingAccessory?.PSAHourlyRate;

                        var tasksToAdd = new List<WorkTask>();
                        for (int i = 0; i < workOrdersToAdd.Count; i++)
                        {
                            if (needsWashingList[i])
                            {
                                tasksToAdd.Add(new WorkTask
                                {
                                    WorkOrderId = workOrdersToAdd[i].Id,
                                    AccessoryId = washingAccessoryId,
                                    TaskType = TaskType.Fit,
                                    Quantity = 1,
                                    TaskStatus = WorkStatus.Done,
                                    FittingPrice = fittingPrice    // price from accessory
                                });
                            }
                        }

                        if (tasksToAdd.Any())
                        {
                            await workTaskRepo.AddRangeAsync(tasksToAdd);
                            // Update each work order's total amount based on its tasks
                            foreach (var wo in workOrdersToAdd)
                            {
                                var woTasks = tasksToAdd.Where(t => t.WorkOrderId == wo.Id).ToList();
                                decimal woTotal = woTasks.Sum(t => (t.FittingPrice ?? 0) * t.Quantity);
                                wo.TotalAmount = woTotal;
                            }
                            await unitOfWork.CompleteAsync();      // save tasks and updated totals
                        }
                    }

                    int importedCount = workOrdersToAdd.Count;
                    int vehicleOnlyCount = selected.Count - importedCount - skipped;
                    _loggingService.LogInfo($"Import completed: added {importedCount} work orders, {newClients.Count} new clients, {newVehicles.Count} new vehicles.");
                    MessageBox.Show($"Successfully imported {importedCount} work orders.\n" +
                                    $"Vehicle‑only records: {vehicleOnlyCount}\n" +
                                    $"New clients: {newClients.Count}\n" +
                                    $"New vehicles: {newVehicles.Count}",
                        "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    CloseWindow(true);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Import failed", ex);
                MessageBox.Show($"Error during import: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsImporting = false;
                ImportProgress = 0;
                CurrentOperation = "";
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

        public class ProgressReport
        {
            public int Percent { get; set; }
            public string Operation { get; set; } = "";
        }
        private bool NeedsWashing(string clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName)) return false;
            var parts = clientName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 && parts.Last() == "66";
        }

        private string CleanClientName(string clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName)) return "";
            var parts = clientName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts.Last() == "66")
                return string.Join(" ", parts.Take(parts.Length - 1));
            return clientName.Trim();
        }

    }
}
