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

        [ObservableProperty]
        private Accessory? _selectedAccessory;

        [ObservableProperty]
        private int _quantity = 1;

        [ObservableProperty]
        private ObservableCollection<WorkTask> _tasks = new();

        public Array OrderTypeValues => Enum.GetValues(typeof(OrderType));
        public Array WorkStatusValues => Enum.GetValues(typeof(WorkStatus));

        public IAsyncRelayCommand SearchVehicleCommand { get; }
        public IRelayCommand AddTaskCommand { get; }
        public IRelayCommand<WorkTask> RemoveTaskCommand { get; }
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
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(() => CloseWindow());
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !_isNew);
            OpenVehicleEditCommand = new RelayCommand(OpenVehicleEdit, () => SelectedVehicle != null);
            OpenAddTaskCommand = new RelayCommand(OpenAddTask);

            // Fire-and-forget initialization – we'll log any errors
            _ = InitializeAsync(workOrderId);
        }


        private void OpenAddTask()
        {
            try
            {
                var logger = App.GetService<ILoggingService>();
                var vm = new AccessoryManagementViewModel(_unitOfWork, logger);
                var view = new AccessoryManagementView { DataContext = vm };

                // Create a window to host the view
                var window = new Window
                {
                    Title = "Manage Tasks",
                    Content = view,
                    Width = 900,
                    Height = 700,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                };

                if (window.ShowDialog() == true)
                {
                    _ = RefreshAccessoriesAsync();
                }
            }
            catch (Exception ex)
            {
                var logger = App.GetService<ILoggingService>();
                logger?.LogError("Failed to open accessory management", ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshAccessoriesAsync()
        {
            var accessories = await _unitOfWork.Accessories.GetAllAsync();
            AvailableAccessories = new ObservableCollection<Accessory>(accessories.OrderBy(a => a.Name));
        }


        private async void OpenVehicleEdit()
        {
            if (SelectedVehicle == null) return;

            var editWindow = new VehicleEditWindow();
            var viewModel = new VehicleEditViewModel(_unitOfWork, SelectedVehicle);
            editWindow.DataContext = viewModel;
            editWindow.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            if (editWindow.ShowDialog() == true)
            {
                // Refresh the selected vehicle with updated data from database
                var refreshedVehicle = await _unitOfWork.Vehicles.GetByIdAsync(SelectedVehicle.Id);
                if (refreshedVehicle != null)
                {
                    SelectedVehicle = refreshedVehicle;
                    // Also refresh the vehicle's client reference
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
                _logger.LogInfo("InitializeAsync started");

                var clients = await _unitOfWork.Clients.GetAllAsync();
                Clients = new ObservableCollection<Client>(clients);
                _logger.LogInfo($"Loaded {Clients.Count} clients");

                var vehicles = await _unitOfWork.Vehicles.GetAllAsync();
                Vehicles = new ObservableCollection<Vehicle>(vehicles);
                _logger.LogInfo($"Loaded {Vehicles.Count} vehicles");

                var accessories = await _unitOfWork.Accessories.GetAllAsync();
                AvailableAccessories = new ObservableCollection<Accessory>(accessories);
                _logger.LogInfo($"Loaded {AvailableAccessories.Count} accessories");

                if (!_isNew)
                {
                    var repo = (WorkOrderRepository)_unitOfWork.WorkOrders;
                    WorkOrder = await repo.GetWithDetailsAsync(workOrderId);
                    if (WorkOrder == null)
                    {
                        _logger.LogError($"WorkOrder with ID {workOrderId} not found");
                        MessageBox.Show($"Work order #{workOrderId} not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    _logger.LogInfo($"Loaded {Tasks.Count} tasks for work order");
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
                    _logger.LogInfo("Created new work order");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("InitializeAsync failed", ex);
                MessageBox.Show($"Error initializing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var result = MessageBox.Show($"Vehicle with chassis {ChassisSearch} not found. Create new vehicle?",
                    "Create Vehicle", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                // ---- VALIDATE CLIENT ----
                if (SelectedClient == null)
                {
                    // Try to auto-select first client
                    if (Clients.Any())
                    {
                        SelectedClient = Clients.First();
                        _logger.LogInfo($"Auto-selected client: {SelectedClient.Name} (ID {SelectedClient.Id})");
                    }
                    else
                    {
                        _logger.LogWarning("No client available – cannot create vehicle");
                        MessageBox.Show("No client exists. Please create a client first using 'Manage Clients'.", "No Client", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Ensure the client has a valid ID (already persisted)
                if (SelectedClient.Id <= 0)
                {
                    _logger.LogError($"Selected client has invalid ID {SelectedClient.Id}. Client must be saved before creating a vehicle.");
                    MessageBox.Show("The selected client is not saved yet. Please save the client first, then try again.", "Invalid Client", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Optional: double-check that the client really exists in the database
                var existingClient = await _unitOfWork.Clients.GetByIdAsync(SelectedClient.Id);
                if (existingClient == null)
                {
                    _logger.LogError($"Client with ID {SelectedClient.Id} does not exist in database.");
                    MessageBox.Show("The selected client no longer exists. Please refresh clients and try again.", "Client Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
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
                await RefreshVehiclesList(); // refresh local collection
                _logger.LogInfo($"Created new vehicle: {vehicle.ChassisNumber} for client {existingClient.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching/creating vehicle: {ChassisSearch}", ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                // Update the vehicle's client when client is manually changed
                SelectedVehicle.ClientId = value.Id;
                SelectedVehicle.Client = value;
                _logger.LogInfo($"Updated vehicle client to {value.Name} (ID {value.Id})");
            }
            if (value != null)
                _logger.LogInfo($"Selected client changed to {value.Name} (ID {value.Id})");
        }

        private void AddTask()
        {
            if (SelectedAccessory == null) return;

            var task = new WorkTask
            {
                WorkOrderId = WorkOrder.Id,
                AccessoryId = SelectedAccessory.Id,
                Accessory = SelectedAccessory,
                Quantity = Quantity,
                TaskType = TaskType.Fit,
                TaskStatus = WorkStatus.Planned,
                Price = SelectedAccessory.Price,
                EstimatedMinutes = SelectedAccessory.Time
            };
            Tasks.Add(task);
            WorkOrder.WorkTasks.Add(task);
            _logger.LogInfo($"Added task: Accessory '{SelectedAccessory.Name}', Quantity {Quantity}, Price {SelectedAccessory.Price}");
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

        private async Task SaveAsync()
        {
            try
            {
                _logger.LogInfo("SaveAsync started");

                if (SelectedVehicle == null)
                {
                    _logger.LogWarning("Save attempted without a vehicle selected");
                    MessageBox.Show("Please select a vehicle.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Ensure WorkOrder has the correct VehicleId
                WorkOrder.VehicleId = SelectedVehicle.Id;

                // If client was manually changed, update the vehicle's client
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
                _logger.LogInfo("Work order saved successfully");
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                _logger.LogError("SaveAsync failed", ex);
                MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteAsync()
        {
            if (_isNew) return;

            _logger.LogInfo($"DeleteAsync called for work order #{WorkOrder.Id}");
            var result = MessageBox.Show($"Delete work order #{WorkOrder.Id}? This will also delete all associated tasks and travels.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

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
                MessageBox.Show($"Error deleting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow(bool success = false)
        {
            _logger.LogInfo($"Closing window, success={success}");
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