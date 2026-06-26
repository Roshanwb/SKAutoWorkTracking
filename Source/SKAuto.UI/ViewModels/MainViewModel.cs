using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SKAuto.Core.DTOs;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Core.Services;
using SKAuto.Data.Repository;
using SKAuto.Export.Pdf;
using SKAuto.UI.Views;
using SKAuto.Core.Enums;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SKAuto.Core.Entities; // for MessageBox

namespace SKAuto.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfigurationService _configService;
        private readonly ILoggingService _logger;

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

        [ObservableProperty]
        private WeeklySummaryDto _weeklySummary = new();

        [ObservableProperty]
        private string _syncStatus;

        [ObservableProperty]
        private User? _currentUser;

        [ObservableProperty]
        private bool _isAdmin;

        public IRelayCommand PreviousDayCommand { get; }
        public IRelayCommand NextDayCommand { get; }
        public IRelayCommand PreviousWeekCommand { get; }
        public IRelayCommand NextWeekCommand { get; }
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
        public ICommand ShowAccessoryManagementCommand { get; }
        public IRelayCommand OpenBackupCommand { get; }
        public IRelayCommand OpenReportsCommand { get; }
        public IRelayCommand OpenPriceUpdateCommand { get; }
        public IRelayCommand OpenHelpCommand { get; }
        public IRelayCommand OpenAboutCommand { get; }
        public IRelayCommand OpenDriveSettingsCommand { get; }
        public IRelayCommand SyncNowCommand { get; }
        public IRelayCommand ShowUserManagementCommand { get; }
        public IRelayCommand ChangePasswordCommand { get; }

        public MainViewModel(IUnitOfWork unitOfWork, IConfigurationService configService, ILoggingService logger)
        {
            _unitOfWork = unitOfWork;
            _configService = configService;
            _logger = logger;

            LoadTodayWorkCommand = new AsyncRelayCommand(LoadTodayWorkAsync);
            CreateWorkOrderCommand = new RelayCommand(CreateWorkOrder);
            ImportDataCommand = new RelayCommand(OpenImport);
            //GenerateReportsCommand = new AsyncRelayCommand(GenerateReportsAsync);
            UpdateStatusCommand = new AsyncRelayCommand<string>(UpdateStatusAsync);
            RescheduleCommand = new AsyncRelayCommand(RescheduleAsync);
            EditClientCommand = new AsyncRelayCommand(EditClient, () => SelectedWorkOrder != null);
            OpenWorkOrderDetailCommand = new AsyncRelayCommand(OpenWorkOrderDetail, () => SelectedWorkOrder != null);
            MarkDoneCommand = new AsyncRelayCommand<int>(MarkDoneAsync);
            DeleteWorkOrderCommand = new AsyncRelayCommand(DeleteWorkOrderAsync, () => SelectedWorkOrder != null);
            OpenClientManagementCommand = new RelayCommand(OpenClientManagement);
            OpenVehicleManagementCommand = new RelayCommand(OpenVehicleManagement);
            EditWorkOrderCommand = new AsyncRelayCommand<int>(EditWorkOrderAsync);
            ShowAccessoryManagementCommand = new RelayCommand(ShowAccessoryManagement);
            PreviousDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(-1));
            NextDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(1));
            PreviousWeekCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(-7));
            NextWeekCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(7));
            OpenBackupCommand = new RelayCommand(OpenBackup);
            OpenDriveSettingsCommand = new RelayCommand(OpenDriveSettings);
            SyncNowCommand = new RelayCommand(async () => await SyncNowAsync());
            OpenReportsCommand = new RelayCommand(OpenReports);
            OpenPriceUpdateCommand = new RelayCommand(OpenPriceUpdate);
            OpenHelpCommand = new RelayCommand(OpenHelp);
            OpenAboutCommand = new RelayCommand(OpenAbout);
            ShowUserManagementCommand = new RelayCommand(ShowUserManagement, () => IsAdmin);
            ChangePasswordCommand = new RelayCommand(ChangePassword);

            LoadTodayWorkCommand.Execute(null);
        }

        private void ShowUserManagement()
        {
            var logger = App.GetService<ILoggingService>();
            var vm = new UserManagementViewModel(_unitOfWork, logger, CurrentUser);
            var win = new UserManagementView { DataContext = vm };
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }

        private void ChangePassword()
        {
            var logger = App.GetService<ILoggingService>();
            var vm = new UserEditViewModel(_unitOfWork, logger, CurrentUser, null, UserEditMode.ChangePassword);
            var win = new UserEditView(vm);
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }

        [RelayCommand]
        private void OpenSettings()
        {
            var settingsWindow = new SettingsView();
            settingsWindow.DataContext = new SettingsViewModel(_configService);
            settingsWindow.Owner = Application.Current.MainWindow;
            settingsWindow.Show();
        }

        public void SetCurrentUser(User user)
        {
            CurrentUser = user;
            IsAdmin = user?.Role == UserRole.Admin;
            _logger.LogInfo($"User set: {user?.Username}, IsAdmin: {IsAdmin}");
        }

        private void OpenReports()
        {
            var vm = new ReportsViewModel(
                _unitOfWork,
                App.GetService<IExportService>(),
                App.GetService<PdfReportGenerator>(),
                App.GetService<ILoggingService>());
            var win = new ReportsView { DataContext = vm };
            win.Show();
        }

        private void OpenHelp()
        {
            var helpWindow = new HelpView();
            helpWindow.Owner = Application.Current.MainWindow;
            helpWindow.ShowDialog();
        }

        private void OpenAbout()
        {
            MessageBox.Show("SKAuto Work Tracking System\nVersion 1.0.0\nDeveloped by SK Auto\n2026 - Insights®",
                            "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private async void OpenPriceUpdate()
        {
            var logger = App.GetService<ILoggingService>();
            var vm = new PriceUpdateViewModel(_unitOfWork, logger);
            var window = new PriceUpdateView { DataContext = vm, Owner = Application.Current.MainWindow };
            var result = window.ShowDialog();
            if (result == true)
            {
                // Refresh today's work orders to show updated prices
                await LoadTodayWorkAsync();
                StatusMessage = "Prices updated and grid refreshed.";
            }
        }

        private void OpenDriveSettings()
        {
            var driveService = App.GetService<IGoogleDriveService>();
            var config = App.GetService<IConfigurationService>();
            var backupService = App.GetService<IBackupService>();
            var logger = App.GetService<ILoggingService>();

            var vm = new GoogleDriveSettingsViewModel(driveService, config, backupService, logger);
            var win = new GoogleDriveSettingsView { DataContext = vm };
            win.Show();
            UpdateSyncStatus();
        }

        private async Task SyncNowAsync()
        {
            // Call backup service to create backup and upload via drive service
            // For now, just update status
            SyncStatus = "Syncing...";
            await Task.Delay(2000); // simulate
            SyncStatus = "Last sync: just now";
        }

        private async void UpdateSyncStatus()
        {
            var config = App.GetService<IConfigurationService>();
            var settings = await config.GetAsync<GoogleDriveSettings>("GoogleDrive");
            if (settings?.LastSync != null)
            {
                var days = (DateTime.Now - settings.LastSync.Value).Days;
                if (days >= 7)
                    SyncStatus = $"⚠️ Sync needed (last: {settings.LastSync.Value:dd/MM}) – click to sync";
                else
                    SyncStatus = $"✓ Last sync: {settings.LastSync.Value:dd/MM}";
            }
            else
            {
                SyncStatus = "⚙️ Configure Google Drive";
            }
        }

        private void OpenBackup()
        {
            var backupService = App.GetService<IBackupService>();
            var loggingService = App.GetService<ILoggingService>();
            var vm = new BackupViewModel(backupService, loggingService);
            var win = new BackupView { DataContext = vm };
            win.Show();
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
                var workOrderRepo = (WorkOrderRepository)_unitOfWork.WorkOrders;
                var orders = await workOrderRepo.GetDailyWorkOrdersAsync(date);
                // Sort by ID descending (latest work orders first)
                TodayWorkOrders = new ObservableCollection<WorkOrderDto>(orders.OrderByDescending(o => o.VehicleChassis));

                var summary = await workOrderRepo.GetDailySummaryAsync(date);
                TotalOrdersToday = summary.TotalWorkOrders;
                TotalRevenueToday = summary.TotalRevenue;

                var allUndone = await _unitOfWork.WorkOrders.FindAsync(w =>
                    w.OrderDate < DateTime.Today && w.Status != WorkStatus.Done);
                UndoneOrders = new ObservableCollection<WorkOrderDto>(
                    allUndone.Select(WorkOrderDto.FromEntity));

                StatusMessage = $"Loaded {TodayWorkOrders.Count} work orders";
                await LoadWeeklySummaryAsync(date);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading work: {ex.Message}";
            }
        }

        partial void OnSelectedDateChanged(DateTime value)
        {
            LoadTodayWorkCommand.Execute(null);
            _ = LoadWeeklySummaryAsync(value);
        }

        private void CreateWorkOrder()
        {
            var logger = App.GetService<ILoggingService>();
            var detailVM = new WorkOrderDetailViewModel(_unitOfWork, logger, 0);
            var window = new WorkOrderDetailWindow { DataContext = detailVM };
            // ✅ Set the owner to the current MainWindow
            window.Owner = Application.Current.MainWindow;
            // ✅ Set startup location (already in XAML, but ensure it's set)
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (window.ShowDialog() == true)
            {
                LoadTodayWorkCommand.Execute(null);
            }
        }

        private async Task LoadWeeklySummaryAsync(DateTime date)
        {
            try
            {
                var workOrderRepo = (WorkOrderRepository)_unitOfWork.WorkOrders;
                WeeklySummary = await workOrderRepo.GetWeeklySummaryAsync(date);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading weekly summary: {ex.Message}";
            }
        }

        private void OpenImport()
        {
            var scopeFactory = App.GetService<IServiceScopeFactory>();
            var loggingService = App.GetService<ILoggingService>();
            var serviceProvider = App.GetService<IServiceProvider>();

            var importVM = new ImportViewModel(
                null,               // IUnitOfWork – not used inside ImportViewModel; safe to pass null
                loggingService,
                serviceProvider,
                scopeFactory
            );

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

        private void ShowAccessoryManagement()
        {
            var logger = App.GetService<ILoggingService>();
            var view = new AccessoryManagementView
            {
                DataContext = new AccessoryManagementViewModel(_unitOfWork, logger)
            };

            var window = new Window
            {
                Title = "Manage Tasks",
                Content = view,
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow
            };
            window.Show();
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

            var logger = App.GetService<ILoggingService>();
            var detailVM = new WorkOrderDetailViewModel(_unitOfWork, logger, SelectedWorkOrder.Id);
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
            var logger = App.GetService<ILoggingService>();
            var vm = new ClientManagementViewModel(_unitOfWork, logger);
            var win = new ClientManagementView { DataContext = vm };
            win.Show();
            LoadTodayWorkCommand.Execute(null);
        }

        private void OpenVehicleManagement()
        {
            var vm = new VehicleManagementViewModel(_unitOfWork);
            var win = new VehicleManagementView { DataContext = vm };
            win.Show();
            // Vehicles might affect work orders? Not directly, but could be needed.
            LoadTodayWorkCommand.Execute(null);
        }

        private async Task EditWorkOrderAsync(int workOrderId)
        {
            var logger = App.GetService<ILoggingService>();
            var detailVM = new WorkOrderDetailViewModel(_unitOfWork, logger, workOrderId);
            var window = new WorkOrderDetailWindow { DataContext = detailVM };
            if (window.ShowDialog() == true)
            {
                await LoadWorkForDateAsync(SelectedDate);
            }
        }


    }
}