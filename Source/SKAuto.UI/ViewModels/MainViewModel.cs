using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.DTOs;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.UI.Views;
using SKAuto.Data.Repository; 


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
        public IAsyncRelayCommand GenerateReportsCommand { get; }
        public IAsyncRelayCommand<WorkStatus?> UpdateStatusCommand { get; }  // Fixed generic type
        public IAsyncRelayCommand RescheduleCommand { get; }
        public IAsyncRelayCommand EditClientCommand { get; }
        public IAsyncRelayCommand OpenWorkOrderDetailCommand { get; }
        public IAsyncRelayCommand<int> MarkDoneCommand { get; }

        public MainViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

            LoadTodayWorkCommand = new AsyncRelayCommand(LoadTodayWorkAsync);
            CreateWorkOrderCommand = new RelayCommand(CreateWorkOrder);
            ImportDataCommand = new RelayCommand(OpenImport);
            GenerateReportsCommand = new AsyncRelayCommand(GenerateReportsAsync);
            UpdateStatusCommand = new AsyncRelayCommand<WorkStatus?>(UpdateStatusAsync);  // Fixed
            RescheduleCommand = new AsyncRelayCommand(RescheduleAsync);
            EditClientCommand = new AsyncRelayCommand(EditClient, () => SelectedWorkOrder != null);
            OpenWorkOrderDetailCommand = new AsyncRelayCommand(OpenWorkOrderDetail, () => SelectedWorkOrder != null);
            MarkDoneCommand = new AsyncRelayCommand<int>(MarkDoneAsync);

            // Load today's work on startup
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

                // Cast to WorkOrderRepository to access specialized methods
                var workOrderRepo = (WorkOrderRepository)_unitOfWork.WorkOrders;

                var orders = await workOrderRepo.GetDailyWorkOrdersAsync(date);
                TodayWorkOrders = new ObservableCollection<WorkOrderDto>(orders);

                // Get summary for the date
                var summary = await workOrderRepo.GetDailySummaryAsync(date);
                TotalOrdersToday = summary.TotalWorkOrders;
                TotalRevenueToday = summary.TotalRevenue;

                // Load undone orders (OrderDate < today && Status != Done)
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
            // Open work order creation dialog
            // You can implement this later
        }
                private void OpenImport()
        {
            var importVM = new ImportViewModel(_unitOfWork); // Create ViewModel with DI
            var importView = new ImportView
            {
                DataContext = importVM // <-- CRITICAL: Set DataContext
            };
            importView.ShowDialog();
            LoadTodayWorkCommand.Execute(null); // refresh main grid
        }

        private async Task GenerateReportsAsync()
        {
            // Implement later
            await Task.CompletedTask;
        }

        private async Task UpdateStatusAsync(WorkStatus? status)
        {
            if (SelectedWorkOrder == null || status == null) return;

            try
            {
                var workOrder = await _unitOfWork.WorkOrders.GetByIdAsync(SelectedWorkOrder.Id);
                if (workOrder != null)
                {
                    workOrder.Status = status.Value;

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
                        await LoadWorkForDateAsync(SelectedDate); // refresh client name in grid
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

            // Cast to WorkOrderRepository to access specialized methods
            var workOrderRepo = (WorkOrderRepository)_unitOfWork.WorkOrders;
            var order = await workOrderRepo.GetWithDetailsAsync(SelectedWorkOrder.Id);

            if (order != null)
            {
                var detailVM = new WorkOrderDetailViewModel(_unitOfWork, order);
                var window = new WorkOrderDetailWindow { DataContext = detailVM };
                if (window.ShowDialog() == true)
                {
                    await LoadWorkForDateAsync(SelectedDate); // refresh
                }
            }
        }

        private async Task MarkDoneAsync(int workOrderId)
        {
            try
            {
                // Cast to WorkOrderRepository to access specialized methods
                var workOrderRepo = (WorkOrderRepository)_unitOfWork.WorkOrders;
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
    }
}