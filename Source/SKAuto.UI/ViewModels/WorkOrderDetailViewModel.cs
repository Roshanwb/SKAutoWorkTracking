using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Repository;
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

        // For enum dropdowns
        public Array OrderTypeValues => Enum.GetValues(typeof(OrderType));
        public Array WorkStatusValues => Enum.GetValues(typeof(WorkStatus));

        public IAsyncRelayCommand SearchVehicleCommand { get; }
        public IRelayCommand AddTaskCommand { get; }
        public IRelayCommand<WorkTask> RemoveTaskCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }

        public WorkOrderDetailViewModel(IUnitOfWork unitOfWork, int workOrderId = 0)
        {
            _unitOfWork = unitOfWork;
            _isNew = workOrderId == 0;

            SearchVehicleCommand = new AsyncRelayCommand(SearchVehicleAsync);
            AddTaskCommand = new RelayCommand(AddTask);
            RemoveTaskCommand = new RelayCommand<WorkTask>(RemoveTask);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(() => CloseWindow());
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !_isNew);

            InitializeAsync(workOrderId).ConfigureAwait(false);
        }

        private async Task InitializeAsync(int workOrderId)
        {
            try
            {
                var clients = await _unitOfWork.Clients.GetAllAsync();
                Clients = new ObservableCollection<Client>(clients);

                var vehicles = await _unitOfWork.Vehicles.GetAllAsync();
                Vehicles = new ObservableCollection<Vehicle>(vehicles);

                var accessories = await _unitOfWork.Accessories.GetAllAsync();
                AvailableAccessories = new ObservableCollection<Accessory>(accessories);

                if (!_isNew)
                {
                    var repo = (WorkOrderRepository)_unitOfWork.WorkOrders;
                    WorkOrder = await repo.GetWithDetailsAsync(workOrderId);
                    SelectedVehicle = WorkOrder.Vehicle;
                    if (SelectedVehicle != null)
                    {
                        ChassisSearch = SelectedVehicle.ChassisNumber;
                        // Automatically set the client from the vehicle
                        SelectedClient = SelectedVehicle.Client;
                    }
                    Tasks = new ObservableCollection<WorkTask>(WorkOrder.WorkTasks);
                }
                else
                {
                    WorkOrder = new WorkOrder
                    {
                        OrderDate = DateTime.Today,
                        Status = WorkStatus.Planned,
                        OrderType = OrderType.Direct_Fitting
                    };
                    Tasks = new ObservableCollection<WorkTask>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SearchVehicleAsync()
        {
            if (string.IsNullOrWhiteSpace(ChassisSearch)) return;

            try
            {
                // Use the repository method that includes the client
                var repo = (VehicleRepository)_unitOfWork.Vehicles;
                var vehicle = await repo.GetByChassisWithClientAsync(ChassisSearch);
                if (vehicle != null)
                {
                    SelectedVehicle = vehicle;
                    // Client is automatically set via the vehicle's navigation property
                    SelectedClient = vehicle.Client;
                }
                else
                {
                    var result = MessageBox.Show($"Vehicle with chassis {ChassisSearch} not found. Create new vehicle?",
                        "Create Vehicle", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        // Need a client for the new vehicle – we'll use the currently selected client
                        // or prompt the user to select one. For simplicity, we'll use the first client.
                        if (SelectedClient == null && Clients.Any())
                        {
                            SelectedClient = Clients.First();
                        }
                        if (SelectedClient == null)
                        {
                            MessageBox.Show("Please select a client first.", "No Client", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        vehicle = new Vehicle
                        {
                            ChassisNumber = ChassisSearch,
                            Model = "Unknown",
                            ClientId = SelectedClient.Id,
                            IsActive = true
                        };
                        await _unitOfWork.Vehicles.AddAsync(vehicle);
                        await _unitOfWork.CompleteAsync();
                        SelectedVehicle = vehicle;
                        // Refresh vehicles list
                        var allVehicles = await _unitOfWork.Vehicles.GetAllAsync();
                        Vehicles = new ObservableCollection<Vehicle>(allVehicles);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching vehicle: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        partial void OnSelectedVehicleChanged(Vehicle? value)
        {
            if (value != null)
            {
                // Client is automatically determined from the vehicle
                SelectedClient = value.Client;
                // WorkOrder.VehicleId is set automatically when we assign SelectedVehicle
                // because the ViewModel sets the ID in the property setter? Actually we need to update WorkOrder.VehicleId.
                if (WorkOrder != null)
                {
                    WorkOrder.VehicleId = value.Id;
                }
            }
        }

        partial void OnSelectedClientChanged(Client? value)
        {
            // Client is read-only when vehicle is selected; this is just for display.
            // We don't set anything on WorkOrder because client is determined by vehicle.
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
        }

        private void RemoveTask(WorkTask? task)
        {
            if (task != null)
            {
                Tasks.Remove(task);
                WorkOrder.WorkTasks.Remove(task);
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                // Validate required fields
                if (WorkOrder.VehicleId == 0 || SelectedVehicle == null)
                {
                    MessageBox.Show("Please select a vehicle.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Client is automatically determined by vehicle, so no separate check needed

                WorkOrder.CalculateTotal();

                if (_isNew)
                {
                    await _unitOfWork.WorkOrders.AddAsync(WorkOrder);
                }
                else
                {
                    await _unitOfWork.WorkOrders.UpdateAsync(WorkOrder);
                }

                await _unitOfWork.CompleteAsync();
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteAsync()
        {
            if (_isNew) return;

            var result = MessageBox.Show($"Delete work order #{WorkOrder.Id}? This will also delete all associated tasks and travels.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _unitOfWork.WorkOrders.DeleteAsync(WorkOrder);
                await _unitOfWork.CompleteAsync();
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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