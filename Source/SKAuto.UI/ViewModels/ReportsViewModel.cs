using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SKAuto.Core.DTOs;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Export.Pdf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

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
        Vehicles
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
        private bool _groupByWeek;

        [ObservableProperty]
        private bool _summaryOnly;

        [ObservableProperty]
        private string _statusMessage;

        public List<SelectableOption<TaskType?>> TaskTypeOptions { get; }
        public List<SelectableOption<WorkStatus?>> WorkStatusOptions { get; }

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

            GenerateExcelCommand = new AsyncRelayCommand(GenerateExcelAsync);
            GeneratePdfCommand = new AsyncRelayCommand(GeneratePdfAsync);
            CloseCommand = new RelayCommand(CloseWindow);
        }

        private async Task GenerateExcelAsync()
        {
            try
            {
                StatusMessage = "Generating Excel report...";
                var saveDialog = new SaveFileDialog
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
                            GroupByWeek = GroupByWeek,
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
            }
            catch (Exception ex)
            {
                _logger.LogError("Excel report generation failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to generate Excel report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task GeneratePdfAsync()
        {
            try
            {
                StatusMessage = "Generating PDF report...";
                var saveDialog = new SaveFileDialog
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
                            GroupByWeek = GroupByWeek,
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
            }
            catch (Exception ex)
            {
                _logger.LogError("PDF report generation failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to generate PDF report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            foreach (Window window in Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
        }
    }
}