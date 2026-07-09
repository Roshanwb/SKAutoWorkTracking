using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Repository;
using SKAuto.Import.Parsers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using SKAuto.UI.Localization;
namespace SKAuto.UI.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _loggingService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _scopeFactory;

        // ---------- TASK CODE MAPPING ----------
        private static readonly Dictionary<string, TaskInfo> CodeToTaskInfo = new(StringComparer.OrdinalIgnoreCase)
        {
            { "66", new TaskInfo("Nettoyage Préparation 66€", 66) },
            { "11", new TaskInfo("Relavage 11€", 11) },
            { "Relavage", new TaskInfo("Relavage 11€", 11) },      // non-numeric variation
            { "Relavag", new TaskInfo("Relavage 11€", 11) },       // common misspelling
            { "68", new TaskInfo("Nettoyage Préparation 68€", 68) },
            // Add more codes as needed
        };
        private static readonly HashSet<string> KnownCodes = new(CodeToTaskInfo.Keys, StringComparer.OrdinalIgnoreCase);

        // ---------- CLIENT NAME NOISE WORDS ----------
        // Words that should be removed from client names (case-insensitive).
        private static readonly HashSet<string> ExcludedWords = new(StringComparer.OrdinalIgnoreCase)
        {
            LocalizationManager.Instance["ClientMergeDialog_OK"], "acc", "kit", "logos", "conforme", "pneus", "att.", "att. rv", "att rv",
            "66", "11", "68", "31", "10", "tapis", "relavage","relavag", "gravage", "pose", "camera",
            "ecran", "sk", "bois", "serrure", "cradel", "grille", "barre", "toit", "balisage",
            "alarme", "antivol", "crochet", "attelage", "boitier", "controle", "housse", "tea", "MQ", "ct", "X","JK","LAUTO","LAUTO*","TRANS","COMPET","COMPAGNE",
            "*","dr","le","atelier","Relavage","11Relavage","Nettoyage","Préparation"
        };
        // Company suffixes to remove (case-insensitive)
        private static readonly HashSet<string> CompanySuffixes = new(StringComparer.OrdinalIgnoreCase)
{
    "acb", "sarl", "sas", "eurl", "sa", "sasu", "sci", "snc", "scop", "selarl", "selas", "gmbh", "ltd", "inc"
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

        // NEW FILTER PROPERTIES
        [ObservableProperty]
        private bool _importWorkOrders = true;

        [ObservableProperty]
        private bool _importVehicles = true;

        [ObservableProperty]
        private bool _importClients = true;

        [ObservableProperty]
        private DateTime? _filterFromDate = null;

        [ObservableProperty]
        private DateTime? _filterToDate = null;

        public IAsyncRelayCommand SelectFolderCommand { get; }
        public IAsyncRelayCommand SelectImportFileCommand { get; }
        public IAsyncRelayCommand SelectParcCarrieresCommand { get; }
        public IRelayCommand ClearAllCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }
        public IRelayCommand SelectAllCommand { get; }

        public ImportViewModel(IUnitOfWork unitOfWork, ILoggingService loggingService, IServiceProvider serviceProvider, IServiceScopeFactory scopeFactory)
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
            _scopeFactory = scopeFactory;
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
            StatusMessage = LocalizationManager.Instance["RunningPythonExtractor"];
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
                        throw new Exception(LocalizationManager.Instance["PythonScriptDidNotProduceOutput"]);

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

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => AddOrders(filteredOrders));
                _loggingService.LogInfo($"PDF import: {filteredOrders.Count} new orders from {folder}");
                StatusMessage = $"Added {filteredOrders.Count} new work orders from PDFs.";
            }
            catch (Exception ex)
            {
                _loggingService.LogError(LocalizationManager.Instance["PDFImportFailed"], ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Error running Python extractor: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
            var dialog = new Microsoft.Win32.OpenFileDialog
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
                System.Windows.MessageBox.Show($"Unsupported file type: {extension}", "Invalid File", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            IsImporting = true;
            StatusMessage = LocalizationManager.Instance["ParsingFile"];
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

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => AddOrders(orders));
                _loggingService.LogInfo($"General import: {orders.Count} orders from {Path.GetFileName(filePath)}");
                StatusMessage = $"Added {orders.Count} work orders.";
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"General import failed: {filePath}", ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Error parsing file: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
            var dialog = new Microsoft.Win32.OpenFileDialog
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
            StatusMessage = LocalizationManager.Instance["ProcessingParcCarrieresFile"];
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

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => AddOrders(filteredOrders));
                _loggingService.LogInfo($"ParcCarrières import: {filteredOrders.Count} new orders from {Path.GetFileName(filePath)}");
                StatusMessage = $"Added {filteredOrders.Count} new work orders from ParcCarrières.";
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"ParcCarrières import failed: {filePath}", ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
            StatusMessage = LocalizationManager.Instance["ClearedAllItems"];
            _loggingService.LogInfo(LocalizationManager.Instance["PreviewClearedByUser"]);
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


        // ---------- CLIENT NAME CLEANING (enhanced) ----------
        private string CleanClientName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "Unknown";

            // 1. Extract and remove known task codes
            ExtractCodeFromClient(rawName, out string afterCodeRemoved);

            // 2. Remove all punctuation and special characters (before splitting)
            string noPunctuation = Regex.Replace(afterCodeRemoved, @"[^\p{L}\s\-']", " ");

            // 3. Split by whitespace and other delimiters
            var tokens = noPunctuation.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            var filteredTokens = new List<string>();

            foreach (var token in tokens)
            {
                string t = token.Trim();
                if (string.IsNullOrWhiteSpace(t))
                    continue;

                // --- NEW: Skip short uppercase tokens (2-3 letters) ---
                if (t.Length >= 2 && t.Length <= 3 && t.All(char.IsUpper))
                    continue;

                // Skip known noise words
                if (ExcludedWords.Contains(t))
                    continue;

                // Skip purely numeric tokens
                if (NumericPattern.IsMatch(t))
                    continue;

                // Skip codes like "TM4634"
                if (CodePattern.IsMatch(t))
                    continue;

                // Skip French header words
                if (HeaderWordsPattern.IsMatch(t))
                    continue;

                filteredTokens.Add(t);
            }

            if (!filteredTokens.Any())
                return "Unknown";

            // 4. Join remaining tokens with space
            string cleaned = string.Join(" ", filteredTokens).Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            // 5. Remove common company suffixes from the end (e.g., "Veolia ACB" -> "Veolia")
            var words = cleaned.Split(' ');
            if (words.Length > 1 && CompanySuffixes.Contains(words.Last()))
            {
                cleaned = string.Join(" ", words.Take(words.Length - 1));
            }

            if (string.IsNullOrWhiteSpace(cleaned))
                cleaned = "Unknown";

            // 6. Capitalize first letter of each word (Title Case)
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            cleaned = culture.TextInfo.ToTitleCase(cleaned.ToLower());

            // 7. Log transformation for debugging
            if (cleaned != rawName)
                _loggingService.LogInfo($"Client name cleaned: '{rawName}' -> '{cleaned}'");

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

            // Apply date filter
            var filteredSelected = selected.AsEnumerable();
            if (FilterFromDate.HasValue)
                filteredSelected = filteredSelected.Where(x => x.OrderDate >= FilterFromDate.Value);
            if (FilterToDate.HasValue)
                filteredSelected = filteredSelected.Where(x => x.OrderDate <= FilterToDate.Value);
            var selectedRows = filteredSelected.ToList();

            if (!selectedRows.Any())
            {
                System.Windows.MessageBox.Show(LocalizationManager.Instance["NoSelectedRowsMatchDateRange"], LocalizationManager.Instance["BackupView_Import"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // --- UI feedback start ---
            IsImporting = true;
            ImportProgress = 0;
            CurrentOperation = "Preparing import...";
            Mouse.OverrideCursor =  System.Windows.Input.Cursors.Wait;

            IProgress<ProgressReport> progress = new Progress<ProgressReport>(p =>
            {
                ImportProgress = p.Percent;
                CurrentOperation = p.Operation;
            });

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var vehicleRepo = (VehicleRepository)unitOfWork.Vehicles;
                    var clientRepo = (ClientRepository)unitOfWork.Clients;
                    var workOrderRepo = (WorkOrderRepository)unitOfWork.WorkOrders;
                    var workTaskRepo = (WorkTaskRepository)unitOfWork.WorkTasks;

                    // Step 1: Collect distinct data
                    progress.Report(new ProgressReport { Percent = 5, Operation = "Collecting data from selected rows..." });
                    var clientNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var vinList = new HashSet<string>();
                    var requiredCodes = new HashSet<string>();

                    foreach (var item in selectedRows)
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

                    progress.Report(new ProgressReport { Percent = 10, Operation = $"Found {clientNames.Count} clients, {vinList.Count} vehicles" });

                    // Step 2: Fetch existing clients
                    progress.Report(new ProgressReport { Percent = 20, Operation = "Fetching existing clients..." });
                    var allClients = await clientRepo.GetAllAsync();
                    var clientDict = new Dictionary<string, Client>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in allClients)
                        clientDict[c.Name] = c;

                    // Step 3: Fetch existing vehicles
                    progress.Report(new ProgressReport { Percent = 30, Operation = "Fetching existing vehicles..." });
                    var existingVehicles = await vehicleRepo.GetByChassisNumbersAsync(vinList);
                    var vehicleDict = existingVehicles.ToDictionary(v => v.ChassisNumber, v => v);

                    // Step 4: Create new clients (if enabled)
                    var newClients = new List<Client>();
                    if (ImportClients)
                    {
                        progress.Report(new ProgressReport { Percent = 40, Operation = "Creating new clients..." });
                        foreach (var name in clientNames)
                        {
                            if (!clientDict.ContainsKey(name))
                            {
                                var newClient = new Client
                                {
                                    Name = name,
                                    Type = ClientType.PSA,
                                    IsActive = true
                                };
                                newClients.Add(newClient);
                                clientDict[name] = newClient;
                            }
                        }
                        if (newClients.Any())
                        {
                            await clientRepo.AddRangeAsync(newClients);
                            await unitOfWork.CompleteAsync();
                            foreach (var c in newClients)
                                clientDict[c.Name] = c;
                        }

                        // Ensure "Unknown" client exists
                        if (!clientDict.ContainsKey("Unknown"))
                        {
                            var unknown = new Client { Name = "Unknown", Type = ClientType.PSA, IsActive = true };
                            await clientRepo.AddAsync(unknown);
                            await unitOfWork.CompleteAsync();
                            clientDict["Unknown"] = unknown;
                        }
                    }

                    // Step 5: Ensure accessories exist (if work orders)
                    var accessoryIdByCode = new Dictionary<string, int>();
                    var accessoryDefaultPriceByCode = new Dictionary<string, decimal>();
                    if (ImportWorkOrders && requiredCodes.Any())
                    {
                        progress.Report(new ProgressReport { Percent = 50, Operation = "Ensuring accessories exist..." });
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
                                    Price = taskInfo.DefaultPrice,
                                    Time = 30,
                                    IsActive = true
                                };
                                await unitOfWork.Accessories.AddAsync(accessory);
                                await unitOfWork.CompleteAsync();
                                _loggingService.LogInfo($"Created accessory '{taskInfo.Name}' with default price {taskInfo.DefaultPrice}");
                            }
                            accessoryIdByCode[code] = accessory.Id;
                            accessoryDefaultPriceByCode[code] = taskInfo.DefaultPrice;
                        }
                    }

                    // Step 6: Create new vehicles (if enabled)
                    var newVehicles = new List<Vehicle>();
                    if (ImportVehicles)
                    {
                        progress.Report(new ProgressReport { Percent = 60, Operation = "Creating new vehicles..." });
                        foreach (var vin in vinList)
                        {
                            if (!vehicleDict.ContainsKey(vin))
                            {
                                var firstDto = selectedRows.First(x => x.Data.Chassis == vin).Data;
                                string cleanClient = CleanClientName(firstDto.ClientName);
                                if (!clientDict.TryGetValue(cleanClient, out var client))
                                    client = clientDict.GetValueOrDefault("Unknown");

                                var newVehicle = new Vehicle
                                {
                                    ChassisNumber = vin,
                                    Model = firstDto.Model,
                                    ClientId = client?.Id ?? 0,
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
                    }

                    // Step 7: Process work orders (with progress in loop)
                    var workOrdersToAdd = new List<WorkOrder>();
                    var perWorkOrderTaskInfo = new List<(int WorkOrderIndex, int AccessoryId, decimal Price)>();
                    int total = selectedRows.Count;
                    int processed = 0;
                    int skipped = 0;

                    if (ImportWorkOrders)
                    {
                        progress.Report(new ProgressReport { Percent = 70, Operation = $"Processing work orders (0/{total})..." });
                        foreach (var item in selectedRows)
                        {
                            processed++;
                            var dto = item.Data;
                            string code = ExtractCodeFromClient(dto.ClientName, out string _);
                            decimal? priceFromCode = null;
                            if (!string.IsNullOrEmpty(code))
                            {
                                if (decimal.TryParse(code, out decimal parsedPrice))
                                    priceFromCode = parsedPrice;
                                else if (CodeToTaskInfo.TryGetValue(code, out var taskInfo))
                                    priceFromCode = taskInfo.DefaultPrice;
                                else
                                    _loggingService.LogWarning($"Code '{code}' not found in mapping");
                            }

                            string cleanClient = CleanClientName(dto.ClientName);
                            var vehicle = vehicleDict.GetValueOrDefault(dto.Chassis);
                            if (vehicle == null && ImportVehicles)
                            {
                                _loggingService.LogWarning($"Vehicle {dto.Chassis} not found and vehicle import disabled – skipping work order");
                                skipped++;
                                continue;
                            }

                            if (dto.HasDate && ImportWorkOrders && vehicle != null)
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

                                var workOrder = new WorkOrder
                                {
                                    VehicleId = vehicle.Id,
                                    OrderDate = dto.OrderDate,
                                    Status = dto.Source == "PDF" ? WorkStatus.Planned : WorkStatus.Done,
                                    CompletedDate = dto.OrderDate,
                                    OrderType = OrderType.PSA_Contract,
                                    Notes = $"Imported from {dto.Source}"
                                };
                                workOrdersToAdd.Add(workOrder);

                                if (code != null && accessoryIdByCode.TryGetValue(code, out int accessoryId))
                                {
                                    decimal taskPrice = priceFromCode ?? accessoryDefaultPriceByCode[code];
                                    perWorkOrderTaskInfo.Add((workOrdersToAdd.Count - 1, accessoryId, taskPrice));
                                    _loggingService.LogInfo($"Preparing task for {dto.Chassis}: code '{code}', price {taskPrice}");
                                }
                            }
                            else if (!ImportWorkOrders)
                            {
                                _loggingService.LogInfo($"Work order creation disabled – skipping {dto.Chassis}");
                            }

                            // Update progress for the loop (70% to 95%)
                            int percent = 70 + (int)((double)processed / total * 25);
                            if (percent > 95) percent = 95;
                            progress.Report(new ProgressReport { Percent = percent, Operation = $"Processing work orders {processed}/{total}: {dto.Chassis}" });
                        }
                    }

                    // Step 8: Save work orders and tasks
                    if (workOrdersToAdd.Any())
                    {
                        progress.Report(new ProgressReport { Percent = 96, Operation = "Saving work orders..." });
                        await workOrderRepo.AddRangeAsync(workOrdersToAdd);
                        await unitOfWork.CompleteAsync();
                    }

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
                        progress.Report(new ProgressReport { Percent = 98, Operation = "Saving tasks..." });
                        await workTaskRepo.AddRangeAsync(tasksToAdd);
                        foreach (var wo in workOrdersToAdd)
                        {
                            var woTasks = tasksToAdd.Where(t => t.WorkOrderId == wo.Id).ToList();
                            wo.TotalAmount = woTasks.Sum(t => t.Price * t.Quantity);
                        }
                        await unitOfWork.CompleteAsync();
                    }

                    // Final progress
                    progress.Report(new ProgressReport { Percent = 100, Operation = "Import complete!" });

                    int importedCount = workOrdersToAdd.Count;
                    int vehicleOnlyCount = selectedRows.Count - importedCount - skipped;
                    _loggingService.LogInfo($"Import completed: added {importedCount} work orders, {newClients.Count} new clients, {newVehicles.Count} new vehicles.");
                    System.Windows.MessageBox.Show($"Successfully imported {importedCount} work orders.\n" +
                                    $"Vehicle-only records: {vehicleOnlyCount}\n" +
                                    $"New clients: {newClients.Count}\n" +
                                    $"New vehicles: {newVehicles.Count}",
                        "Import Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    CloseWindow(true);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(LocalizationManager.Instance["ImportFailed"], ex);
                System.Windows.MessageBox.Show($"Error during import: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsImporting = false;
                ImportProgress = 0;
                CurrentOperation = "";
                Mouse.OverrideCursor = null;
            }
        }

        private void CloseWindow(bool success = false)
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
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