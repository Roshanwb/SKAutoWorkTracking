using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.DTOs;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Export.Pdf;
using SKAuto.UI.Localization;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace SKAuto.UI.ViewModels
{
    public class SelectableOption<T>
    {
        public T? Value { get; set; }
        public string Display { get; set; } = "";
    }

    public partial class ClientSelectionItem : ObservableObject
    {
        public int ClientId { get; set; }
        public string Name { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }

    public enum ReportType
    {
        WorkOrders,
        Clients,
        Vehicles,
        Tasks
    }

    public partial class ReportsViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IExportService _excelExport;
        private readonly PdfReportGenerator _pdfExport;
        private readonly ILoggingService _logger;
        private readonly IEmailService _emailService;
        private readonly IConfigurationService _configService;

        [ObservableProperty]
        private ReportType _selectedReportType = ReportType.WorkOrders;

        [ObservableProperty]
        private DateTime _fromDate = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime _toDate = DateTime.Today;

        [ObservableProperty]
        private TaskType? _selectedTaskType;

        [ObservableProperty]
        private WorkStatus? _selectedWorkStatus;

        [ObservableProperty]
        private int? _selectedAccessoryId;

        [ObservableProperty]
        private bool _groupByWeek;

        [ObservableProperty]
        private bool _summaryOnly;

        [ObservableProperty]
        private bool _groupByTaskType;

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private bool _isBusy;

        // NEW: Show/Hide columns
        [ObservableProperty]
        private bool _showPrice = true;

        [ObservableProperty]
        private bool _showTime = true;

        // NEW: Send to Accounting checkbox
        [ObservableProperty]
        private bool _sendToAccounting = false;

        // Options for dropdowns
        public List<SelectableOption<TaskType?>> TaskTypeOptions { get; }
        public List<SelectableOption<WorkStatus?>> WorkStatusOptions { get; }
        public List<SelectableOption<int?>> AccessoryOptions { get; private set; }

        // Client selection
        [ObservableProperty]
        private ObservableCollection<ClientSelectionItem> _availableClients = new();

        // OrderType options
        public List<SelectableOption<OrderType?>> OrderTypeOptions { get; }

        // Selected option objects
        private SelectableOption<TaskType?> _selectedTaskTypeOption;
        public SelectableOption<TaskType?> SelectedTaskTypeOption
        {
            get => _selectedTaskTypeOption;
            set
            {
                if (SetProperty(ref _selectedTaskTypeOption, value))
                {
                    SelectedTaskType = value?.Value;
                }
            }
        }

        private SelectableOption<WorkStatus?> _selectedWorkStatusOption;
        public SelectableOption<WorkStatus?> SelectedWorkStatusOption
        {
            get => _selectedWorkStatusOption;
            set
            {
                if (SetProperty(ref _selectedWorkStatusOption, value))
                {
                    SelectedWorkStatus = value?.Value;
                }
            }
        }

        private SelectableOption<int?> _selectedAccessoryOption;
        public SelectableOption<int?> SelectedAccessoryOption
        {
            get => _selectedAccessoryOption;
            set
            {
                if (SetProperty(ref _selectedAccessoryOption, value))
                {
                    SelectedAccessoryId = value?.Value;
                }
            }
        }

        // Selected OrderType option
        private SelectableOption<OrderType?> _selectedOrderTypeOption;
        public SelectableOption<OrderType?> SelectedOrderTypeOption
        {
            get => _selectedOrderTypeOption;
            set => SetProperty(ref _selectedOrderTypeOption, value);
        }

        // Commands for client selection
        public IRelayCommand SelectAllClientsCommand { get; }
        public IRelayCommand DeselectAllClientsCommand { get; }

        public IAsyncRelayCommand GenerateExcelCommand { get; }
        public IAsyncRelayCommand GeneratePdfCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public ReportsViewModel(IUnitOfWork unitOfWork, IExportService excelExport, PdfReportGenerator pdfExport, ILoggingService logger, IEmailService emailService, IConfigurationService configService)
        {
            _unitOfWork = unitOfWork;
            _excelExport = excelExport;
            _pdfExport = pdfExport;
            _logger = logger;
            _emailService = emailService;
            _configService = configService;

            // Build task type options
            TaskTypeOptions = new List<SelectableOption<TaskType?>>();
            TaskTypeOptions.Add(new SelectableOption<TaskType?> { Value = null, Display = "All" });
            foreach (TaskType value in Enum.GetValues(typeof(TaskType)))
            {
                TaskTypeOptions.Add(new SelectableOption<TaskType?> { Value = value, Display = value.ToString() });
            }

            // Build work status options
            WorkStatusOptions = new List<SelectableOption<WorkStatus?>>();
            WorkStatusOptions.Add(new SelectableOption<WorkStatus?> { Value = null, Display = "All" });
            foreach (WorkStatus value in Enum.GetValues(typeof(WorkStatus)))
            {
                WorkStatusOptions.Add(new SelectableOption<WorkStatus?> { Value = value, Display = value.ToString() });
            }

            // Initialize accessory options with "All"
            AccessoryOptions = new List<SelectableOption<int?>>
            {
                new SelectableOption<int?> { Value = null, Display = "All" }
            };

            // Build OrderType options with friendly names
            OrderTypeOptions = new List<SelectableOption<OrderType?>>();
            OrderTypeOptions.Add(new SelectableOption<OrderType?> { Value = null, Display = "All" });
            foreach (OrderType value in Enum.GetValues(typeof(OrderType)))
            {
                OrderTypeOptions.Add(new SelectableOption<OrderType?> { Value = value, Display = value.GetDisplayName() });
            }

            // Set default selections to "All"
            SelectedTaskTypeOption = TaskTypeOptions.First();
            SelectedWorkStatusOption = WorkStatusOptions.First();
            SelectedAccessoryOption = AccessoryOptions.First();
            SelectedOrderTypeOption = OrderTypeOptions.First();

            // Commands
            SelectAllClientsCommand = new RelayCommand(() => SetAllClientsSelected(true));
            DeselectAllClientsCommand = new RelayCommand(() => SetAllClientsSelected(false));

            GenerateExcelCommand = new AsyncRelayCommand(GenerateExcelAsync);
            GeneratePdfCommand = new AsyncRelayCommand(GeneratePdfAsync);
            CloseCommand = new RelayCommand(CloseWindow);

            // Load initial data
            _ = LoadAccessoriesAsync();
            _ = LoadAvailableClientsAsync();
        }

        partial void OnFromDateChanged(DateTime value) => _ = LoadAvailableClientsAsync();
        partial void OnToDateChanged(DateTime value) => _ = LoadAvailableClientsAsync();

        private async Task LoadAvailableClientsAsync()
        {
            try
            {
                var orders = await _unitOfWork.WorkOrders.FindAsync(w => w.OrderDate >= FromDate && w.OrderDate <= ToDate);
                if (!orders.Any())
                {
                    AvailableClients = new ObservableCollection<ClientSelectionItem>();
                    return;
                }

                var vehicleIds = orders.Select(o => o.VehicleId).Distinct().ToList();
                var vehicles = await _unitOfWork.Vehicles.FindAsync(v => vehicleIds.Contains(v.Id));
                var clientIds = vehicles.Select(v => v.ClientId).Distinct().ToList();
                if (!clientIds.Any())
                {
                    AvailableClients = new ObservableCollection<ClientSelectionItem>();
                    return;
                }

                var allClients = await _unitOfWork.Clients.FindAsync(c => clientIds.Contains(c.Id));
                var clients = allClients
                    .OrderBy(c => c.Name)
                    .Select(c => new ClientSelectionItem
                    {
                        ClientId = c.Id,
                        Name = c.Name,
                        IsSelected = false
                    })
                    .ToList();
                AvailableClients = new ObservableCollection<ClientSelectionItem>(clients);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load clients for report filter", ex);
                StatusMessage = "Error loading clients";
            }
        }

        private void SetAllClientsSelected(bool selected)
        {
            foreach (var item in AvailableClients)
                item.IsSelected = selected;
        }

        private async Task LoadAccessoriesAsync()
        {
            try
            {
                var accessories = await _unitOfWork.Accessories.GetAllAsync();
                var newList = new List<SelectableOption<int?>>
                {
                    new SelectableOption<int?> { Value = null, Display = "All" }
                };
                foreach (var a in accessories.OrderBy(a => a.Name))
                {
                    newList.Add(new SelectableOption<int?> { Value = a.Id, Display = a.Name });
                }
                AccessoryOptions = newList;
                OnPropertyChanged(nameof(AccessoryOptions));

                if (SelectedAccessoryId == null)
                    SelectedAccessoryOption = AccessoryOptions.First();
                else
                {
                    var match = AccessoryOptions.FirstOrDefault(o => o.Value == SelectedAccessoryId);
                    SelectedAccessoryOption = match ?? AccessoryOptions.First();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(LocalizationManager.Instance["FailedToLoadAccessoriesForReportFilter"], ex);
                StatusMessage = LocalizationManager.Instance["ErrorLoadingAccessories"];
            }
        }

        private async Task GenerateExcelAsync()
        {
            if (_isBusy) return;
            _isBusy = true;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            StatusMessage = LocalizationManager.Instance["GeneratingExcelReport"];

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = GetDefaultFileName(".xlsx")
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var filter = BuildFilter();
                    byte[] data = SelectedReportType switch
                    {
                        ReportType.WorkOrders => await _excelExport.GenerateWorkOrdersReportAsync(filter, ShowPrice, ShowTime),
                        ReportType.Tasks => await _excelExport.GenerateTasksReportAsync(new ReportFilter
                        {
                            GroupByTaskType = GroupByTaskType,
                            SummaryOnly = SummaryOnly
                        }),
                        ReportType.Clients => await _excelExport.GenerateClientsReportAsync(),
                        ReportType.Vehicles => await _excelExport.GenerateVehiclesReportAsync(),
                        _ => throw new NotSupportedException()
                    };

                    await System.IO.File.WriteAllBytesAsync(saveDialog.FileName, data);
                    StatusMessage = $"Report saved to {saveDialog.FileName}";
                    _logger.LogInfo($"Excel report generated: {saveDialog.FileName}");

                    // Send email if checkbox is checked
                    if (SendToAccounting)
                    {
                        await SendReportEmailAsync(saveDialog.FileName, "Excel");
                    }
                }
                else
                {
                    StatusMessage = LocalizationManager.Instance["ReportGenerationCancelled"];
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(LocalizationManager.Instance["ExcelReportGenerationFailed"], ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Failed to generate Excel report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isBusy = false;
                Mouse.OverrideCursor = null;
                StatusMessage = "Ready";
            }
        }

        private async Task GeneratePdfAsync()
        {
            if (_isBusy) return;
            _isBusy = true;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            StatusMessage = LocalizationManager.Instance["GeneratingPDFReport"];

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF Files|*.pdf",
                    FileName = GetDefaultFileName(".pdf")
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var filter = BuildFilter();
                    byte[] data = SelectedReportType switch
                    {
                        ReportType.WorkOrders => await _pdfExport.GenerateWorkOrdersReportAsync(filter, ShowPrice, ShowTime),
                        ReportType.Tasks => await _pdfExport.GenerateTasksReportAsync(new ReportFilter
                        {
                            GroupByTaskType = GroupByTaskType,
                            SummaryOnly = SummaryOnly
                        }),
                        ReportType.Clients => await _pdfExport.GenerateClientsReportAsync(),
                        ReportType.Vehicles => await _pdfExport.GenerateVehiclesReportAsync(),
                        _ => throw new NotSupportedException()
                    };

                    await System.IO.File.WriteAllBytesAsync(saveDialog.FileName, data);
                    StatusMessage = $"Report saved to {saveDialog.FileName}";
                    _logger.LogInfo($"PDF report generated: {saveDialog.FileName}");

                    // Send email if checkbox is checked
                    if (SendToAccounting)
                    {
                        await SendReportEmailAsync(saveDialog.FileName, "PDF");
                    }
                }
                else
                {
                    StatusMessage = LocalizationManager.Instance["ReportGenerationCancelled"];
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(LocalizationManager.Instance["PDFReportGenerationFailed"], ex);
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Failed to generate PDF report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isBusy = false;
                Mouse.OverrideCursor = null;
                StatusMessage = "Ready";
            }
        }

        private async Task SendReportEmailAsync(string filePath, string reportType)
        {
            try
            {
                var config = await _configService.GetAsync<AppConfig>("AppConfig") ?? new AppConfig();
                if (string.IsNullOrEmpty(config.AccountingEmail))
                {
                    StatusMessage = "Accounting email not configured. Email not sent.";
                    _logger.LogWarning("Accounting email address is not set in AppConfig.");
                    return;
                }

                var subject = $"SKAuto {reportType} Report - {DateTime.Now:dd/MM/yyyy}";
                var body = $"Please find attached the {reportType} report generated on {DateTime.Now:dd/MM/yyyy HH:mm}.";

                await _emailService.SendEmailAsync(
                    config.AccountingEmail,
                    subject,
                    body,
                    new List<string> { filePath }
                );

                StatusMessage = $"Report saved and sent to accounting ({config.AccountingEmail})";
                _logger.LogInfo($"Report emailed to {config.AccountingEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send report email", ex);
                StatusMessage = $"Email failed: {ex.Message}";
                System.Windows.MessageBox.Show($"Failed to send email: {ex.Message}", "Email Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ReportFilter BuildFilter()
        {
            var selectedClientIds = AvailableClients.Where(c => c.IsSelected).Select(c => c.ClientId).ToList();
            return new ReportFilter
            {
                From = FromDate,
                To = ToDate,
                TaskType = SelectedTaskType,
                WorkStatus = SelectedWorkStatus,
                AccessoryId = SelectedAccessoryId,
                GroupByWeek = GroupByWeek,
                SummaryOnly = SummaryOnly,
                ClientIds = selectedClientIds.Any() ? selectedClientIds : null,
                OrderType = SelectedOrderTypeOption?.Value
            };
        }

        private string GetDefaultFileName(string extension)
        {
            string type = SelectedReportType.ToString().ToLower();
            string date = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return $"{type}_report_{date}{extension}";
        }

        private void CloseWindow()
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
        }
    }
}