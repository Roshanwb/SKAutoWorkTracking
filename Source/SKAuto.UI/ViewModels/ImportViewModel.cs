using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Import.Parsers;
using SKAuto.Data.Repository;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace SKAuto.UI.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _loggingService;
        private readonly IServiceProvider _serviceProvider;

        // ---------- TASK CODE MAPPING ----------
        private static readonly Dictionary<string, TaskInfo> CodeToTaskInfo = new(StringComparer.OrdinalIgnoreCase)
        { 
            { "66", new TaskInfo("Nettoyage Préparation 66€", 66) },
            { "11", new TaskInfo("Relavage 11€", 11) },
            { "Relavage", new TaskInfo("Relavage 11€", 11) },      // non‑numeric variation
            { "Relavag", new TaskInfo("Relavage 11€", 11) },       // common misspelling
            { "68", new TaskInfo("Nettoyage Préparation 68€", 68) },
            // Add more codes as needed
        };
        private static readonly HashSet<string> KnownCodes = new(CodeToTaskInfo.Keys, StringComparer.OrdinalIgnoreCase);

        // ---------- CLIENT NAME NOISE WORDS ----------
        // Words that should be removed from client names (case‑insensitive).
        private static readonly HashSet<string> ExcludedWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "ok", "acc", "kit", "logos", "conforme", "pneus", "att.", "att. rv", "att rv",
            "66", "11", "68", "31", "10", "tapis", "relavage","relavag", "gravage", "pose", "camera",
            "ecran", "sk", "bois", "serrure", "cradel", "grille", "barre", "toit", "balisage",
            "alarme", "antivol", "crochet", "attelage", "boitier", "controle", "housse"
        };

        // Regular expression to detect codes like "TM4634", "AB123", "TS0025" – letters followed by digits.
        private static readonly Regex CodePattern = new Regex(@"^[A-Z]{2,}\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // Regular expression to detect purely numeric tokens (like "68", "31").
        private static readonly Regex NumericPattern = new Regex(@"^\d+$", RegexOptions.Compiled);
        // Regular expression to detect French header words (from Python script).
        private static readonly Regex HeaderWordsPattern = new Regex(
            @"^(heure|type|rapide|normale|site|livr|client|modèle|modele|vin)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly char[] SplitChars = new[] { ' ', '/', '\\', '-', '_', '(', ')', '[', ']', ',', ';' };
        // ----------------------------------------

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

        // ===== PDF FOLDER IMPORT =====
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
                    string output = process.StandardOutput.ReadToEnd();
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
                        .Select(p =>
                        {
                            // Parse date; if the original string is "Unknown", we treat it as no date.
                            string dateStr = p[0].Trim();
                            DateTime orderDate;
                            bool hasDate = DateTime.TryParseExact(dateStr, "MM/dd/yyyy", null,
                                            System.Globalization.DateTimeStyles.None, out orderDate);

                            return new ImportWorkOrderDto
                            {
                                OrderDate = hasDate ? orderDate : DateTime.Today, // fallback, but HasDate will be false
                                HasDate = hasDate,
                                Chassis = p[1].Trim(),
                                Model = p[2].Trim(),
                                ClientName = p[3].Trim(),
                                Source = "PDF"
                            };
                        })
                        .ToList();

                    progress.Report(new ProgressReport { Percent = 90, Operation = "Processing results..." });
                    return orders;
                });

                // Filter against database
                List<ImportWorkOrderDto> filteredOrders;
                using (var scope = _serviceProvider.CreateScope())
                {
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    filteredOrders = await Task.Run(async () =>
                    {
                        var result = new List<ImportWorkOrderDto>();
                        int total = parsedOrders.Count;
                        int processed = 0;
                        foreach (var dto in parsedOrders)
                        {
                            processed++;
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

        // ===== GENERAL CSV/EXCEL IMPORT =====
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

        // ===== PARCCARRIÈRES CSV IMPORT =====
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

                            var exists = (await unitOfWork.WorkOrders
                                .FindAsync(w => w.Vehicle.ChassisNumber == dto.Chassis && w.OrderDate == dto.OrderDate))
                                .Any();

                            if (!exists)
                                result.Add(dto);

                            if (processed % 50 == 0)
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

            // Sort the entire collection by OrderDate descending (latest first)
            var sorted = PreviewOrders.OrderByDescending(x => x.OrderDate).ToList();
            PreviewOrders.Clear();
            foreach (var item in sorted)
                PreviewOrders.Add(item);

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
                    orderItem.IsSelected = true;
            }
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

        // ---------- TASK CODE EXTRACTION ----------
        private string? ExtractCodeFromClient(string rawClientName, out string cleanedName)
        {
            cleanedName = rawClientName?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(rawClientName))
                return null;

            // Split by common delimiters
            var segments = rawClientName.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return null;

            // First pass: find known codes
            foreach (var seg in segments)
            {
                if (KnownCodes.Contains(seg))
                {
                    // Remove this segment and rebuild cleaned name
                    cleanedName = string.Join(" ", segments.Where(s => !string.Equals(s, seg, StringComparison.OrdinalIgnoreCase))).Trim();
                    return seg;
                }
            }
            return null;
        }

        // ---------- CLIENT NAME CLEANING (full version) ----------
        private string CleanClientName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "";

            // First, extract known task codes (they will be removed anyway)
            ExtractCodeFromClient(rawName, out string afterCodeRemoved);

            // Now apply noise removal on `afterCodeRemoved`
            var tokens = afterCodeRemoved.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            var filteredTokens = new List<string>();

            foreach (var token in tokens)
            {
                string t = token.Trim();

                // Skip if token is a known excluded word
                if (ExcludedWords.Contains(t))
                    continue;

                // Skip if token is purely numeric
                if (NumericPattern.IsMatch(t))
                    continue;

                // Skip if token looks like a code (letters followed by digits)
                if (CodePattern.IsMatch(t))
                    continue;

                // Skip if token is a French header word (from Python list)
                if (HeaderWordsPattern.IsMatch(t))
                    continue;

                // If token is too short (length 1) and not a letter (like "A", "X"), maybe keep? We'll keep single letters.
                // But we'll keep all non‑matched tokens.
                filteredTokens.Add(t);
            }

            // Join remaining tokens with space
            string cleaned = string.Join(" ", filteredTokens).Trim();

            // If after all cleaning we get an empty string, fall back to original? Better to return "Unknown"?
            // But we already have an "Unknown" client fallback later. So we can return empty, and later code will use "Unknown".
            // However, we must ensure we don't lose all info; maybe keep the longest token if empty.
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                // Fallback: take the longest token that is not noise? For now, return the original after code removal.
                cleaned = afterCodeRemoved;
            }

            return cleaned;
        }

        // Old helpers kept for compatibility (now using the new clean)
        private bool NeedsWashing(string clientName) => ExtractCodeFromClient(clientName, out _) == "66";

        // -----------------------------------------

        // ===== MAIN IMPORT (database commit) =====
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

                    // ---- 1. Collect distinct cleaned client names, chassis numbers, and all required codes ----
                    var clientNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var vinList = new HashSet<string>();
                    var requiredCodes = new HashSet<string>();

                    foreach (var item in selected)
                    {
                        var dto = item.Data;
                        string code = ExtractCodeFromClient(dto.ClientName, out string _);
                        string finalClientName = CleanClientName(dto.ClientName);
                        if (!string.IsNullOrWhiteSpace(finalClientName))
                            clientNames.Add(finalClientName);
                        vinList.Add(dto.Chassis);
                        if (code != null)
                            requiredCodes.Add(code);
                    }

                    // ---- 2. Fetch existing clients and vehicles ----
                    var allClients = await clientRepo.GetAllAsync();
                    var clientDict = new Dictionary<string, Client>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in allClients)
                        clientDict[c.Name] = c;

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
                            clientDict[name] = newClient;
                        }
                    }
                    if (newClients.Any())
                    {
                        await clientRepo.AddRangeAsync(newClients);
                        await unitOfWork.CompleteAsync(); // IDs assigned
                        foreach (var c in newClients)
                            clientDict[c.Name] = c;
                    }

                    // ---- 4. Ensure "Unknown" client exists ----
                    if (!clientDict.ContainsKey("Unknown"))
                    {
                        var unknown = new Client { Name = "Unknown", Type = ClientType.Direct, IsActive = true };
                        await clientRepo.AddAsync(unknown);
                        await unitOfWork.CompleteAsync();
                        clientDict["Unknown"] = unknown;
                    }

                    // ---- 5. Ensure all required accessories exist (store default price in accessory) ----
                    var accessoryIdByCode = new Dictionary<string, int>();
                    var accessoryDefaultPriceByCode = new Dictionary<string, decimal>();
                    foreach (var code in requiredCodes)
                    {
                        if (!CodeToTaskInfo.TryGetValue(code, out var taskInfo))
                        {
                            _loggingService.LogWarning($"Unknown code '{code}' – skipping accessory creation");
                            continue;
                        }

                        var accessory = (await unitOfWork.Accessories
                            .FindAsync(a => a.Name == taskInfo.Name))
                            .FirstOrDefault();
                        if (accessory == null)
                        {
                            accessory = new Accessory
                            {
                                Name = taskInfo.Name,
                                Price = taskInfo.DefaultPrice,   // store default price in accessory
                                Time = 30,
                                IsActive = true
                            };
                            await unitOfWork.Accessories.AddAsync(accessory);
                            await unitOfWork.CompleteAsync();
                            _loggingService.LogInfo($"Created accessory '{taskInfo.Name}' with default price {taskInfo.DefaultPrice}");
                        }
                        accessoryIdByCode[code] = accessory.Id;
                        accessoryDefaultPriceByCode[code] = taskInfo.DefaultPrice; // keep for fallback
                    }

                    // ---- 6. Create new vehicles (batch) – set ClientId ----
                    var newVehicles = new List<Vehicle>();
                    foreach (var vin in vinList)
                    {
                        if (!vehicleDict.ContainsKey(vin))
                        {
                            var firstDto = selected.First(x => x.Data.Chassis == vin).Data;
                            string cleanClient = CleanClientName(firstDto.ClientName);
                            if (!clientDict.TryGetValue(cleanClient, out var client))
                                client = clientDict["Unknown"];

                            var newVehicle = new Vehicle
                            {
                                ChassisNumber = vin,
                                Model = firstDto.Model,
                                ClientId = client.Id,
                                IsActive = true
                            };
                            newVehicles.Add(newVehicle);
                            vehicleDict[vin] = newVehicle;
                        }
                    }
                    if (newVehicles.Any())
                    {
                        await vehicleRepo.AddRangeAsync(newVehicles);
                        await unitOfWork.CompleteAsync(); // vehicles now have IDs
                    }

                    // ---- 7. Process each row: create work orders and capture code & price ----
                    var workOrdersToAdd = new List<WorkOrder>();
                    var perWorkOrderTaskInfo = new List<(int WorkOrderIndex, int AccessoryId, decimal Price)>();
                    int total = selected.Count;
                    int processed = 0;
                    int skipped = 0;

                    foreach (var item in selected)
                    {
                        processed++;
                        var dto = item.Data;
                        string code = ExtractCodeFromClient(dto.ClientName, out string _);

                        // Determine price from code
                        decimal? priceFromCode = null;
                        if (!string.IsNullOrEmpty(code))
                        {
                            if (decimal.TryParse(code, out decimal parsedPrice))
                            {
                                priceFromCode = parsedPrice;
                                _loggingService.LogInfo($"Code '{code}' parsed as numeric price {parsedPrice}");
                            }
                            else if (CodeToTaskInfo.TryGetValue(code, out var taskInfo))
                            {
                                priceFromCode = taskInfo.DefaultPrice;
                                _loggingService.LogInfo($"Code '{code}' mapped to task '{taskInfo.Name}' with default price {taskInfo.DefaultPrice}");
                            }
                            else
                            {
                                _loggingService.LogWarning($"Code '{code}' not found in mapping – no price will be assigned");
                            }
                        }

                        string cleanClient = CleanClientName(dto.ClientName);
                        var vehicle = vehicleDict[dto.Chassis];
                        var client = clientDict.TryGetValue(cleanClient, out var cli) ? cli : clientDict["Unknown"];

                        if (dto.HasDate)
                        {
                            var exists = (await workOrderRepo
                                .FindAsync(w => w.Vehicle.ChassisNumber == dto.Chassis && w.OrderDate == dto.OrderDate))
                                .Any();
                            if (exists)
                            {
                                _loggingService.LogWarning($"Skipped duplicate: {dto.Chassis} on {dto.OrderDate:yyyy-MM-dd}");
                                skipped++;
                                continue;
                            }

                            var tempworkStatus = dto.Source == "PDF" ? WorkStatus.Planned : WorkStatus.Done;
                            var workOrder = new WorkOrder
                            {
                                VehicleId = vehicle.Id,
                                OrderDate = dto.OrderDate,
                                Status = tempworkStatus,
                                CompletedDate = dto.OrderDate,
                                OrderType = OrderType.PSA_Contract,
                                Notes = $"Imported from {dto.Source}"
                            };
                            workOrdersToAdd.Add(workOrder);

                            // If we have a code and an accessory exists for it, prepare a task
                            if (code != null && accessoryIdByCode.TryGetValue(code, out int accessoryId))
                            {
                                // Use priceFromCode if available; otherwise fallback to accessory's default price (already stored)
                                decimal taskPrice = priceFromCode ?? accessoryDefaultPriceByCode[code];
                                perWorkOrderTaskInfo.Add((workOrdersToAdd.Count - 1, accessoryId, taskPrice));
                                _loggingService.LogInfo($"Preparing task for {dto.Chassis}: code '{code}', price {taskPrice}");
                            }
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

                    // ---- 9. Add tasks for prepared work orders ----
                    var tasksToAdd = new List<WorkTask>();
                    foreach (var (index, accessoryId, taskPrice) in perWorkOrderTaskInfo)
                    {
                        var workOrder = workOrdersToAdd[index];
                        tasksToAdd.Add(new WorkTask
                        {
                            WorkOrderId = workOrder.Id,
                            AccessoryId = accessoryId,
                            TaskType = TaskType.Preparation,
                            Quantity = 1,
                            TaskStatus = WorkStatus.Done,
                            Price = taskPrice
                        });
                    }

                    if (tasksToAdd.Any())
                    {
                        await workTaskRepo.AddRangeAsync(tasksToAdd);
                        // Update work order totals
                        foreach (var wo in workOrdersToAdd)
                        {
                            var woTasks = tasksToAdd.Where(t => t.WorkOrderId == wo.Id).ToList();
                            wo.TotalAmount = woTasks.Sum(t => t.Price * t.Quantity);
                        }
                        await unitOfWork.CompleteAsync();
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
        public class TaskInfo
        {
            public string Name { get; set; }
            public decimal DefaultPrice { get; set; }

            public TaskInfo(string name, decimal defaultPrice)
            {
                Name = name;
                DefaultPrice = defaultPrice;
            }
        }
    }
}