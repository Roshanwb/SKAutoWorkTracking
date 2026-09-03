using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Core.Services;
using SKAuto.Data.Repository;
using SKAuto.UI.Localization;
using SKAuto.UI.ViewModels;
using SKAuto.UI.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace SKAuto.UI.ViewModels
{
    public partial class WorkOrderDetailViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _logger;
        private readonly IConfigurationService _configService;
        private readonly IGoogleDriveService _driveService;
        private readonly IMessageBoxService _messageBoxService; // NEW
        private readonly bool _isNew;
        private decimal _psaRate;

        [ObservableProperty]
        private WorkOrder _workOrder;

        [ObservableProperty]
        private ObservableCollection<Client> _clients = new();

        [ObservableProperty]
        private Client? _selectedClient;

        [ObservableProperty]
        private ObservableCollection<Vehicle> _vehicles = new();

        [ObservableProperty]
        private Vehicle? _selectedVehicle;

        [ObservableProperty]
        private string _chassisSearch = "";

        [ObservableProperty]
        private ObservableCollection<Accessory> _availableAccessories = new();

        [ObservableProperty]
        private ObservableCollection<Accessory> _filteredAccessories = new();

        [ObservableProperty]
        private string _accessorySearchText = string.Empty;

        [ObservableProperty]
        private Accessory? _selectedAccessory;

        [ObservableProperty]
        private int _quantity = 1;

        [ObservableProperty]
        private int _taskMinutes = 0;

        [ObservableProperty]
        private decimal? _taskPrice;

        [ObservableProperty]
        private ObservableCollection<WorkTask> _tasks = new();

        [ObservableProperty]
        private ObservableCollection<Travel> _travels = new();

        [ObservableProperty]
        private ObservableCollection<SourceDocument> _attachments = new();

        [ObservableProperty]
        private SourceDocument? _selectedAttachment;

        // OrderType options with friendly names
        public List<SelectableOption<OrderType>> OrderTypeOptions { get; }

        private SelectableOption<OrderType> _selectedOrderTypeOption;
        public SelectableOption<OrderType> SelectedOrderTypeOption
        {
            get => _selectedOrderTypeOption;
            set
            {
                if (SetProperty(ref _selectedOrderTypeOption, value) && value != null)
                {
                    WorkOrder.OrderType = value.Value;
                }
            }
        }

        public Array WorkStatusValues => Enum.GetValues(typeof(WorkStatus));

        public IAsyncRelayCommand SearchVehicleCommand { get; }
        public IAsyncRelayCommand ShowVehicleWorkOrdersCommand { get; } // NEW
        public IRelayCommand AddTaskCommand { get; }
        public IRelayCommand<WorkTask> RemoveTaskCommand { get; }
        public IRelayCommand AddTravelCommand { get; }
        public IRelayCommand<Travel> RemoveTravelCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }
        public IRelayCommand OpenVehicleEditCommand { get; }
        public IRelayCommand OpenAddTaskCommand { get; }
        public IRelayCommand IncrementTimeCommand { get; }
        public IRelayCommand DecrementTimeCommand { get; }

        public IAsyncRelayCommand AddAttachmentCommand { get; }
        public IAsyncRelayCommand<SourceDocument> RemoveAttachmentCommand { get; }
        public IAsyncRelayCommand<SourceDocument> DownloadAttachmentCommand { get; }

        // Updated constructor with IMessageBoxService
        public WorkOrderDetailViewModel(IUnitOfWork unitOfWork, ILoggingService logger, IConfigurationService configService,
                                        IGoogleDriveService driveService, IMessageBoxService messageBoxService,
                                        int workOrderId = 0)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configService = configService;
            _driveService = driveService;
            _messageBoxService = messageBoxService; // NEW
            _isNew = workOrderId == 0;

            _logger.LogInfo($"WorkOrderDetailViewModel initializing. IsNew: {_isNew}, WorkOrderId: {workOrderId}");

            // Build OrderType options with friendly names
            OrderTypeOptions = Enum.GetValues(typeof(OrderType))
                .Cast<OrderType>()
                .Select(ot => new SelectableOption<OrderType> { Value = ot, Display = ot.GetDisplayName() })
                .ToList();

            SearchVehicleCommand = new AsyncRelayCommand(SearchVehicleAsync);
            ShowVehicleWorkOrdersCommand = new AsyncRelayCommand(ShowVehicleWorkOrdersAsync); // NEW
            AddTaskCommand = new RelayCommand(AddTask);
            RemoveTaskCommand = new RelayCommand<WorkTask>(RemoveTask);
            AddTravelCommand = new RelayCommand(AddTravel);
            RemoveTravelCommand = new RelayCommand<Travel>(RemoveTravel);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(() => CloseWindow());
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !_isNew);
            OpenVehicleEditCommand = new RelayCommand(OpenVehicleEdit, () => SelectedVehicle != null);
            OpenAddTaskCommand = new RelayCommand(OpenAddTask);

            IncrementTimeCommand = new RelayCommand(() => TaskMinutes = System.Math.Min(999, TaskMinutes + 5));
            DecrementTimeCommand = new RelayCommand(() => TaskMinutes = System.Math.Max(0, TaskMinutes - 5));

            AddAttachmentCommand = new AsyncRelayCommand(AddAttachmentAsync);
            RemoveAttachmentCommand = new AsyncRelayCommand<SourceDocument>(RemoveAttachmentAsync);
            DownloadAttachmentCommand = new AsyncRelayCommand<SourceDocument>(DownloadAttachmentAsync);

            _ = InitializeAsync(workOrderId);
            _ = LoadPsaRateAsync();
        }

        // NEW: Command to show all work orders for the current chassis
        private async Task ShowVehicleWorkOrdersAsync()
        {
            if (string.IsNullOrWhiteSpace(ChassisSearch))
            {
                _messageBoxService.Show("Please enter a chassis number first.", "No Chassis", MessageBoxButtonType.OK, MessageBoxImageType.Information);
                return;
            }

            // Check if vehicle exists
            var vehicle = await _unitOfWork.Vehicles.FindAsync(v => v.ChassisNumber == ChassisSearch);
            if (!vehicle.Any())
            {
                _messageBoxService.Show("Vehicle not found. Please search first.", "Not Found", MessageBoxButtonType.OK, MessageBoxImageType.Warning);
                return;
            }

            // Open the lookup window
            var viewModel = new VehicleWorkOrdersViewModel(_unitOfWork, ChassisSearch);
            var view = new VehicleWorkOrdersView
            {
                DataContext = viewModel,
                Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            };
            view.ShowDialog();
        }

        private async Task LoadPsaRateAsync()
        {
            try
            {
                var config = await _configService.GetAsync<AppConfig>("AppConfig") ?? new AppConfig();
                _psaRate = config.PsaRate;
            }
            catch
            {
                _psaRate = 66.0m;
            }
        }

        partial void OnAccessorySearchTextChanged(string value)
        {
            _logger.LogInfo($"Accessory search text changed: '{value}'");
            FilterAccessories(value);
        }

        private void FilterAccessories(string searchText)
        {
            _logger.LogInfo($"Filtering accessories with search: '{searchText}'");
            if (string.IsNullOrWhiteSpace(searchText))
            {
                FilteredAccessories = new ObservableCollection<Accessory>(AvailableAccessories);
                return;
            }

            var lowerSearch = searchText.ToLowerInvariant();
            var filtered = AvailableAccessories
                .Where(a => a.Name?.ToLowerInvariant().Contains(lowerSearch) == true ||
                            a.PartNumber?.ToLowerInvariant().Contains(lowerSearch) == true)
                .ToList();

            FilteredAccessories = new ObservableCollection<Accessory>(filtered);
        }

        partial void OnSelectedAccessoryChanged(Accessory? value)
        {
            if (value != null)
            {
                TaskMinutes = value.Time ?? 0;
                TaskPrice = value.Price;
                _logger.LogInfo($"Selected accessory: {value.Name}, default time {TaskMinutes} min, price {TaskPrice:C}");
            }
            else
            {
                TaskMinutes = 0;
                TaskPrice = null;
            }
        }

        partial void OnTaskMinutesChanged(int value)
        {
            if (value > 0 && _psaRate > 0)
            {
                decimal calculatedPrice = System.Math.Round((_psaRate / 60) * value, 2);
                TaskPrice = calculatedPrice;
                _logger.LogInfo($"Minutes changed to {value}, recalculated price to {calculatedPrice:C} using rate {_psaRate:C}/h");
            }
            else
            {
                if (SelectedAccessory != null)
                    TaskPrice = SelectedAccessory.Price;
                else
                    TaskPrice = null;
            }
        }

        private void AddTravel()
        {
            _logger.LogInfo(LocalizationManager.Instance["AddTravelCalled"]);
            var dialog = new TravelDialog();
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var travel = new Travel
                    {
                        WorkOrderId = WorkOrder.Id,
                        TravelDate = dialog.TravelDate,
                        Destination = dialog.Destination,
                        DistanceKm = decimal.TryParse(dialog.DistanceKm, out var km) ? km : (decimal?)null,
                        TravelCost = decimal.TryParse(dialog.TravelCost, out var cost) ? cost : (decimal?)null,
                        Notes = dialog.Notes
                    };
                    Travels.Add(travel);
                    WorkOrder.Travels.Add(travel);
                    _logger.LogInfo($"Added travel to {travel.Destination} on {travel.TravelDate:yyyy-MM-dd}, cost={travel.TravelCost}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(LocalizationManager.Instance["FailedToAddTravel"], ex);
                    _messageBoxService.Show($"Error adding travel: {ex.Message}", "Error", MessageBoxButtonType.OK, MessageBoxImageType.Error);
                }
            }
            else
            {
                _logger.LogInfo(LocalizationManager.Instance["AddTravelCancelledByUser"]);
            }
        }

        private void RemoveTravel(Travel? travel)
        {
            if (travel == null) return;
            _logger.LogInfo($"Removing travel: Destination={travel.Destination}, Date={travel.TravelDate:yyyy-MM-dd}");
            Travels.Remove(travel);
            WorkOrder.Travels.Remove(travel);
        }

        private async void OpenAddTask()
        {
            try
            {
                var logger = App.GetService<ILoggingService>();
                var configService = App.GetService<IConfigurationService>();
                var vm = new AccessoryManagementViewModel(_unitOfWork, logger, configService);
                var view = new AccessoryManagementView { DataContext = vm };

                var window = new Window
                {
                    Title = "Manage Tasks",
                    Content = view,
                    Width = 900,
                    Height = 700,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                };

                if (window.ShowDialog() == true)
                {
                    AccessorySearchText = "";
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await RefreshAccessoriesAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                var logger = App.GetService<ILoggingService>();
                logger?.LogError("Failed to open accessory management", ex);
                _messageBoxService.Show($"Error: {ex.Message}", "Error", MessageBoxButtonType.OK, MessageBoxImageType.Error);
            }
        }

        private async Task RefreshAccessoriesAsync()
        {
            _logger.LogInfo(LocalizationManager.Instance["RefreshingAccessoriesList"]);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var accessories = await _unitOfWork.Accessories.GetAllAsync();
                AvailableAccessories = new ObservableCollection<Accessory>(accessories.OrderBy(a => a.Name));
                FilterAccessories(AccessorySearchText);
                _logger.LogInfo($"Loaded {AvailableAccessories.Count} accessories, filtered to {FilteredAccessories.Count}");
            });
        }

        private async void OpenVehicleEdit()
        {
            if (SelectedVehicle == null) return;

            _logger.LogInfo($"Opening vehicle edit for chassis {SelectedVehicle.ChassisNumber}");
            var editWindow = new VehicleEditWindow();
            var viewModel = new VehicleEditViewModel(_unitOfWork, SelectedVehicle);
            editWindow.DataContext = viewModel;
            editWindow.Owner = System.Windows.Application.Current.Windows.OfType<System.Windows. Window>().FirstOrDefault(w => w.IsActive);

            if (editWindow.ShowDialog() == true)
            {
                var refreshedVehicle = await _unitOfWork.Vehicles.GetByIdAsync(SelectedVehicle.Id);
                if (refreshedVehicle != null)
                {
                    SelectedVehicle = refreshedVehicle;
                    if (refreshedVehicle.Client != null)
                    {
                        SelectedClient = refreshedVehicle.Client;
                        WorkOrder.VehicleId = refreshedVehicle.Id;
                    }
                    _logger.LogInfo($"Vehicle refreshed after edit: {refreshedVehicle.ChassisNumber}");
                }
                await RefreshVehiclesList();
                OnPropertyChanged(nameof(SelectedVehicle));
            }
        }

        private async Task InitializeAsync(int workOrderId)
        {
            try
            {
                _logger.LogInfo(LocalizationManager.Instance["InitializeAsyncStarted"]);

                var clients = await _unitOfWork.Clients.GetAllAsync();
                Clients = new ObservableCollection<Client>(clients);
                _logger.LogInfo($"Loaded {Clients.Count} clients");

                var vehicles = await _unitOfWork.Vehicles.GetAllAsync();
                Vehicles = new ObservableCollection<Vehicle>(vehicles);
                _logger.LogInfo($"Loaded {Vehicles.Count} vehicles");

                var accessories = await _unitOfWork.Accessories.GetAllAsync();
                AvailableAccessories = new ObservableCollection<Accessory>(accessories.OrderBy(a => a.Name));
                FilterAccessories("");
                _logger.LogInfo($"Loaded {AvailableAccessories.Count} accessories");

                if (!_isNew)
                {
                    var repo = (WorkOrderRepository)_unitOfWork.WorkOrders;
                    WorkOrder = await repo.GetWithDetailsAsync(workOrderId);
                    if (WorkOrder == null)
                    {
                        _logger.LogError($"WorkOrder with ID {workOrderId} not found");
                        _messageBoxService.Show($"Work order #{workOrderId} not found.", "Error", MessageBoxButtonType.OK, MessageBoxImageType.Error);
                        CloseWindow();
                        return;
                    }

                    SelectedVehicle = WorkOrder.Vehicle;
                    if (SelectedVehicle != null)
                    {
                        ChassisSearch = SelectedVehicle.ChassisNumber;
                        SelectedClient = SelectedVehicle.Client;
                        _logger.LogInfo($"Loaded existing work order #{WorkOrder.Id}, Vehicle: {SelectedVehicle.ChassisNumber}, Client: {SelectedClient?.Name ?? "None"}");
                    }
                    Tasks = new ObservableCollection<WorkTask>(WorkOrder.WorkTasks);
                    Travels = new ObservableCollection<Travel>(WorkOrder.Travels);
                    var docs = await _unitOfWork.SourceDocuments.FindAsync(d => d.WorkOrderId == workOrderId);
                    Attachments = new ObservableCollection<SourceDocument>(docs.OrderByDescending(d => d.UploadDate ?? DateTime.MinValue));
                    _logger.LogInfo($"Loaded {Tasks.Count} tasks, {Travels.Count} travels, {Attachments.Count} attachments");

                    // Set selected OrderType option
                    SelectedOrderTypeOption = OrderTypeOptions.FirstOrDefault(o => o.Value == WorkOrder.OrderType);
                }
                else
                {
                    WorkOrder = new WorkOrder
                    {
                        OrderDate = DateTime.Today,
                        Status = WorkStatus.Planned,
                        OrderType = OrderType.PSA_Sur_Site
                    };
                    Tasks = new ObservableCollection<WorkTask>();
                    Travels = new ObservableCollection<Travel>();
                    Attachments = new ObservableCollection<SourceDocument>();
                    _logger.LogInfo(LocalizationManager.Instance["CreatedNewWorkOrder"]);

                    // Set default OrderType option
                    SelectedOrderTypeOption = OrderTypeOptions.FirstOrDefault(o => o.Value == OrderType.PSA_Sur_Site);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(LocalizationManager.Instance["InitializeAsyncFailed"], ex);
                _messageBoxService.Show($"Error initializing: {ex.Message}", "Error", MessageBoxButtonType.OK, MessageBoxImageType.Error);
                CloseWindow();
            }
        }

        private async Task SearchVehicleAsync()
        {
            if (string.IsNullOrWhiteSpace(ChassisSearch)) return;

            _logger.LogInfo($"Searching vehicle with chassis: {ChassisSearch}");
            try
            {
                var repo = (VehicleRepository)_unitOfWork.Vehicles;
                var vehicle = await repo.GetByChassisWithClientAsync(ChassisSearch);
                if (vehicle != null)
                {
                    SelectedVehicle = vehicle;
                    SelectedClient = vehicle.Client;
                    _logger.LogInfo($"Vehicle found: {vehicle.ChassisNumber}, Client: {vehicle.Client?.Name ?? "None"} (ID {vehicle.ClientId})");
                    return;
                }

                _logger.LogWarning($"Vehicle with chassis {ChassisSearch} not found");
                var result = _messageBoxService.Show($"Vehicle with chassis {ChassisSearch} not found. Create new vehicle?",
                    "Create Vehicle", MessageBoxButtonType.YesNo, MessageBoxImageType.Question);
                if (result != MessageBoxResultType.Yes) return;

                if (SelectedClient == null)
                {
                    if (Clients.Any())
                    {
                        SelectedClient = Clients.First();
                        _logger.LogInfo($"Auto-selected client: {SelectedClient.Name} (ID {SelectedClient.Id})");
                    }
                    else
                    {
                        _logger.LogWarning(LocalizationManager.Instance["NoClientAvailableCannotCreateVehicle"]);
                        _messageBoxService.Show(LocalizationManager.Instance["NoClientExists"], "No Client", MessageBoxButtonType.OK, MessageBoxImageType.Warning);
                        return;
                    }
                }

                if (SelectedClient.Id <= 0)
                {
                    _logger.LogError($"Selected client has invalid ID {SelectedClient.Id}");
                    _messageBoxService.Show(LocalizationManager.Instance["SelectedClientNotSavedYet"], "Invalid Client", MessageBoxButtonType.OK, MessageBoxImageType.Error);
                    return;
                }

                var existingClient = await _unitOfWork.Clients.GetByIdAsync(SelectedClient.Id);
                if (existingClient == null)
                {
                    _logger.LogError($"Client with ID {SelectedClient.Id} does not exist in database.");
                    _messageBoxService.Show(LocalizationManager.Instance["SelectedClientNoLongerExists"], "Client Not Found", MessageBoxButtonType.OK, MessageBoxImageType.Error);
                    return;
                }

                _logger.LogInfo($"Creating new vehicle for client: {existingClient.Name} (ID {existingClient.Id})");

                vehicle = new Vehicle
                {
                    ChassisNumber = ChassisSearch,
                    Model = "Unknown",
                    ClientId = existingClient.Id,
                    IsActive = true
                };

                await _unitOfWork.Vehicles.AddAsync(vehicle);
                await _unitOfWork.CompleteAsync();

                SelectedVehicle = vehicle;
                await RefreshVehiclesList();
                _logger.LogInfo($"Created new vehicle: {vehicle.ChassisNumber} for client {existingClient.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching/creating vehicle: {ChassisSearch}", ex);
                _messageBoxService.Show($"Error: {ex.Message}", "Error", MessageBoxButtonType.OK, MessageBoxImageType.Error);
            }
        }

        private async Task RefreshVehiclesList()
        {
            var allVehicles = await _unitOfWork.Vehicles.GetAllAsync();
            Vehicles = new ObservableCollection<Vehicle>(allVehicles);
            _logger.LogInfo($"Refreshed vehicles list, now {Vehicles.Count} vehicles");
        }

        partial void OnSelectedVehicleChanged(Vehicle? value)
        {
            if (value != null)
            {
                SelectedClient = value.Client;
                if (WorkOrder != null)
                {
                    WorkOrder.VehicleId = value.Id;
                    _logger.LogInfo($"Selected vehicle changed to ID {value.Id}, Chassis {value.ChassisNumber}");
                }
            }
        }

        partial void OnSelectedClientChanged(Client? value)
        {
            if (value != null && SelectedVehicle != null)
            {
                SelectedVehicle.ClientId = value.Id;
                SelectedVehicle.Client = value;
                _logger.LogInfo($"Updated vehicle client to {value.Name} (ID {value.Id})");
            }
            if (value != null)
                _logger.LogInfo($"Selected client changed to {value.Name} (ID {value.Id})");
        }

        private void AddTask()
        {
            if (SelectedAccessory == null)
            {
                _logger.LogWarning(LocalizationManager.Instance["AddTaskNoAccessorySelected"]);
                return;
            }

            var task = new WorkTask
            {
                WorkOrderId = WorkOrder.Id,
                AccessoryId = SelectedAccessory.Id,
                Accessory = SelectedAccessory,
                Quantity = Quantity,
                TaskType = SelectedAccessory.TaskType,
                TaskStatus = WorkStatus.Planned,
                Price = TaskPrice ?? SelectedAccessory.Price,
                EstimatedMinutes = TaskMinutes > 0 ? TaskMinutes : SelectedAccessory.Time
            };

            Tasks.Add(task);
            WorkOrder.WorkTasks.Add(task);

            TaskMinutes = 0;
            TaskPrice = null;
            AccessorySearchText = "";
            SelectedAccessory = null;

            _logger.LogInfo($"Added task: Accessory '{task.Accessory.Name}', Type '{task.TaskType}', Quantity {Quantity}, Price {task.Price}, EstMin {task.EstimatedMinutes}");
        }

        private void RemoveTask(WorkTask? task)
        {
            if (task != null)
            {
                var taskInfo = $"AccessoryId {task.AccessoryId}, Quantity {task.Quantity}, Price {task.Price}";
                Tasks.Remove(task);
                WorkOrder.WorkTasks.Remove(task);
                _logger.LogInfo($"Removed task: {taskInfo}");
            }
        }

        private async Task AddAttachmentAsync()
        {
            var openFileDialog = new Microsoft.Win32.   OpenFileDialog
            {
                Multiselect = true,
                Title = "Select files to attach"
            };

            if (openFileDialog.ShowDialog() != true) return;

            try
            {
                var baseFolderId = await _driveService.GetFolderIdAsync("SKAuto Attachments");
                var workOrderFolderId = await _driveService.GetSubFolderIdAsync(baseFolderId, WorkOrder.Id.ToString());

                foreach (var filePath in openFileDialog.FileNames)
                {
                    var fileName = Path.GetFileName(filePath);
                    var fileId = await _driveService.UploadFileAsync(filePath, fileName, workOrderFolderId);

                    var doc = new SourceDocument
                    {
                        WorkOrderId = WorkOrder.Id,
                        OriginalFilename = fileName,
                        GoogleDriveFileId = fileId,
                        FileHash = ComputeFileHash(filePath),
                        FileSize = new FileInfo(filePath).Length,
                        UploadDate = DateTime.UtcNow,
                        ContentType = GetMimeType(filePath),
                        DocumentType = "Attachment"
                    };
                    await _unitOfWork.SourceDocuments.AddAsync(doc);
                }
                await _unitOfWork.CompleteAsync();
                var docs = await _unitOfWork.SourceDocuments.FindAsync(d => d.WorkOrderId == WorkOrder.Id);
                Attachments = new ObservableCollection<SourceDocument>(docs.OrderByDescending(d => d.UploadDate ?? DateTime.MinValue));
                _logger.LogInfo($"Added {openFileDialog.FileNames.Length} attachment(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to add attachment", ex);
                _messageBoxService.Show($"Error adding attachment: {ex.Message}", "Error", MessageBoxButtonType.OK, MessageBoxImageType.Error);
            }
        }

        private async Task RemoveAttachmentAsync(SourceDocument? doc)
        {
            if (doc == null) return;
            var result = _messageBoxService.Show($"Delete attachment '{doc.OriginalFilename}'? This action cannot be undone.",
                "Confirm Delete", MessageBoxButtonType.YesNo, MessageBoxImageType.Warning);
            if (result != MessageBoxResultType.Yes) return;

            try
            {
                if (!string.IsNullOrEmpty(doc.GoogleDriveFileId))
                    await _driveService.DeleteFileAsync(doc.GoogleDriveFileId);

                await _unitOfWork.SourceDocuments.DeleteAsync(doc);
                await _unitOfWork.CompleteAsync();
                var docs = await _unitOfWork.SourceDocuments.FindAsync(d => d.WorkOrderId == WorkOrder.Id);
                Attachments = new ObservableCollection<SourceDocument>(docs.OrderByDescending(d => d.UploadDate ?? DateTime.MinValue));
                _logger.LogInfo($"Deleted attachment {doc.OriginalFilename} (ID {doc.Id})");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to delete attachment", ex);
                _messageBoxService.Show($"Error deleting attachment: {ex.Message}", "Error", MessageBoxButtonType.OK, MessageBoxImageType.Error);
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

            try
            {
                await _driveService.DownloadFileAsync(doc.GoogleDriveFileId, saveDialog.FileName);
                _messageBoxService.Show($"File downloaded to {saveDialog.FileName}", "Download Complete", MessageBoxButtonType.OK, MessageBoxImageType.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to download attachment", ex);
                _messageBoxService.Show($"Error downloading attachment: {ex.Message}", "Error", MessageBoxButtonType.OK, MessageBoxImageType.Error);
            }
        }

        private string ComputeFileHash(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private string GetMimeType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
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

        private async Task SaveAsync()
        {
            try
            {
                _logger.LogInfo(LocalizationManager.Instance["SaveAsyncStarted"]);

                if (SelectedVehicle == null)
                {
                    _logger.LogWarning(LocalizationManager.Instance["SaveAttemptedWithoutVehicleSelected"]);
                    _messageBoxService.Show(LocalizationManager.Instance["PleaseSelectVehicle"], "Validation", MessageBoxButtonType.OK, MessageBoxImageType.Warning);
                    return;
                }

                WorkOrder.VehicleId = SelectedVehicle.Id;

                // --- NEW: Duplicate check ---
                var existingOrders = await _unitOfWork.WorkOrders.FindAsync(
                    wo => wo.VehicleId == SelectedVehicle.Id &&
                          wo.OrderDate == WorkOrder.OrderDate &&
                          wo.Id != WorkOrder.Id // exclude self if editing
                );

                if (existingOrders.Any())
                {
                    _logger.LogWarning($"Duplicate work order for vehicle {SelectedVehicle.ChassisNumber} on {WorkOrder.OrderDate:yyyy-MM-dd}");
                    _messageBoxService.Show(
                        $"A work order already exists for chassis {SelectedVehicle.ChassisNumber} on {WorkOrder.OrderDate:dd/MM/yyyy}. Please choose a different date.",
                        "Duplicate Work Order",
                        MessageBoxButtonType.OK,
                        MessageBoxImageType.Warning
                    );
                    return;
                }
                // --- End of duplicate check ---

                if (SelectedClient != null && SelectedVehicle.ClientId != SelectedClient.Id)
                {
                    _logger.LogInfo($"Updating vehicle client from {SelectedVehicle.ClientId} to {SelectedClient.Id}");
                    SelectedVehicle.ClientId = SelectedClient.Id;
                    await _unitOfWork.Vehicles.UpdateAsync(SelectedVehicle);
                }

                WorkOrder.CalculateTotal();
                _logger.LogInfo($"WorkOrder total calculated: {WorkOrder.TotalAmount:C}");

                if (_isNew)
                {
                    await _unitOfWork.WorkOrders.AddAsync(WorkOrder);
                    _logger.LogInfo($"New work order added (ID {WorkOrder.Id})");
                }
                else
                {
                    await _unitOfWork.WorkOrders.UpdateAsync(WorkOrder);
                    _logger.LogInfo($"Work order updated (ID {WorkOrder.Id})");
                }

                await _unitOfWork.CompleteAsync();
                _logger.LogInfo(LocalizationManager.Instance["WorkOrderSavedSuccessfully"]);
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(LocalizationManager.Instance["SaveAsyncFailed"], ex);
                _messageBoxService.Show($"Error saving: {ex.Message}", "Error", MessageBoxButtonType.OK, MessageBoxImageType.Error);
            }
        }

        private async Task DeleteAsync()
        {
            if (_isNew) return;

            _logger.LogInfo($"DeleteAsync called for work order #{WorkOrder.Id}");
            var result = _messageBoxService.Show($"Delete work order #{WorkOrder.Id}? This will also delete all associated tasks and travels.",
                "Confirm Delete", MessageBoxButtonType.YesNo, MessageBoxImageType.Warning);
            if (result != MessageBoxResultType.Yes) return;

            try
            {
                await _unitOfWork.WorkOrders.DeleteAsync(WorkOrder);
                await _unitOfWork.CompleteAsync();
                _logger.LogInfo($"Work order #{WorkOrder.Id} deleted successfully");
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"DeleteAsync failed for work order #{WorkOrder.Id}", ex);
                _messageBoxService.Show($"Error deleting: {ex.Message}", "Error", MessageBoxButtonType.OK, MessageBoxImageType.Error);
            }
        }

        private void CloseWindow(bool success = false)
        {
            _logger.LogInfo($"Closing window, success={success}");
            foreach (Window window in System.Windows.Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.DialogResult = success;
                    window.Close();
                    break;
                }
        }
    }
}