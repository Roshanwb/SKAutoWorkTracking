using SKAuto.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.Interfaces
{
    public interface IExportService
    {
        Task<byte[]> GenerateDailyReportAsync(DateTime date);
        Task<byte[]> GenerateWorkOrderReportAsync(int workOrderId);
        Task<byte[]> GenerateMonthlySummaryAsync(int month, int year);
        Task<byte[]> GeneratePSAPerformanceReportAsync(DateTime fromDate, DateTime toDate);
        Task<string> ExportToCsvAsync<T>(IEnumerable<T> data);
        //Task<byte[]> GenerateWorkOrdersReportAsync(DateTime from, DateTime to);
        Task<byte[]> GenerateClientsReportAsync();
        Task<byte[]> GenerateVehiclesReportAsync();
        Task<byte[]> GenerateWorkOrdersReportAsync(ReportFilter filter);
    }
}
