using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.DTOs;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows; // for MessageBox

namespace SKAuto.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;

        [ObservableProperty]
        private ObservableCollection<WorkOrderDto> _todayWorkOrders = new();

        [ObservableProperty]
        private WorkOrderDto? _selectedWorkOrder;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private DateTime _selectedDate = DateTime.Today;

        [ObservableProperty]
        private int _totalOrdersToday;

        [ObservableProperty]
        private decimal _totalRevenueToday;

        [ObservableProperty]
        private ObservableCollection<WorkOrderDto> _undoneOrders = new();

        [ObservableProperty]
        private WorkOrderDto? _selectedUndoneOrder;

        [ObservableProperty]
        private DateTime _rescheduleDate = DateTime.Today;

        public IAsyncRelayCommand LoadTodayWorkCommand { get; }
        public IRelayCommand CreateWorkOrderCommand { get; }
        public IRelayCommand ImportDataCommand { get; }
        public IRelayCommand OpenClientManagementCommand { get; }
        public IRelayCommand OpenVehicleManagementCommand { get; }
        public IAsyncRelayCommand<int> EditWorkOrderCommand { get; }
        public IAsyncRelayCommand GenerateReportsCommand { get; }
        public IAsyncRelayCommand<string> UpdateStatusCommand { get; }
        public IAsyncRelayCommand RescheduleCommand { get; }
        public IAsyncRelayCommand EditClientCommand { get; }
        public IAsyncRelayCommand OpenWorkOrderDetailCommand { get; }
        public IAsyncRelayCommand<int> MarkDoneCommand { get; }
        public IAsyncRelayCommand DeleteWorkOrderCommand { get; }

        public MainViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

            LoadTodayWorkCommand = new AsyncRelayCommand(LoadTodayWorkAsync);
            CreateWorkOrderCommand = new RelayCommand(CreateWorkOrder);
            ImportDataCommand = new RelayCommand(OpenImport);
            GenerateReportsCommand = new AsyncRelayCommand(GenerateReportsAsync);
            UpdateStatusCommand = new AsyncRelayCommand<string>(UpdateStatusAsync);
            RescheduleCommand = new AsyncRelayCommand(RescheduleAsync);
            EditClientCommand = new AsyncRelayCommand(EditClient, () => SelectedWorkOrder != null);
            OpenWorkOrderDetailCommand = new AsyncRelayCommand(OpenWorkOrderDetail, () => SelectedWorkOrder != null);
            MarkDoneCommand = new AsyncRelayCommand<int>(MarkDoneAsync);
            DeleteWorkOrderCommand = new AsyncRelayCommand(DeleteWorkOrderAsync, () => SelectedWorkOrder != null);
            OpenClientManagementCommand = new RelayCommand(OpenClientManagement);
            OpenVehicleManagementCommand = new RelayCommand(OpenVehicleManagement);
            EditWorkOrderCommand = new AsyncRelayCommand<int>(EditWorkOrderAsync);

            LoadTodayWorkCommand.Execute(null);
        }

        private async Task LoadTodayWorkAsync()
        {
            await LoadWorkForDateAsync(SelectedDate);
        }

        private async Task LoadWorkForDateAsync(DateTime date)
        {
            try
            {
                StatusMessage = $"Loading work for {date:dd/MM/yyyy}...";

                var workOrderRepo = (Data.Repository.WorkOrderRepository)_unitOfWork.WorkOrders;

                var orders = await workOrderRepo.GetDailyWorkOrdersAsync(date);
                TodayWorkOrders = new ObservableCollection<WorkOrderDto>(orders);

                var summary = await workOrderRepo.GetDailySummaryAsync(date);
                TotalOrdersToday = summary.TotalWorkOrders;
                TotalRevenueToday = summary.TotalRevenue;

                var allUndone = await _unitOfWork.WorkOrders.FindAsync(w =>
                    w.OrderDate < DateTime.Today && w.Status != WorkStatus.Done);
                UndoneOrders = new ObservableCollection<WorkOrderDto>(
                    allUndone.Select(WorkOrderDto.FromEntity));

                StatusMessage = $"Loaded {TodayWorkOrders.Count} work orders";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading work: {ex.Message}";
            }
        }

        partial void OnSelectedDateChanged(DateTime value)
        {
            LoadTodayWorkCommand.Execute(null);
        }

        private void CreateWorkOrder()
        {
            var detailVM = new WorkOrderDetailViewModel(_unitOfWork, 0);
            var window = new WorkOrderDetailWindow { DataContext = detailVM };
            if (window.ShowDialog() == true)
            {
                LoadTodayWorkCommand.Execute(null);
            }
        }

        private void OpenImport()
        {
            var importVM = new ImportViewModel(_unitOfWork);
            var importView = new ImportView { DataContext = importVM };
            importView.ShowDialog();
            LoadTodayWorkCommand.Execute(null);
        }

        private async Task GenerateReportsAsync()
        {
            await Task.CompletedTask;
        }

        private async Task UpdateStatusAsync(string? statusString)
        {
            if (SelectedWorkOrder == null || string.IsNullOrEmpty(statusString)) return;

            if (Enum.TryParse<WorkStatus>(statusString, out var status))
            {
                try
                {
                    var workOrder = await _unitOfWork.WorkOrders.GetByIdAsync(SelectedWorkOrder.Id);
                    if (workOrder != null)
                    {
                        workOrder.Status = status;

                        if (status == WorkStatus.Done)
                            workOrder.CompletedDate = DateTime.Now;

                        await _unitOfWork.WorkOrders.UpdateAsync(workOrder);
                        await _unitOfWork.CompleteAsync();

                        StatusMessage = $"Updated order {SelectedWorkOrder.Id} to {status}";
                        await LoadWorkForDateAsync(SelectedDate);
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error updating status: {ex.Message}";
                }
            }
        }

        private async Task RescheduleAsync()
        {
            if (SelectedUndoneOrder == null) return;

            try
            {
                var order = await _unitOfWork.WorkOrders.GetByIdAsync(SelectedUndoneOrder.Id);
                if (order != null)
                {
                    order.OrderDate = RescheduleDate;
                    await _unitOfWork.WorkOrders.UpdateAsync(order);
                    await _unitOfWork.CompleteAsync();
                    StatusMessage = $"Rescheduled order {SelectedUndoneOrder.Id} to {RescheduleDate:dd/MM/yyyy}";
                    await LoadWorkForDateAsync(SelectedDate);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error rescheduling: {ex.Message}";
            }
        }

        private async Task EditClient()
        {
            if (SelectedWorkOrder == null) return;

            try
            {
                var client = await _unitOfWork.Clients.GetByIdAsync(SelectedWorkOrder.ClientId);
                if (client != null)
                {
                    var vm = new ClientEditViewModel(_unitOfWork, client);
                    var win = new ClientEditWindow { DataContext = vm };
                    if (win.ShowDialog() == true)
                    {
                        await LoadWorkForDateAsync(SelectedDate);
                        StatusMessage = "Client updated successfully";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error editing client: {ex.Message}";
            }
        }

        private async Task OpenWorkOrderDetail()
        {
            if (SelectedWorkOrder == null) return;

            var detailVM = new WorkOrderDetailViewModel(_unitOfWork, SelectedWorkOrder.Id);
            var window = new WorkOrderDetailWindow { DataContext = detailVM };
            if (window.ShowDialog() == true)
            {
                await LoadWorkForDateAsync(SelectedDate);
            }
        }

        private async Task MarkDoneAsync(int workOrderId)
        {
            try
            {
                var workOrderRepo = (Data.Repository.WorkOrderRepository)_unitOfWork.WorkOrders;
                var order = await workOrderRepo.GetWithDetailsAsync(workOrderId);

                if (order != null)
                {
                    order.Status = WorkStatus.Done;
                    order.CompletedDate = DateTime.Now;
                    foreach (var task in order.WorkTasks)
                        task.TaskStatus = WorkStatus.Done;

                    await _unitOfWork.WorkOrders.UpdateAsync(order);
                    await _unitOfWork.CompleteAsync();
                    StatusMessage = $"Marked order {workOrderId} as Done";
                    await LoadWorkForDateAsync(SelectedDate);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error marking as done: {ex.Message}";
            }
        }

        private async Task DeleteWorkOrderAsync()
        {
            if (SelectedWorkOrder == null) return;

            var result = MessageBox.Show($"Delete work order #{SelectedWorkOrder.Id}? This action cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                var order = await _unitOfWork.WorkOrders.GetByIdAsync(SelectedWorkOrder.Id);
                if (order != null)
                {
                    await _unitOfWork.WorkOrders.DeleteAsync(order);
                    await _unitOfWork.CompleteAsync();
                    StatusMessage = $"Work order {SelectedWorkOrder.Id} deleted.";
                    await LoadWorkForDateAsync(SelectedDate);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting: {ex.Message}";
            }
        }

        partial void OnSelectedWorkOrderChanged(WorkOrderDto? value)
        {
            DeleteWorkOrderCommand?.NotifyCanExecuteChanged();
            EditClientCommand?.NotifyCanExecuteChanged();
            OpenWorkOrderDetailCommand?.NotifyCanExecuteChanged();
        }

        private void OpenClientManagement()
        {
            var vm = new ClientManagementViewModel(_unitOfWork);
            var win = new ClientManagementView { DataContext = vm };
            win.ShowDialog();
            // Refresh main grid in case client names changed
            LoadTodayWorkCommand.Execute(null);
        }

        private void OpenVehicleManagement()
        {
            var vm = new VehicleManagementViewModel(_unitOfWork);
            var win = new VehicleManagementView { DataContext = vm };
            win.ShowDialog();
            // Vehicles might affect work orders? Not directly, but could be needed.
            LoadTodayWorkCommand.Execute(null);
        }

        private async Task EditWorkOrderAsync(int workOrderId)
        {
            var detailVM = new WorkOrderDetailViewModel(_unitOfWork, workOrderId);
            var window = new WorkOrderDetailWindow { DataContext = detailVM };
            if (window.ShowDialog() == true)
            {
                await LoadWorkForDateAsync(SelectedDate);
            }
        }
    }
}