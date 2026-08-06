using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Repository;
using SKAuto.Export.Pdf;
using SKAuto.UI.Localization;
using SKAuto.UI.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace SKAuto.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfigurationService _configService;
        private readonly ILoggingService _logger;
        private readonly IGoogleDriveService _driveService;

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
        public IRelayCommand GoToTodayCommand { get; }

        // NEW: Billing status command
        public IAsyncRelayCommand<string> UpdateBillingStatusCommand { get; }

        // NEW: Attachments command
        public IAsyncRelayCommand<WorkOrderDto> OpenAttachmentsCommand { get; }

        public MainViewModel(IUnitOfWork unitOfWork, IConfigurationService configService, ILoggingService logger, IGoogleDriveService driveService)
        {
            _unitOfWork = unitOfWork;
            _configService = configService;
            _logger = logger;
            _driveService = driveService; // <-- This was missing – now injected!

            LoadTodayWorkCommand = new AsyncRelayCommand(LoadTodayWorkAsync);
            CreateWorkOrderCommand = new RelayCommand(CreateWorkOrder);
            ImportDataCommand = new RelayCommand(OpenImport);
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
            GoToTodayCommand = new RelayCommand(() => SelectedDate = DateTime.Today);

            // NEW: Billing status command with CanExecute
            UpdateBillingStatusCommand = new AsyncRelayCommand<string>(UpdateBillingStatusAsync, s => SelectedWorkOrder != null);

            // NEW: Attachments command
            OpenAttachmentsCommand = new AsyncRelayCommand<WorkOrderDto>(OpenAttachmentsAsync);

            LoadTodayWorkCommand.Execute(null);
        }

        // ===== BILLING STATUS =====
        private async Task UpdateBillingStatusAsync(string? statusString)
        {
            if (SelectedWorkOrder == null || string.IsNullOrEmpty(statusString)) return;

            if (Enum.TryParse<BillingStatus>(statusString, out var billingStatus))
            {
                try
                {
                    var workOrder = await _unitOfWork.WorkOrders.GetByIdAsync(SelectedWorkOrder.Id);
                    if (workOrder != null)
                    {
                        workOrder.BillingStatus = billingStatus;
                        await _unitOfWork.WorkOrders.UpdateAsync(workOrder);
                        await _unitOfWork.CompleteAsync();
                        StatusMessage = $"Updated billing status to {billingStatus} for order {SelectedWorkOrder.Id}";
                        await LoadWorkForDateAsync(SelectedDate);
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error updating billing status: {ex.Message}";
                }
            }
        }

        // ===== OPEN ATTACHMENTS =====
        private async Task OpenAttachmentsAsync(WorkOrderDto? workOrder)
        {
            if (workOrder == null) return;

            var vm = new AttachmentManagementViewModel(_unitOfWork, _driveService, _logger, workOrder.Id);
            var view = new AttachmentManagementView(vm);
            view.Owner = System.Windows.Application.Current.MainWindow;
            view.ShowDialog();
            await LoadWorkForDateAsync(SelectedDate);
        }

        // ===== EXISTING METHODS (unchanged) =====
        private void ShowUserManagement()
        {
            var logger = App.GetService<ILoggingService>();
            var vm = new UserManagementViewModel(_unitOfWork, logger, CurrentUser);
            var window = new UserManagementView { DataContext = vm };
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private void ChangePassword()
        {
            var logger = App.GetService<ILoggingService>();
            var vm = new UserEditViewModel(_unitOfWork, logger, CurrentUser, null, UserEditMode.ChangePassword);
            var window = new UserEditView(vm);
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        [RelayCommand]
        private void OpenSettings()
        {
            var settingsWindow = new SettingsView();
            settingsWindow.DataContext = new SettingsViewModel(_configService);
            settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
             settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
            settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
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
            var window = new ReportsView { DataContext = vm };
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Show();
        }

        private void OpenHelp()
        {
            var window = new HelpView();
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private void OpenAbout()
        {
            System.Windows.MessageBox.Show("SKAuto Work Tracking System\nVersion 1.0.0\nDeveloped by SK Auto\n2026 - Insights®",
                            LocalizationManager.Instance["MainWindow_About"], MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void OpenPriceUpdate()
        {
            var logger = App.GetService<ILoggingService>();
            var vm = new PriceUpdateViewModel(_unitOfWork, logger);
            var window = new PriceUpdateView { DataContext = vm, Owner = System.Windows.Application.Current.MainWindow };
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (window.ShowDialog() == true)
            {
                await LoadTodayWorkAsync();
                StatusMessage = LocalizationManager.Instance["PricesUpdatedAndGridRefreshed"];
            }
        }

        private void OpenDriveSettings()
        {
            var driveService = App.GetService<IGoogleDriveService>();
            var config = App.GetService<IConfigurationService>();
            var backupService = App.GetService<IBackupService>();
            var logger = App.GetService<ILoggingService>();

            var vm = new GoogleDriveSettingsViewModel(driveService, config, backupService, logger);
            var window = new GoogleDriveSettingsView { DataContext = vm };
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Show();
            UpdateSyncStatus();     
        }

        private async Task SyncNowAsync()
        {
            SyncStatus = "Syncing...";
            await Task.Delay(2000);
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
                    SyncStatus = $"✅ Last sync: {settings.LastSync.Value:dd/MM}";
            }
            else
            {
                SyncStatus = "❌ Configure Google Drive";
            }
        }

        private void OpenBackup()
        {
            var backupService = App.GetService<IBackupService>();
            var loggingService = App.GetService<ILoggingService>();
            var vm = new BackupViewModel(backupService, loggingService);
            var window =   new BackupView { DataContext = vm };
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Show();
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
            var detailVM = new WorkOrderDetailViewModel(_unitOfWork, logger, _configService, _driveService, 0);
            var window = new WorkOrderDetailWindow { DataContext = detailVM };
            window.Owner = System.Windows.Application.Current.MainWindow;
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
                null,
                loggingService,
                serviceProvider,
                scopeFactory
            );

            var window = new ImportView { DataContext = importVM };
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
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
                DataContext = new AccessoryManagementViewModel(_unitOfWork, logger, _configService)
            };

            var window = new System.Windows.Window
            {
                Title = "Manage Tasks",
                Content = view,
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow
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
                    var window = new ClientEditWindow { DataContext = vm };
                    window.Owner = System.Windows.Application.Current.MainWindow;
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    if (window.ShowDialog() == true)
                    {
                        await LoadWorkForDateAsync(SelectedDate);
                        StatusMessage = LocalizationManager.Instance["ClientUpdatedSuccessfully"];
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
            var detailVM = new WorkOrderDetailViewModel(_unitOfWork, logger, _configService, _driveService, SelectedWorkOrder.Id);
            var window = new WorkOrderDetailWindow { DataContext = detailVM };
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
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

            var result = System.Windows.MessageBox.Show($"Delete work order #{SelectedWorkOrder.Id}? This action cannot be undone.",
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
            UpdateBillingStatusCommand?.NotifyCanExecuteChanged();
        }

        private void OpenClientManagement()
        {
            var logger = App.GetService<ILoggingService>();
            var vm = new ClientManagementViewModel(_unitOfWork, logger);
            var window = new ClientManagementView { DataContext = vm };
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Show();
                LoadTodayWorkCommand.Execute(null);
        }

        private void OpenVehicleManagement()
        {
            var vm = new VehicleManagementViewModel(_unitOfWork);
            var window = new VehicleManagementView { DataContext = vm };
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Show();
            LoadTodayWorkCommand.Execute(null);
        }

        private async Task EditWorkOrderAsync(int workOrderId)
        {
            var logger = App.GetService<ILoggingService>();
            var detailVM = new WorkOrderDetailViewModel(_unitOfWork, logger, _configService, _driveService, workOrderId);
            var window = new WorkOrderDetailWindow { DataContext = detailVM };
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (window.ShowDialog() == true)
            {
                await LoadWorkForDateAsync(SelectedDate);
            }
        }
    }
}