using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Repository;
using SKAuto.Import.Parsers;
using SKAuto.UI.Localization;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
namespace SKAuto.UI.ViewModels
{
    public partial class VehicleManagementViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;

        [ObservableProperty]
        private ObservableCollection<Vehicle> _vehicles = new();

        [ObservableProperty]
        private Vehicle? _selectedVehicle;

        [ObservableProperty]
        private string _searchText = "";

        [ObservableProperty]
        private ObservableCollection<Vehicle> _filteredVehicles = new();

        // Progress properties
        [ObservableProperty]
        private bool _isImporting;

        [ObservableProperty]
        private int _importProgress;

        [ObservableProperty]
        private string _currentOperation = "";

        public IAsyncRelayCommand ImportVehiclesCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public VehicleManagementViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

            ImportVehiclesCommand = new AsyncRelayCommand(ImportVehiclesAsync);
            CloseCommand = new RelayCommand(Close);

            LoadVehiclesAsync().ConfigureAwait(false);
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredVehicles = new ObservableCollection<Vehicle>(Vehicles);
                return;
            }

            var filtered = Vehicles.Where(v =>
                v.ChassisNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                v.Model?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                v.Make?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true
            ).ToList();

            FilteredVehicles = new ObservableCollection<Vehicle>(filtered);
        }

        private async Task LoadVehiclesAsync()
        {
            var all = await _unitOfWork.Vehicles.GetAllAsync();
            Vehicles = new ObservableCollection<Vehicle>(all);
            ApplyFilter();
        }

        private void Close() =>
            System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this)?.Close();

        // ===== IMPORT VEHICLES =====
        private async Task ImportVehiclesAsync()
        {
            // --- UI feedback start ---
            IsImporting = true;
            ImportProgress = 0;
            CurrentOperation = "Preparing import...";
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select ParcCarrières CSV file",
                Filter = "CSV files|*.csv",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                IsImporting = false;
                ImportProgress = 0;
                CurrentOperation = "";
                Mouse.OverrideCursor = null;
                return;
            }

            string filePath = dialog.FileName;
            var logger = App.GetService<ILoggingService>();
            logger.LogInfo($"Starting vehicle import from {filePath}");

            try
            {
                var parser = new VehicleCsvParser();
                CurrentOperation = "Parsing CSV file...";
                ImportProgress = 10;
                var importDtos = parser.Parse(filePath);

                if (!importDtos.Any())
                {
                    System.Windows.MessageBox.Show(LocalizationManager.Instance["NoValidVehicleDataInFile"], LocalizationManager.Instance["BackupView_Import"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                using (var scope = App.GetService<IServiceScopeFactory>().CreateScope())
                {
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var clientRepo = (ClientRepository)unitOfWork.Clients;
                    var vehicleRepo = (VehicleRepository)unitOfWork.Vehicles;

                    CurrentOperation = "Fetching existing clients...";
                    ImportProgress = 20;
                    var allClients = await clientRepo.GetAllAsync();
                    var clientDict = allClients.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

                    CurrentOperation = "Preparing clients...";
                    ImportProgress = 30;

                    // Ensure "PSA Group" client
                    if (!clientDict.ContainsKey("PSA Group"))
                    {
                        var psaGroup = new Client { Name = "PSA Group", Type = ClientType.PSA, IsActive = true };
                        await clientRepo.AddAsync(psaGroup);
                        await unitOfWork.CompleteAsync();
                        clientDict["PSA Group"] = psaGroup;
                        logger.LogInfo("Created default client 'PSA Group'");
                    }

                    // Collect all cleaned client names
                    var clientNames = importDtos
                        .Select(dto => CleanClientName(dto.ClientName))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    // Create missing clients
                    var newClients = new List<Client>();
                    foreach (var name in clientNames)
                    {
                        if (!clientDict.ContainsKey(name))
                        {
                            var client = new Client { Name = name, Type = ClientType.PSA, IsActive = true };
                            newClients.Add(client);
                            clientDict[name] = client;
                        }
                    }

                    if (newClients.Any())
                    {
                        CurrentOperation = $"Saving {newClients.Count} new clients...";
                        ImportProgress = 40;
                        await clientRepo.AddRangeAsync(newClients);
                        await unitOfWork.CompleteAsync();
                        logger.LogInfo($"Created {newClients.Count} new clients.");
                    }

                    // Build client ID map
                    var clientIdMap = clientDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Id, StringComparer.OrdinalIgnoreCase);

                    CurrentOperation = "Fetching existing vehicles...";
                    ImportProgress = 50;
                    var existingVehicles = await vehicleRepo.GetAllAsync();
                    var vehicleDict = existingVehicles.ToDictionary(v => v.ChassisNumber, v => v, StringComparer.OrdinalIgnoreCase);

                    CurrentOperation = "Processing vehicles...";
                    ImportProgress = 60;
                    var newVehicles = new List<Vehicle>();
                    int skipped = 0;
                    int total = importDtos.Count;
                    int processed = 0;

                    foreach (var dto in importDtos)
                    {
                        processed++;
                        // Update progress every 100 items
                        if (processed % 100 == 0 || processed == total)
                        {
                            int progress = 60 + (int)((double)processed / total * 30);
                            ImportProgress = progress;
                            CurrentOperation = $"Processing vehicle {processed}/{total}...";
                        }

                        string cleanClient = CleanClientName(dto.ClientName);
                        if (!clientIdMap.TryGetValue(cleanClient, out int clientId))
                        {
                            clientId = clientIdMap["PSA Group"];
                        }

                        if (vehicleDict.ContainsKey(dto.ChassisNumber))
                        {
                            skipped++;
                            continue;
                        }

                        var vehicle = new Vehicle
                        {
                            ChassisNumber = dto.ChassisNumber,
                            Model = dto.Model,
                            ClientId = clientId,
                            IsActive = true
                        };
                        newVehicles.Add(vehicle);
                        vehicleDict[dto.ChassisNumber] = vehicle;
                    }

                    if (newVehicles.Any())
                    {
                        CurrentOperation = $"Saving {newVehicles.Count} new vehicles...";
                        ImportProgress = 90;
                        await vehicleRepo.AddRangeAsync(newVehicles);
                        await unitOfWork.CompleteAsync();
                        logger.LogInfo($"Added {newVehicles.Count} new vehicles.");
                    }

                    ImportProgress = 100;
                    CurrentOperation = "Import complete!";
                    logger.LogInfo($"Vehicle import: {newVehicles.Count} added, {skipped} skipped, {newClients.Count} new clients.");
                    System.Windows.MessageBox.Show($"Import complete:\n{newVehicles.Count} vehicles added\n{skipped} skipped (already exist)\n{newClients.Count} new clients created",
                        LocalizationManager.Instance["VehicleManagementView_ImportVehicles"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                    await LoadVehiclesAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Vehicle import failed", ex);
                System.Windows.MessageBox.Show($"Error: {ex.Message}", LocalizationManager.Instance["ImportFailed"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsImporting = false;
                ImportProgress = 0;
                CurrentOperation = "";
                Mouse.OverrideCursor = null;
            }
        }

        // ===== CLIENT NAME CLEANING =====
        private static readonly HashSet<string> ExcludedWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "ok", "acc", "kit", "logos", "conforme", "pneus", "att.", "att. rv", "att rv",
            "66", "11", "68", "31", "10", "tapis", "relavage","relavag", "gravage", "pose", "camera",
            "ecran", "sk", "bois", "serrure", "cradel", "grille", "barre", "toit", "balisage",
            "alarme", "antivol", "crochet", "attelage", "boitier", "controle", "housse", "tea", "MQ", "ct", "X","JK","LAUTO","LAUTO*","TRANS","COMPET","COMPAGNE",
            "*","dr","le","atelier","Relavage","11Relavage","Nettoyage","Préparation"
        };
        private static readonly HashSet<string> CompanySuffixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "acb", "sarl", "sas", "eurl", "sa", "sasu", "sci", "snc", "scop", "selarl", "selas", "gmbh", "ltd", "inc"
        };
        private static readonly Regex CodePattern = new Regex(@"^[A-Z]{2,}\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NumericPattern = new Regex(@"^\d+$", RegexOptions.Compiled);
        private static readonly Regex HeaderWordsPattern = new Regex(
            @"^(heure|type|rapide|normale|site|livr|client|modèle|modele|vin)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly char[] SplitChars = new[] { ' ', '/', '\\', '-', '_', '(', ')', '[', ']', ',', ';' };

        private string CleanClientName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "Unknown";

            string cleaned = rawName;
            cleaned = Regex.Replace(cleaned, @"[^\p{L}\s\-']", " ");

            var tokens = cleaned.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            var filteredTokens = new List<string>();

            foreach (var token in tokens)
            {
                string t = token.Trim();
                if (string.IsNullOrWhiteSpace(t))
                    continue;

                if (t.Length >= 2 && t.Length <= 3 && t.All(char.IsUpper))
                    continue;
                if (ExcludedWords.Contains(t))
                    continue;
                if (NumericPattern.IsMatch(t))
                    continue;
                if (CodePattern.IsMatch(t))
                    continue;
                if (HeaderWordsPattern.IsMatch(t))
                    continue;

                filteredTokens.Add(t);
            }

            if (!filteredTokens.Any())
                return "Unknown";

            string result = string.Join(" ", filteredTokens).Trim();
            result = Regex.Replace(result, @"\s+", " ");

            var words = result.Split(' ');
            if (words.Length > 1 && CompanySuffixes.Contains(words.Last()))
            {
                result = string.Join(" ", words.Take(words.Length - 1));
            }

            if (string.IsNullOrWhiteSpace(result))
                return "Unknown";

            var culture = System.Globalization.CultureInfo.CurrentCulture;
            return culture.TextInfo.ToTitleCase(result.ToLower());
        }
    }
}