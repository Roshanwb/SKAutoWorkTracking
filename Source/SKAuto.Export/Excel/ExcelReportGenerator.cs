using ClosedXML.Excel;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SKAuto.Export.Excel
{
    public class ExcelReportGenerator : IExportService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ExcelReportGenerator(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<byte[]> GenerateDailyReportAsync(DateTime date)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"Work {date:dd-MM-yyyy}");

            // Header
            worksheet.Cell("A1").Value = "SK Auto - Daily Work Report";
            worksheet.Cell("A2").Value = date.ToString("dd/MM/yyyy");
            worksheet.Cell("A3").Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";

            // Style header
            var headerRange = worksheet.Range("A1:E3");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontSize = 12;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Get data – include Vehicle and Client
            var orders = await _unitOfWork.WorkOrders
                .FindAsync(w => w.OrderDate.Date == date.Date);
            var ordersList = orders.ToList();

            if (!ordersList.Any())
            {
                worksheet.Cell("A5").Value = "No work orders for this date";
                worksheet.Columns().AdjustToContents();
                using var stream = new System.IO.MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }

            // Summary section
            worksheet.Cell("A5").Value = "Summary";
            worksheet.Cell("A5").Style.Font.Bold = true;

            worksheet.Cell("A6").Value = "Total Orders:";
            worksheet.Cell("B6").Value = ordersList.Count;

            worksheet.Cell("A7").Value = "Completed:";
            worksheet.Cell("B7").Value = ordersList.Count(o => o.Status == WorkStatus.Done);

            worksheet.Cell("A8").Value = "In Progress:";
            worksheet.Cell("B8").Value = ordersList.Count(o => o.Status == WorkStatus.InProgress);

            worksheet.Cell("A9").Value = "Total Revenue:";
            worksheet.Cell("B9").Value = ordersList.Where(o => o.TotalAmount.HasValue).Sum(o => o.TotalAmount.Value);
            worksheet.Cell("B9").Style.NumberFormat.Format = "€#,##0.00";

            // Orders table
            int row = 12;
            worksheet.Cell($"A{row}").Value = "Work Orders";
            worksheet.Cell($"A{row}").Style.Font.Bold = true;
            row++;

            // Table headers
            var headers = new[] { "ID", "Client", "Vehicle", "Chassis", "Status", "Tasks", "Total", "Notes" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(row, i + 1).Value = headers[i];
                worksheet.Cell(row, i + 1).Style.Font.Bold = true;
                worksheet.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }
            row++;

            // Table data – client accessed via Vehicle.Client
            foreach (var order in ordersList.OrderBy(o => o.Id))
            {
                worksheet.Cell(row, 1).Value = order.Id;
                worksheet.Cell(row, 2).Value = order.Vehicle?.Client?.Name ?? "N/A";
                worksheet.Cell(row, 3).Value = order.Vehicle?.Model ?? "N/A";
                worksheet.Cell(row, 4).Value = order.Vehicle?.ChassisNumber ?? "N/A";
                worksheet.Cell(row, 5).Value = order.Status.ToString();
                worksheet.Cell(row, 6).Value = order.WorkTasks.Count;
                worksheet.Cell(row, 7).Value = order.TotalAmount ?? 0;
                worksheet.Cell(row, 7).Style.NumberFormat.Format = "€#,##0.00";
                worksheet.Cell(row, 8).Value = order.Notes;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            var dataRange = worksheet.Range($"A{row - ordersList.Count - 1}:H{row - 1}");
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            using var memoryStream = new System.IO.MemoryStream();
            workbook.SaveAs(memoryStream);
            return memoryStream.ToArray();
        }

        public async Task<byte[]> GenerateWorkOrderReportAsync(int workOrderId)
        {
            var order = await _unitOfWork.WorkOrders.GetWithDetailsAsync(workOrderId);
            if (order == null)
                throw new ArgumentException($"Work order {workOrderId} not found");

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"WO-{workOrderId}");

            // Header with company info
            worksheet.Cell("A1").Value = "SK Auto";
            worksheet.Cell("A2").Value = "Automotive Services";
            worksheet.Cell("A3").Value = "PSA Certified Partner";
            worksheet.Cell("A4").Value = "TVA: FRXXXXXXXXX";

            // Work Order header
            worksheet.Cell("A6").Value = "WORK ORDER";
            worksheet.Cell("A6").Style.Font.Bold = true;
            worksheet.Cell("A6").Style.Font.FontSize = 14;

            // Order details – client from vehicle
            int row = 8;
            worksheet.Cell(row, 1).Value = "Order #:";
            worksheet.Cell(row, 2).Value = workOrderId;
            row++;

            worksheet.Cell(row, 1).Value = "Date:";
            worksheet.Cell(row, 2).Value = order.OrderDate.ToString("dd/MM/yyyy");
            row++;

            worksheet.Cell(row, 1).Value = "Client:";
            worksheet.Cell(row, 2).Value = order.Vehicle?.Client?.Name ?? "N/A";
            row++;

            worksheet.Cell(row, 1).Value = "Vehicle:";
            worksheet.Cell(row, 2).Value = $"{order.Vehicle?.Model} ({order.Vehicle?.ChassisNumber})";
            row++;

            worksheet.Cell(row, 1).Value = "Status:";
            worksheet.Cell(row, 2).Value = order.Status.ToString();
            row += 2;

            // Tasks table
            worksheet.Cell(row, 1).Value = "Tasks";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            row++;

            if (order.WorkTasks.Any())
            {
                // Headers: Accessory, Type, Qty, Unit Price, Fitting, Subtotal
                var taskHeaders = new[] { "Accessory", "Type", "Qty", "Unit Price", "Fitting", "Subtotal" };
                for (int i = 0; i < taskHeaders.Length; i++)
                {
                    worksheet.Cell(row, i + 1).Value = taskHeaders[i];
                    worksheet.Cell(row, i + 1).Style.Font.Bold = true;
                }
                row++;

                foreach (var task in order.WorkTasks)
                {
                    worksheet.Cell(row, 1).Value = task.Accessory?.Name;
                    worksheet.Cell(row, 2).Value = task.TaskType.ToString();
                    worksheet.Cell(row, 3).Value = task.Quantity;

                    // Based on TaskType, show price in appropriate column
                    if (task.TaskType == TaskType.Sell)
                    {
                        worksheet.Cell(row, 4).Value = task.Price ?? 0;    // Unit Price
                        worksheet.Cell(row, 4).Style.NumberFormat.Format = "€#,##0.00";
                        worksheet.Cell(row, 5).Value = 0;                  // Fitting empty
                    }
                    else if (task.TaskType == TaskType.Fit || task.TaskType == TaskType.Preparation || task.TaskType == TaskType.Travel)
                    {
                        worksheet.Cell(row, 4).Value = 0;                  // Unit Price empty
                        worksheet.Cell(row, 5).Value = task.Price ?? 0;    // Fitting
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "€#,##0.00";
                    }
                    else // Remove, Other
                    {
                        worksheet.Cell(row, 4).Value = 0;
                        worksheet.Cell(row, 5).Value = 0;
                    }

                    worksheet.Cell(row, 6).Value = task.CalculateTotal();
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "€#,##0.00";
                    row++;
                }
            }

            // Travel if any
            if (order.Travels.Any())
            {
                row++;
                worksheet.Cell(row, 1).Value = "Travel";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                row++;

                foreach (var travel in order.Travels)
                {
                    worksheet.Cell(row, 1).Value = $"To: {travel.Destination}";
                    worksheet.Cell(row, 2).Value = travel.TravelDate.ToString("dd/MM/yyyy");
                    worksheet.Cell(row, 3).Value = travel.TravelCost ?? 0;
                    worksheet.Cell(row, 3).Style.NumberFormat.Format = "€#,##0.00";
                    row++;
                }
            }

            // Totals
            row++;
            worksheet.Cell(row, 5).Value = "Total:";
            worksheet.Cell(row, 5).Style.Font.Bold = true;
            worksheet.Cell(row, 6).Value = order.TotalAmount ?? 0;
            worksheet.Cell(row, 6).Style.Font.Bold = true;
            worksheet.Cell(row, 6).Style.NumberFormat.Format = "€#,##0.00";

            // Notes
            if (!string.IsNullOrEmpty(order.Notes))
            {
                row += 2;
                worksheet.Cell(row, 1).Value = "Notes:";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                row++;
                worksheet.Cell(row, 1).Value = order.Notes;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // Other methods (GenerateMonthlySummaryAsync, GeneratePSAPerformanceReportAsync, ExportToCsvAsync) would need similar adjustments.
        // For brevity, they are omitted but follow the same pattern: replace order.Client with order.Vehicle.Client,
        // and use task.Price with conditional logic as above.
        public async Task<byte[]> GenerateMonthlySummaryAsync(int month, int year)
        {
            var fromDate = new DateTime(year, month, 1);
            var toDate = fromDate.AddMonths(1).AddDays(-1);

            var orders = await _unitOfWork.WorkOrders.FindAsync(w =>
                w.OrderDate >= fromDate && w.OrderDate <= toDate);
            var ordersList = orders.ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"{month:00}-{year}");

            // Header
            worksheet.Cell("A1").Value = $"SK Auto - Monthly Summary {month:00}/{year}";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 14;

            // Summary by client type – now group by Vehicle.Client.Type
            worksheet.Cell("A3").Value = "Summary by Client Type";
            worksheet.Cell("A3").Style.Font.Bold = true;

            var psaOrders = ordersList.Where(o => o.Vehicle?.Client?.Type == ClientType.PSA).ToList();
            var directOrders = ordersList.Where(o => o.Vehicle?.Client?.Type == ClientType.Direct).ToList();

            worksheet.Cell("A4").Value = "PSA Orders:";
            worksheet.Cell("B4").Value = psaOrders.Count;
            worksheet.Cell("C4").Value = psaOrders.Where(o => o.TotalAmount.HasValue).Sum(o => o.TotalAmount.Value);
            worksheet.Cell("C4").Style.NumberFormat.Format = "€#,##0.00";

            worksheet.Cell("A5").Value = "Direct Orders:";
            worksheet.Cell("B5").Value = directOrders.Count;
            worksheet.Cell("C5").Value = directOrders.Where(o => o.TotalAmount.HasValue).Sum(o => o.TotalAmount.Value);
            worksheet.Cell("C5").Style.NumberFormat.Format = "€#,##0.00";

            worksheet.Cell("A6").Value = "Total:";
            worksheet.Cell("A6").Style.Font.Bold = true;
            worksheet.Cell("B6").Value = ordersList.Count;
            worksheet.Cell("C6").Value = ordersList.Where(o => o.TotalAmount.HasValue).Sum(o => o.TotalAmount.Value);
            worksheet.Cell("C6").Style.Font.Bold = true;
            worksheet.Cell("C6").Style.NumberFormat.Format = "€#,##0.00";

            // Daily breakdown
            int row = 9;
            worksheet.Cell(row, 1).Value = "Daily Breakdown";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            row++;

            var dailyHeaders = new[] { "Date", "Orders", "Completed", "Revenue" };
            for (int i = 0; i < dailyHeaders.Length; i++)
            {
                worksheet.Cell(row, i + 1).Value = dailyHeaders[i];
                worksheet.Cell(row, i + 1).Style.Font.Bold = true;
            }
            row++;

            for (var date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                var dayOrders = ordersList.Where(o => o.OrderDate.Date == date.Date).ToList();
                if (dayOrders.Any())
                {
                    worksheet.Cell(row, 1).Value = date.ToString("dd/MM");
                    worksheet.Cell(row, 2).Value = dayOrders.Count;
                    worksheet.Cell(row, 3).Value = dayOrders.Count(o => o.Status == WorkStatus.Done);
                    worksheet.Cell(row, 4).Value = dayOrders.Where(o => o.TotalAmount.HasValue).Sum(o => o.TotalAmount.Value);
                    worksheet.Cell(row, 4).Style.NumberFormat.Format = "€#,##0.00";
                    row++;
                }
            }

            worksheet.Columns().AdjustToContents();
            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> GeneratePSAPerformanceReportAsync(DateTime fromDate, DateTime toDate)
        {
            var orders = await _unitOfWork.WorkOrders.FindAsync(w =>
                w.OrderDate >= fromDate && w.OrderDate <= toDate &&
                w.Vehicle != null && w.Vehicle.Client != null && w.Vehicle.Client.Type == ClientType.PSA);
            var ordersList = orders.ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("PSA Performance");

            // Header
            worksheet.Cell("A1").Value = $"PSA Performance Report {fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 14;

            // Summary
            worksheet.Cell("A3").Value = "Total PSA Orders:";
            worksheet.Cell("B3").Value = ordersList.Count;

            var totalHours = ordersList
                .SelectMany(o => o.WorkTasks)
                .Where(t => t.ActualMinutes.HasValue)
                .Sum(t => t.ActualMinutes.Value) / 60.0m;

            worksheet.Cell("A4").Value = "Total Hours Worked:";
            worksheet.Cell("B4").Value = totalHours;
            worksheet.Cell("B4").Style.NumberFormat.Format = "0.00";

            // Efficiency analysis
            int row = 7;
            worksheet.Cell(row, 1).Value = "Efficiency Analysis";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            row++;

            var efficiencyHeaders = new[] { "Accessory", "Avg Est Time", "Avg Act Time", "Efficiency %", "Count" };
            for (int i = 0; i < efficiencyHeaders.Length; i++)
            {
                worksheet.Cell(row, i + 1).Value = efficiencyHeaders[i];
                worksheet.Cell(row, i + 1).Style.Font.Bold = true;
            }
            row++;

            var accessoryTasks = ordersList
                .SelectMany(o => o.WorkTasks)
                .Where(t => t.Accessory != null && t.EstimatedMinutes.HasValue && t.ActualMinutes.HasValue)
                .GroupBy(t => t.Accessory!.Name);

            foreach (var group in accessoryTasks)
            {
                var avgEst = group.Average(t => t.EstimatedMinutes!.Value);
                var avgAct = group.Average(t => t.ActualMinutes!.Value);
                var efficiency = avgEst > 0 ? (avgEst / avgAct) * 100 : 100;

                worksheet.Cell(row, 1).Value = group.Key;
                worksheet.Cell(row, 2).Value = avgEst;
                worksheet.Cell(row, 2).Style.NumberFormat.Format = "0";
                worksheet.Cell(row, 3).Value = avgAct;
                worksheet.Cell(row, 3).Style.NumberFormat.Format = "0";
                worksheet.Cell(row, 4).Value = efficiency;
                worksheet.Cell(row, 4).Style.NumberFormat.Format = "0.00%";
                worksheet.Cell(row, 5).Value = group.Count();
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<string> ExportToCsvAsync<T>(IEnumerable<T> data)
        {
            var type = typeof(T);
            var properties = type.GetProperties();

            var csv = new System.Text.StringBuilder();

            // Header
            csv.AppendLine(string.Join(",", properties.Select(p => p.Name)));

            // Data
            foreach (var item in data)
            {
                var values = properties.Select(p =>
                {
                    var value = p.GetValue(item);
                    return value == null ? "" :
                        value is DateTime date ? date.ToString("dd/MM/yyyy") :
                        value.ToString()!.Replace(",", ";");
                });
                csv.AppendLine(string.Join(",", values));
            }

            return csv.ToString();
        }
    }
}