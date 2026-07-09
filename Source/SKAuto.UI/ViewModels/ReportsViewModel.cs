using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Export.Pdf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;   using SKAuto.UI.Localization;
// For Cursors

namespace SKAuto.UI.ViewModels
{
    public class SelectableOption<T>
    {
        public T? Value { get; set; }
        public string Display { get; set; } = "";
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
        private bool _isBusy;   // To disable buttons during generation

        // Options for dropdowns
        public List<SelectableOption<TaskType?>> TaskTypeOptions { get; }
        public List<SelectableOption<WorkStatus?>> WorkStatusOptions { get; }
        public List<SelectableOption<int?>> AccessoryOptions { get; private set; }

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

        public IAsyncRelayCommand GenerateExcelCommand { get; }
        public IAsyncRelayCommand GeneratePdfCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public ReportsViewModel(IUnitOfWork unitOfWork, IExportService excelExport, PdfReportGenerator pdfExport, ILoggingService logger)
        {
            _unitOfWork = unitOfWork;
            _excelExport = excelExport;
            _pdfExport = pdfExport;
            _logger = logger;

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
            // Set default selections to "All"
            SelectedTaskTypeOption = TaskTypeOptions.First();
            SelectedWorkStatusOption = WorkStatusOptions.First();
            SelectedAccessoryOption = AccessoryOptions.First();

            _ = LoadAccessoriesAsync();

            GenerateExcelCommand = new AsyncRelayCommand(GenerateExcelAsync);
            GeneratePdfCommand = new AsyncRelayCommand(GeneratePdfAsync);
            CloseCommand = new RelayCommand(CloseWindow);
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

                // Keep the "All" selection if it was previously selected
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
                    byte[] data = SelectedReportType switch
                    {
                        ReportType.WorkOrders => await _excelExport.GenerateWorkOrdersReportAsync(new ReportFilter
                        {
                            From = FromDate,
                            To = ToDate,
                            TaskType = SelectedTaskType,
                            WorkStatus = SelectedWorkStatus,
                            AccessoryId = SelectedAccessoryId,
                            GroupByWeek = GroupByWeek,
                            SummaryOnly = SummaryOnly
                        }),
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
                    byte[] data = SelectedReportType switch
                    {
                        ReportType.WorkOrders => await _pdfExport.GenerateWorkOrdersReportAsync(new ReportFilter
                        {
                            From = FromDate,
                            To = ToDate,
                            TaskType = SelectedTaskType,
                            WorkStatus = SelectedWorkStatus,
                            AccessoryId = SelectedAccessoryId,
                            GroupByWeek = GroupByWeek,
                            SummaryOnly = SummaryOnly
                        }),
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
            }
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