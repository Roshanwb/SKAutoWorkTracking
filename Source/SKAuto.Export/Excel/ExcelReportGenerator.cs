using ClosedXML.Excel;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using System;
using System.IO;
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
            // (unchanged – keep your existing implementation)
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"Work {date:dd-MM-yyyy}");

            worksheet.Cell("A1").Value = "SK Auto - Daily Work Report";
            worksheet.Cell("A2").Value = date.ToString("dd/MM/yyyy");
            worksheet.Cell("A3").Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";

            var headerRange = worksheet.Range("A1:E3");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontSize = 12;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            var orders = await _unitOfWork.WorkOrders
                .FindAsync(w => w.OrderDate.Date == date.Date);
            var ordersList = orders.ToList();

            if (!ordersList.Any())
            {
                worksheet.Cell("A5").Value = "No work orders for this date";
                worksheet.Columns().AdjustToContents();
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }

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

            int row = 12;
            worksheet.Cell($"A{row}").Value = "Work Orders";
            worksheet.Cell($"A{row}").Style.Font.Bold = true;
            row++;

            var headers = new[] { "ID", "Client", "Vehicle", "Chassis", "Status", "Tasks", "Total", "Notes" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(row, i + 1).Value = headers[i];
                worksheet.Cell(row, i + 1).Style.Font.Bold = true;
                worksheet.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }
            row++;

            foreach (var order in ordersList.OrderBy(o => o.Id))
            {
                var tasks = order.WorkTasks?.Select(t => t.Accessory?.Name).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new();
                string taskNames = string.Join(", ", tasks);

                worksheet.Cell(row, 1).Value = order.Id;
                worksheet.Cell(row, 2).Value = order.Vehicle?.Client?.Name ?? "N/A";
                worksheet.Cell(row, 3).Value = order.Vehicle?.Model ?? "N/A";
                worksheet.Cell(row, 4).Value = order.Vehicle?.ChassisNumber ?? "N/A";
                worksheet.Cell(row, 5).Value = order.Status.ToString();
                worksheet.Cell(row, 6).Value = taskNames;
                worksheet.Cell(row, 7).Value = order.TotalAmount ?? 0;
                worksheet.Cell(row, 7).Style.NumberFormat.Format = "€#,##0.00";
                worksheet.Cell(row, 8).Value = order.Notes;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            var dataRange = worksheet.Range($"A{row - ordersList.Count - 1}:H{row - 1}");
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            return memoryStream.ToArray();
        }

        public async Task<byte[]> GenerateWorkOrderReportAsync(int workOrderId)
        {
            // (unchanged – keep your existing implementation)
            var order = await _unitOfWork.WorkOrders.GetWithDetailsAsync(workOrderId);
            if (order == null)
                throw new ArgumentException($"Work order {workOrderId} not found");

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"WO-{workOrderId}");

            worksheet.Cell("A1").Value = "SK Auto";
            worksheet.Cell("A2").Value = "Automotive Services";
            worksheet.Cell("A3").Value = "PSA Certified Partner";
            worksheet.Cell("A4").Value = "TVA: FRXXXXXXXXX";

            worksheet.Cell("A6").Value = "WORK ORDER";
            worksheet.Cell("A6").Style.Font.Bold = true;
            worksheet.Cell("A6").Style.Font.FontSize = 14;

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

            worksheet.Cell(row, 1).Value = "Tasks";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            row++;

            if (order.WorkTasks.Any())
            {
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

                    if (task.TaskType == TaskType.Sell)
                    {
                        worksheet.Cell(row, 4).Value = task.Price ?? 0;
                        worksheet.Cell(row, 4).Style.NumberFormat.Format = "€#,##0.00";
                        worksheet.Cell(row, 5).Value = 0;
                    }
                    else if (task.TaskType == TaskType.Fit || task.TaskType == TaskType.Preparation || task.TaskType == TaskType.Travel)
                    {
                        worksheet.Cell(row, 4).Value = 0;
                        worksheet.Cell(row, 5).Value = task.Price ?? 0;
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "€#,##0.00";
                    }
                    else
                    {
                        worksheet.Cell(row, 4).Value = 0;
                        worksheet.Cell(row, 5).Value = 0;
                    }

                    worksheet.Cell(row, 6).Value = task.CalculateTotal();
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "€#,##0.00";
                    row++;
                }
            }

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

            row++;
            worksheet.Cell(row, 5).Value = "Total:";
            worksheet.Cell(row, 5).Style.Font.Bold = true;
            worksheet.Cell(row, 6).Value = order.TotalAmount ?? 0;
            worksheet.Cell(row, 6).Style.Font.Bold = true;
            worksheet.Cell(row, 6).Style.NumberFormat.Format = "€#,##0.00";

            if (!string.IsNullOrEmpty(order.Notes))
            {
                row += 2;
                worksheet.Cell(row, 1).Value = "Notes:";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                row++;
                worksheet.Cell(row, 1).Value = order.Notes;
            }

            worksheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> GenerateWorkOrdersReportAsync(ReportFilter filter)
        {
            // Load orders with includes
            var orders = (await _unitOfWork.WorkOrders
                .FindAsync(w => w.OrderDate >= filter.From && w.OrderDate <= filter.To))
                .ToList();

            // Load related data
            var vehicleIds = orders.Select(o => o.VehicleId).Distinct().ToList();
            var vehicles = (await _unitOfWork.Vehicles.FindAsync(v => vehicleIds.Contains(v.Id)))
                .ToDictionary(v => v.Id);
            var clientIds = vehicles.Values.Select(v => v.ClientId).Distinct().ToList();
            var clients = (await _unitOfWork.Clients.FindAsync(c => clientIds.Contains(c.Id)))
                .ToDictionary(c => c.Id);
            var orderIds = orders.Select(o => o.Id).ToList();
            var tasks = (await _unitOfWork.WorkTasks.FindAsync(t => orderIds.Contains(t.WorkOrderId)))
                .GroupBy(t => t.WorkOrderId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Apply task type filter
            if (filter.TaskType.HasValue)
            {
                var orderIdsWithTask = tasks
                    .Where(kvp => kvp.Value.Any(t => t.TaskType == filter.TaskType.Value))
                    .Select(kvp => kvp.Key)
                    .ToHashSet();
                orders = orders.Where(o => orderIdsWithTask.Contains(o.Id)).ToList();
            }

            // Apply work status filter
            if (filter.WorkStatus.HasValue)
            {
                orders = orders.Where(o => o.Status == filter.WorkStatus.Value).ToList();
            }

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Work Orders");

            // Title
            ws.Cell(1, 1).Value = "Work Orders Report";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(2, 1).Value = $"Period: {filter.From:dd/MM/yyyy} – {filter.To:dd/MM/yyyy}";
            ws.Cell(3, 1).Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";

            // Filters summary
            int filterRow = 5;
            ws.Cell(filterRow, 1).Value = "Applied Filters:";
            ws.Cell(filterRow, 1).Style.Font.Bold = true;
            filterRow++;
            if (filter.TaskType.HasValue)
                ws.Cell(filterRow++, 1).Value = $"Task Type: {filter.TaskType.Value}";
            else
                ws.Cell(filterRow++, 1).Value = "Task Type: All";
            if (filter.WorkStatus.HasValue)
                ws.Cell(filterRow++, 1).Value = $"Status: {filter.WorkStatus.Value}";
            else
                ws.Cell(filterRow++, 1).Value = "Status: All";
            ws.Cell(filterRow++, 1).Value = $"Group by Week: {(filter.GroupByWeek ? "Yes" : "No")}";
            ws.Cell(filterRow++, 1).Value = $"Summary Only: {(filter.SummaryOnly ? "Yes" : "No")}";

            int dataStartRow = filterRow + 2;

            if (filter.SummaryOnly)
            {
                // Summary table
                ws.Cell(dataStartRow, 1).Value = "Summary Statistics";
                ws.Cell(dataStartRow, 1).Style.Font.Bold = true;
                dataStartRow++;

                ws.Cell(dataStartRow, 1).Value = "Total Orders:";
                ws.Cell(dataStartRow, 2).Value = orders.Count;
                dataStartRow++;

                ws.Cell(dataStartRow, 1).Value = "Total Tasks:";
                ws.Cell(dataStartRow, 2).Value = tasks.Sum(kvp => kvp.Value.Count);
                dataStartRow++;

                ws.Cell(dataStartRow, 1).Value = "Total Revenue:";
                ws.Cell(dataStartRow, 2).Value = orders.Sum(o => o.TotalAmount ?? 0);
                ws.Cell(dataStartRow, 2).Style.NumberFormat.Format = "€#,##0.00";
                dataStartRow++;
            }
            else
            {
                if (filter.GroupByWeek)
                {
                    var weekGroups = orders
                        .GroupBy(o => GetIsoWeek(o.OrderDate))
                        .OrderBy(g => g.Key);

                    foreach (var group in weekGroups)
                    {
                        int week = group.Key;
                        DateTime weekStart = GetStartOfWeek(group.First().OrderDate);
                        DateTime weekEnd = weekStart.AddDays(6);

                        ws.Cell(dataStartRow, 1).Value = $"Week {week} ({weekStart:dd/MM} – {weekEnd:dd/MM})";
                        ws.Cell(dataStartRow, 1).Style.Font.Bold = true;
                        ws.Cell(dataStartRow, 1).Style.Font.FontSize = 12;
                        dataStartRow++;

                        WriteWorkOrderTable(ws, ref dataStartRow, group.ToList(), vehicles, clients, tasks);

                        decimal weekTotal = group.Sum(o => o.TotalAmount ?? 0);
                        ws.Cell(dataStartRow, 7).Value = $"Week Total: €{weekTotal:0.00}";
                        ws.Cell(dataStartRow, 7).Style.Font.Bold = true;
                        ws.Cell(dataStartRow, 7).Style.NumberFormat.Format = "€#,##0.00";
                        dataStartRow += 2;
                    }
                }
                else
                {
                    WriteWorkOrderTable(ws, ref dataStartRow, orders, vehicles, clients, tasks);
                }
            }

            // Grand total
            decimal grandTotal = orders.Sum(o => o.TotalAmount ?? 0);
            ws.Cell(dataStartRow, 7).Value = $"GRAND TOTAL: €{grandTotal:0.00}";
            ws.Cell(dataStartRow, 7).Style.Font.Bold = true;
            ws.Cell(dataStartRow, 7).Style.NumberFormat.Format = "€#,##0.00";

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        private void WriteWorkOrderTable(IXLWorksheet ws, ref int row, List<WorkOrder> orders,
            Dictionary<int, Vehicle> vehicles, Dictionary<int, Client> clients,
            Dictionary<int, List<WorkTask>> tasks)
        {
            string[] headers = { "ID", "Date", "Client", "Vehicle", "Status", "Tasks", "Total" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
                ws.Cell(row, i + 1).Style.Font.Bold = true;
                ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }
            row++;

            bool alternate = false;
            foreach (var o in orders)
            {
                vehicles.TryGetValue(o.VehicleId, out var vehicle);
                string clientName = vehicle != null && clients.TryGetValue(vehicle.ClientId, out var client) ? client.Name : "";
                string taskNames = tasks.ContainsKey(o.Id)
                    ? string.Join(", ", tasks[o.Id].Select(t => t.Accessory?.Name ?? "?"))
                    : "";

                ws.Cell(row, 1).Value = o.Id;
                ws.Cell(row, 2).Value = o.OrderDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 3).Value = clientName;
                ws.Cell(row, 4).Value = vehicle?.Model ?? "";
                ws.Cell(row, 5).Value = o.Status.ToString();
                ws.Cell(row, 6).Value = taskNames;
                ws.Cell(row, 7).Value = o.TotalAmount ?? 0;
                ws.Cell(row, 7).Style.NumberFormat.Format = "€#,##0.00";

                if (alternate)
                {
                    for (int i = 1; i <= 7; i++)
                        ws.Cell(row, i).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                row++;
                alternate = !alternate;
            }
            row++;
        }

        public async Task<byte[]> GenerateClientsReportAsync()
        {
            var clients = await _unitOfWork.Clients.GetAllAsync();
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Clients");

            string[] headers = { "ID", "Name", "Type", "Phone", "Email", "Address", "Active" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            int row = 2;
            foreach (var c in clients)
            {
                ws.Cell(row, 1).Value = c.Id;
                ws.Cell(row, 2).Value = c.Name;
                ws.Cell(row, 3).Value = c.Type.ToString();
                ws.Cell(row, 4).Value = c.Phone;
                ws.Cell(row, 5).Value = c.Email;
                ws.Cell(row, 6).Value = c.Address;
                ws.Cell(row, 7).Value = c.IsActive ? "Yes" : "No";
                row++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> GenerateVehiclesReportAsync()
        {
            var vehicles = await _unitOfWork.Vehicles.GetAllAsync();
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Vehicles");

            string[] headers = { "ID", "Chassis", "Make", "Model", "Year", "Registration", "Client", "Active" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            int row = 2;
            foreach (var v in vehicles)
            {
                ws.Cell(row, 1).Value = v.Id;
                ws.Cell(row, 2).Value = v.ChassisNumber;
                ws.Cell(row, 3).Value = v.Make;
                ws.Cell(row, 4).Value = v.Model;
                ws.Cell(row, 5).Value = v.Year;
                ws.Cell(row, 6).Value = v.Registration;
                ws.Cell(row, 7).Value = v.Client?.Name ?? "";
                ws.Cell(row, 8).Value = v.IsActive ? "Yes" : "No";
                row++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> GenerateMonthlySummaryAsync(int month, int year)
        {
            // (unchanged – keep your existing implementation)
            var fromDate = new DateTime(year, month, 1);
            var toDate = fromDate.AddMonths(1).AddDays(-1);

            var orders = await _unitOfWork.WorkOrders.FindAsync(w =>
                w.OrderDate >= fromDate && w.OrderDate <= toDate);
            var ordersList = orders.ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"{month:00}-{year}");

            worksheet.Cell("A1").Value = $"SK Auto - Monthly Summary {month:00}/{year}";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 14;

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
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> GeneratePSAPerformanceReportAsync(DateTime fromDate, DateTime toDate)
        {
            // (unchanged – keep your existing implementation)
            var orders = await _unitOfWork.WorkOrders.FindAsync(w =>
                w.OrderDate >= fromDate && w.OrderDate <= toDate &&
                w.Vehicle != null && w.Vehicle.Client != null && w.Vehicle.Client.Type == ClientType.PSA);
            var ordersList = orders.ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("PSA Performance");

            worksheet.Cell("A1").Value = $"PSA Performance Report {fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 14;

            worksheet.Cell("A3").Value = "Total PSA Orders:";
            worksheet.Cell("B3").Value = ordersList.Count;

            var totalHours = ordersList
                .SelectMany(o => o.WorkTasks)
                .Where(t => t.ActualMinutes.HasValue)
                .Sum(t => t.ActualMinutes.Value) / 60.0m;

            worksheet.Cell("A4").Value = "Total Hours Worked:";
            worksheet.Cell("B4").Value = totalHours;
            worksheet.Cell("B4").Style.NumberFormat.Format = "0.00";

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
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<string> ExportToCsvAsync<T>(IEnumerable<T> data)
        {
            var type = typeof(T);
            var properties = type.GetProperties();

            var csv = new System.Text.StringBuilder();

            csv.AppendLine(string.Join(",", properties.Select(p => p.Name)));

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

        // Helper methods for week calculations
        private int GetIsoWeek(DateTime date)
        {
            return System.Globalization.CultureInfo.CurrentCulture.Calendar
                .GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }
    }
}