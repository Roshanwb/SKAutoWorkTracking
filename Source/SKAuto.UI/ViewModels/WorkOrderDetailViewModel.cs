using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Repository;
using SKAuto.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using SKAuto.UI.Localization;
namespace SKAuto.UI.ViewModels
{
    public partial class WorkOrderDetailViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _logger;
        private readonly bool _isNew;

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

        // Search properties
        [ObservableProperty]
        private ObservableCollection<Accessory> _filteredAccessories = new();

        [ObservableProperty]
        private string _accessorySearchText = string.Empty;

        [ObservableProperty]
        private Accessory? _selectedAccessory;

        [ObservableProperty]
        private int _quantity = 1;

        [ObservableProperty]
        private ObservableCollection<WorkTask> _tasks = new();

        [ObservableProperty]
        private ObservableCollection<Travel> _travels = new();

        public Array OrderTypeValues => Enum.GetValues(typeof(OrderType));
        public Array WorkStatusValues => Enum.GetValues(typeof(WorkStatus));

        public IAsyncRelayCommand SearchVehicleCommand { get; }
        public IRelayCommand AddTaskCommand { get; }
        public IRelayCommand<WorkTask> RemoveTaskCommand { get; }
        public IRelayCommand AddTravelCommand { get; }
        public IRelayCommand<Travel> RemoveTravelCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }
        public IRelayCommand OpenVehicleEditCommand { get; }
        public IRelayCommand OpenAddTaskCommand { get; }

        public WorkOrderDetailViewModel(IUnitOfWork unitOfWork, ILoggingService logger, int workOrderId = 0)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _isNew = workOrderId == 0;

            _logger.LogInfo($"WorkOrderDetailViewModel initializing. IsNew: {_isNew}, WorkOrderId: {workOrderId}");

            SearchVehicleCommand = new AsyncRelayCommand(SearchVehicleAsync);
            AddTaskCommand = new RelayCommand(AddTask);
            RemoveTaskCommand = new RelayCommand<WorkTask>(RemoveTask);
            AddTravelCommand = new RelayCommand(AddTravel);
            RemoveTravelCommand = new RelayCommand<Travel>(RemoveTravel);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(() => CloseWindow());
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !_isNew);
            OpenVehicleEditCommand = new RelayCommand(OpenVehicleEdit, () => SelectedVehicle != null);
            OpenAddTaskCommand = new RelayCommand(OpenAddTask);

            _ = InitializeAsync(workOrderId);
        }

        // Called when search text changes
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
                _logger.LogInfo($"Filter result: {FilteredAccessories.Count} accessories (no filter)");
                return;
            }

            var lowerSearch = searchText.ToLowerInvariant();
            var filtered = AvailableAccessories
                .Where(a => a.Name?.ToLowerInvariant().Contains(lowerSearch) == true ||
                            a.PartNumber?.ToLowerInvariant().Contains(lowerSearch) == true)
                .ToList();

            FilteredAccessories = new ObservableCollection<Accessory>(filtered);
            _logger.LogInfo($"Filter result: {FilteredAccessories.Count} accessories matched");
        }

        

        // ========== ADD TRAVEL ==========
        private void AddTravel()
        {
            _logger.LogInfo(LocalizationManager.Instance["AddTravelCalled"]);
            var dialog = new TravelDialog();
            // ? Set the owner to the current MainWindow
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            // ? Set startup location (already in XAML, but ensure it's set)
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
                    System.Windows.MessageBox.Show($"Error adding travel: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            else
            {
                _logger.LogInfo(LocalizationManager.Instance["AddTravelCancelledByUser"]);
            }
        }

        // ========== REMOVE TRAVEL ==========
        private void RemoveTravel(Travel? travel)
        {
            if (travel == null) return;
            _logger.LogInfo($"Removing travel: Destination={travel.Destination}, Date={travel.TravelDate:yyyy-MM-dd}");
            Travels.Remove(travel);
            WorkOrder.Travels.Remove(travel);
        }

        // ========== OPEN ADD TASK (MANAGE TASKS) ==========
        private async void OpenAddTask()
        {
            try
            {
                var logger = App.GetService<ILoggingService>();
                var vm = new AccessoryManagementViewModel(_unitOfWork, logger);
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
                    // Clear search text so the newly added accessory appears
                    AccessorySearchText = "";
                    // Ensure the refresh happens on the UI thread
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
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task RefreshAccessoriesAsync()
        {
            _logger.LogInfo(LocalizationManager.Instance["RefreshingAccessoriesList"]);
            // Run on the UI thread to avoid cross-thread collection issues
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var accessories = await _unitOfWork.Accessories.GetAllAsync();
                AvailableAccessories = new ObservableCollection<Accessory>(accessories.OrderBy(a => a.Name));
                FilterAccessories(AccessorySearchText);
                _logger.LogInfo($"Loaded {AvailableAccessories.Count} accessories, filtered to {FilteredAccessories.Count}");
            });
        }
        // ========== OPEN VEHICLE EDIT ==========
        private async void OpenVehicleEdit()
        {
            if (SelectedVehicle == null) return;

            _logger.LogInfo($"Opening vehicle edit for chassis {SelectedVehicle.ChassisNumber}");
            var editWindow = new VehicleEditWindow();
            var viewModel = new VehicleEditViewModel(_unitOfWork, SelectedVehicle);
            editWindow.DataContext = viewModel;
            editWindow.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

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

        // ========== INITIALIZE ==========
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
                FilterAccessories(""); // initialize filtered list with all items
                _logger.LogInfo($"Loaded {AvailableAccessories.Count} accessories");

                if (!_isNew)
                {
                    var repo = (WorkOrderRepository)_unitOfWork.WorkOrders;
                    WorkOrder = await repo.GetWithDetailsAsync(workOrderId);
                    if (WorkOrder == null)
                    {
                        _logger.LogError($"WorkOrder with ID {workOrderId} not found");
                        System.Windows.MessageBox.Show($"Work order #{workOrderId} not found.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                    _logger.LogInfo($"Loaded {Tasks.Count} tasks and {Travels.Count} travels for work order");
                }
                else
                {
                    WorkOrder = new WorkOrder
                    {
                        OrderDate = DateTime.Today,
                        Status = WorkStatus.Planned,
                        OrderType = OrderType.PSA_Contract
                    };
                    Tasks = new ObservableCollection<WorkTask>();
                    Travels = new ObservableCollection<Travel>();
                    _logger.LogInfo(LocalizationManager.Instance["CreatedNewWorkOrder"]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(LocalizationManager.Instance["InitializeAsyncFailed"], ex);
                System.Windows.MessageBox.Show($"Error initializing: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                CloseWindow();
            }
        }

        // ========== SEARCH VEHICLE ==========
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
                var result = System.Windows.MessageBox.Show($"Vehicle with chassis {ChassisSearch} not found. Create new vehicle?",
                    "Create Vehicle", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                if (result != System.Windows.MessageBoxResult.Yes) return;

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
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["NoClientExists"], "No Client", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                }

                if (SelectedClient.Id <= 0)
                {
                    _logger.LogError($"Selected client has invalid ID {SelectedClient.Id}");
                    System.Windows.MessageBox.Show(LocalizationManager.Instance["SelectedClientNotSavedYet"], "Invalid Client", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                var existingClient = await _unitOfWork.Clients.GetByIdAsync(SelectedClient.Id);
                if (existingClient == null)
                {
                    _logger.LogError($"Client with ID {SelectedClient.Id} does not exist in database.");
                    System.Windows.MessageBox.Show(LocalizationManager.Instance["SelectedClientNoLongerExists"], "Client Not Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // Helper method to refresh the Vehicles collection
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

        // ========== ADD TASK ==========
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
                Price = SelectedAccessory.Price,
                EstimatedMinutes = SelectedAccessory.Time
            };
            Tasks.Add(task);
            WorkOrder.WorkTasks.Add(task);
            _logger.LogInfo($"Added task: Accessory '{SelectedAccessory.Name}', Type '{SelectedAccessory.TaskType}', Quantity {Quantity}, Price {SelectedAccessory.Price}");
        }

        // ========== REMOVE TASK ==========
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

        // ========== SAVE ==========
        private async Task SaveAsync()
        {
            try
            {
                _logger.LogInfo(LocalizationManager.Instance["SaveAsyncStarted"]);

                if (SelectedVehicle == null)
                {
                    _logger.LogWarning(LocalizationManager.Instance["SaveAttemptedWithoutVehicleSelected"]);
                    System.Windows.MessageBox.Show(LocalizationManager.Instance["PleaseSelectVehicle"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                WorkOrder.VehicleId = SelectedVehicle.Id;

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
                System.Windows.MessageBox.Show($"Error saving: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // ========== DELETE ==========
        private async Task DeleteAsync()
        {
            if (_isNew) return;

            _logger.LogInfo($"DeleteAsync called for work order #{WorkOrder.Id}");
            var result = System.Windows.MessageBox.Show($"Delete work order #{WorkOrder.Id}? This will also delete all associated tasks and travels.",
                "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;

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
                System.Windows.MessageBox.Show($"Error deleting: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // ========== CLOSE WINDOW ==========
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