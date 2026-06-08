using SKAuto.Core.DTOs;
using System.Threading.Tasks;

namespace SKAuto.Core.Interfaces
{
    public interface IExportService
    {
        Task<byte[]> GenerateDailyReportAsync(DateTime date);
        Task<byte[]> GenerateWorkOrderReportAsync(int workOrderId);
        Task<byte[]> GenerateWorkOrdersReportAsync(ReportFilter filter);
        Task<byte[]> GenerateTasksReportAsync(ReportFilter filter);   // NEW
        Task<byte[]> GenerateClientsReportAsync();
        Task<byte[]> GenerateVehiclesReportAsync();
        Task<byte[]> GenerateMonthlySummaryAsync(int month, int year);
        Task<byte[]> GeneratePSAPerformanceReportAsync(DateTime fromDate, DateTime toDate);
        Task<string> ExportToCsvAsync<T>(IEnumerable<T> data);
    }
}