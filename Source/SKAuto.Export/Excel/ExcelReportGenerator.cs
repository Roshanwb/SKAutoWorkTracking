using ClosedXML.Excel;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using System.Globalization;

namespace SKAuto.Export.Excel
{
    public class ExcelReportGenerator : IExportService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _logger;

        public ExcelReportGenerator(IUnitOfWork unitOfWork, ILoggingService logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ========== DAILY REPORT ==========
        public async Task<byte[]> GenerateDailyReportAsync(DateTime date)
        {
            _logger.LogInfo($"GenerateDailyReportAsync started for date {date:yyyy-MM-dd}");

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"Work {date:dd-MM-yyyy}");

            worksheet.Cell("A1").Value = "SK Auto - Daily Work Report";
            worksheet.Cell("A2").Value = date.ToString("dd/MM/yyyy");
            worksheet.Cell("A3").Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";

            var headerRange = worksheet.Range("A1:E3");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontSize = 12;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            var orders = await _unitOfWork.WorkOrders.FindAsync(w => w.OrderDate.Date == date.Date);
            var ordersList = orders.ToList();
            _logger.LogInfo($"Found {ordersList.Count} work orders for {date:yyyy-MM-dd}");

            if (!ordersList.Any())
            {
                worksheet.Cell("A5").Value = "No work orders for this date";
                worksheet.Columns().AdjustToContents();
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                _logger.LogInfo("Daily report generated with no orders");
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
            worksheet.Cell("B9").Value = ordersList.Sum(o => o.TotalAmount ?? 0);
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
            _logger.LogInfo($"Daily report generated successfully, {ordersList.Count} orders");
            return memoryStream.ToArray();
        }

        // ========== SINGLE WORK ORDER REPORT ==========
        public async Task<byte[]> GenerateWorkOrderReportAsync(int workOrderId)
        {
            _logger.LogInfo($"GenerateWorkOrderReportAsync started for work order ID {workOrderId}");
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
                worksheet.Cell(row, 1).Value = "Travels";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                row++;

                var travelHeaders = new[] { "Date", "Destination", "Distance (km)", "Cost (€)", "Notes" };
                for (int i = 0; i < travelHeaders.Length; i++)
                {
                    worksheet.Cell(row, i + 1).Value = travelHeaders[i];
                    worksheet.Cell(row, i + 1).Style.Font.Bold = true;
                }
                row++;

                foreach (var travel in order.Travels)
                {
                    worksheet.Cell(row, 1).Value = travel.TravelDate.ToString("dd/MM/yyyy");
                    worksheet.Cell(row, 2).Value = travel.Destination;
                    worksheet.Cell(row, 3).Value = travel.DistanceKm ?? 0;
                    worksheet.Cell(row, 4).Value = travel.TravelCost ?? 0;
                    worksheet.Cell(row, 4).Style.NumberFormat.Format = "€#,##0.00";
                    worksheet.Cell(row, 5).Value = travel.Notes;
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
            _logger.LogInfo($"Work order report generated for ID {workOrderId}");
            return stream.ToArray();
        }

        // ========== WORK ORDERS REPORT ==========
        public async Task<byte[]> GenerateWorkOrdersReportAsync(ReportFilter filter, bool showPrice = true, bool showTime = true)
        {
            _logger.LogInfo($"GenerateWorkOrdersReportAsync started. From {filter.From:yyyy-MM-dd} to {filter.To:yyyy-MM-dd}, " +
                $"TaskType={filter.TaskType}, Status={filter.WorkStatus}, AccessoryId={filter.AccessoryId}, " +
                $"GroupByWeek={filter.GroupByWeek}, SummaryOnly={filter.SummaryOnly}, " +
                $"ClientIds={(filter.ClientIds != null ? string.Join(",", filter.ClientIds) : "All")}, " +
                $"OrderType={filter.OrderType}, ShowPrice={showPrice}, ShowTime={showTime}");

            var orders = (await _unitOfWork.WorkOrders.FindAsync(w => w.OrderDate >= filter.From && w.OrderDate <= filter.To)).ToList();

            if (filter.WorkStatus.HasValue)
                orders = orders.Where(o => o.Status == filter.WorkStatus.Value).ToList();

            if (filter.ClientIds != null && filter.ClientIds.Any())
            {
                var clientIdSet = filter.ClientIds.ToHashSet();
                orders = orders.Where(o => o.Vehicle != null && clientIdSet.Contains(o.Vehicle.ClientId)).ToList();
            }

            if (filter.OrderType.HasValue)
                orders = orders.Where(o => o.OrderType == filter.OrderType.Value).ToList();

            var vehicleIds = orders.Select(o => o.VehicleId).Distinct().ToList();
            var vehicles = (await _unitOfWork.Vehicles.FindAsync(v => vehicleIds.Contains(v.Id))).ToDictionary(v => v.Id);
            var clientIds = vehicles.Values.Select(v => v.ClientId).Distinct().ToList();
            var clients = (await _unitOfWork.Clients.FindAsync(c => clientIds.Contains(c.Id))).ToDictionary(c => c.Id);

            var orderIds = orders.Select(o => o.Id).ToList();
            var allTasks = (await _unitOfWork.WorkTasks.FindAsync(t => orderIds.Contains(t.WorkOrderId))).ToList();
            var allTravels = (await _unitOfWork.Travels.FindAsync(t => orderIds.Contains(t.WorkOrderId))).ToList();

            var accessoryIds = allTasks.Select(t => t.AccessoryId).Distinct().ToList();
            var accessories = (await _unitOfWork.Accessories.FindAsync(a => accessoryIds.Contains(a.Id)))
                .ToDictionary(a => a.Id, a => a.Name);

            var travelOrderIds = allTravels.Select(t => t.WorkOrderId).Distinct().ToHashSet();

            var reportRows = new List<ReportRow>();
            foreach (var wo in orders)
            {
                bool hasTravel = travelOrderIds.Contains(wo.Id);
                var tasks = allTasks.Where(t => t.WorkOrderId == wo.Id).ToList();

                if (filter.TaskType.HasValue)
                {
                    if (filter.TaskType.Value == TaskType.Travel)
                    {
                        if (!hasTravel)
                            tasks.Clear();
                    }
                    else
                    {
                        if (hasTravel)
                            tasks.Clear();
                        else
                            tasks = tasks.Where(t => t.TaskType == filter.TaskType.Value).ToList();
                    }
                }

                if (filter.AccessoryId.HasValue && tasks.Any())
                    tasks = tasks.Where(t => t.AccessoryId == filter.AccessoryId.Value).ToList();

                foreach (var task in tasks)
                {
                    decimal amount = (task.Price ?? 0) * task.Quantity;
                    string desc = accessories.GetValueOrDefault(task.AccessoryId, "?");
                    if (hasTravel)
                        desc = "Travel – " + desc;
                    reportRows.Add(new ReportRow
                    {
                        WorkOrder = wo,
                        Description = desc,
                        Amount = amount,
                        Vehicle = vehicles.GetValueOrDefault(wo.VehicleId),
                        Client = vehicles.TryGetValue(wo.VehicleId, out var veh) && clients.TryGetValue(veh.ClientId, out var cl) ? cl : null,
                        HasTravel = hasTravel,
                        IsTravelRow = false,
                        EstimatedMinutes = task.EstimatedMinutes
                    });
                }

                if (!filter.TaskType.HasValue || filter.TaskType.Value == TaskType.Travel)
                {
                    var travels = allTravels.Where(t => t.WorkOrderId == wo.Id).ToList();
                    foreach (var travel in travels)
                    {
                        string desc = $"Travel to {travel.Destination} ({travel.DistanceKm} km)";
                        decimal amount = travel.TravelCost ?? 0;
                        reportRows.Add(new ReportRow
                        {
                            WorkOrder = wo,
                            Description = desc,
                            Amount = amount,
                            Vehicle = vehicles.GetValueOrDefault(wo.VehicleId),
                            Client = vehicles.TryGetValue(wo.VehicleId, out var veh2) && clients.TryGetValue(veh2.ClientId, out var cl2) ? cl2 : null,
                            HasTravel = true,
                            IsTravelRow = true,
                            EstimatedMinutes = null
                        });
                    }
                }
            }

            reportRows = reportRows.Where(r => r.Amount > 0 || !string.IsNullOrEmpty(r.Description)).ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Work Orders");

            var culture = CultureInfo.CurrentUICulture;
            bool isFrench = culture.TwoLetterISOLanguageName == "fr";

            string title = isFrench ? "Rapport des ordres de travail" : "Work Orders Report";
            string periodLabel = isFrench ? "Période" : "Period";
            string generatedLabel = isFrench ? "Généré le" : "Generated";
            string noDataMsg = isFrench ? "Aucun enregistrement ne correspond aux filtres sélectionnés." : "No records match the selected filters.";

            worksheet.Cell(1, 1).Value = title;
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Cell(2, 1).Value = $"{periodLabel}: {filter.From:dd/MM/yyyy} – {filter.To:dd/MM/yyyy}";
            worksheet.Cell(3, 1).Value = $"{generatedLabel}: {DateTime.Now:dd/MM/yyyy HH:mm}";

            int filterRow = 5;
            worksheet.Cell(filterRow, 1).Value = isFrench ? "Filtres appliqués :" : "Applied Filters:";
            worksheet.Cell(filterRow, 1).Style.Font.Bold = true;
            filterRow++;
            worksheet.Cell(filterRow++, 1).Value = $"Task Type: {(filter.TaskType.HasValue ? filter.TaskType.Value.ToString() : "All")}";
            worksheet.Cell(filterRow++, 1).Value = $"Status: {(filter.WorkStatus.HasValue ? filter.WorkStatus.Value.ToString() : "All")}";
            worksheet.Cell(filterRow++, 1).Value = $"Order Type: {(filter.OrderType.HasValue ? filter.OrderType.Value.GetDisplayName() : "All")}";
            worksheet.Cell(filterRow++, 1).Value = $"Clients: {(filter.ClientIds != null && filter.ClientIds.Any() ? "Selected" : "All")}";
            worksheet.Cell(filterRow++, 1).Value = $"Group by Week: {(filter.GroupByWeek ? "Yes" : "No")}";
            worksheet.Cell(filterRow++, 1).Value = $"Summary Only: {(filter.SummaryOnly ? "Yes" : "No")}";

            int dataStartRow = filterRow + 2;

            if (!reportRows.Any())
            {
                string[] baseHeaders = GetBaseHeaders(culture, showPrice, showTime);
                for (int i = 0; i < baseHeaders.Length; i++)
                {
                    worksheet.Cell(dataStartRow, i + 1).Value = baseHeaders[i];
                    worksheet.Cell(dataStartRow, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(dataStartRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
                }
                dataStartRow++;
                worksheet.Cell(dataStartRow, 1).Value = noDataMsg;
                worksheet.Columns().AdjustToContents();
                using var emptyMs = new MemoryStream();
                workbook.SaveAs(emptyMs);
                return emptyMs.ToArray();
            }

            if (filter.SummaryOnly)
            {
                worksheet.Cell(dataStartRow, 1).Value = isFrench ? "Statistiques récapitulatives" : "Summary Statistics";
                worksheet.Cell(dataStartRow, 1).Style.Font.Bold = true;
                dataStartRow++;
                worksheet.Cell(dataStartRow, 1).Value = "Total Rows:";
                worksheet.Cell(dataStartRow, 2).Value = reportRows.Count;
                dataStartRow++;
                worksheet.Cell(dataStartRow, 1).Value = "Total Revenue:";
                worksheet.Cell(dataStartRow, 2).Value = reportRows.Sum(r => r.Amount);
                worksheet.Cell(dataStartRow, 2).Style.NumberFormat.Format = "€#,##0.00";
                dataStartRow++;
            }
            else
            {
                if (filter.GroupByWeek)
                {
                    var weekGroups = reportRows.GroupBy(r => GetIsoWeek(r.WorkOrder.OrderDate)).OrderBy(g => g.Key);
                    foreach (var group in weekGroups)
                    {
                        int week = group.Key;
                        DateTime weekStart = GetStartOfWeek(group.First().WorkOrder.OrderDate);
                        DateTime weekEnd = weekStart.AddDays(6);
                        worksheet.Cell(dataStartRow, 1).Value = $"Week {week} ({weekStart:dd/MM} – {weekEnd:dd/MM})";
                        worksheet.Cell(dataStartRow, 1).Style.Font.Bold = true;
                        worksheet.Cell(dataStartRow, 1).Style.Font.FontSize = 12;
                        dataStartRow++;
                        WriteReportRows(worksheet, ref dataStartRow, group.ToList(), showPrice, showTime);
                        decimal weekTotal = group.Sum(r => r.Amount);
                        int weekLastCol = GetLastColumnIndex(showPrice, showTime);
                        worksheet.Cell(dataStartRow, weekLastCol).Value = $"Week Total: €{weekTotal:0.00}";
                        worksheet.Cell(dataStartRow, weekLastCol).Style.Font.Bold = true;
                        worksheet.Cell(dataStartRow, weekLastCol).Style.NumberFormat.Format = "€#,##0.00";
                        dataStartRow += 2;
                    }
                }
                else
                {
                    WriteReportRows(worksheet, ref dataStartRow, reportRows, showPrice, showTime);
                }
            }

            decimal grandTotal = reportRows.Sum(r => r.Amount);
            int lastCol = GetLastColumnIndex(showPrice, showTime);
            worksheet.Cell(dataStartRow, lastCol).Value = $"GRAND TOTAL: €{grandTotal:0.00}";
            worksheet.Cell(dataStartRow, lastCol).Style.Font.Bold = true;
            worksheet.Cell(dataStartRow, lastCol).Style.NumberFormat.Format = "€#,##0.00";

            worksheet.Columns().AdjustToContents();
            using var finalMs = new MemoryStream();
            workbook.SaveAs(finalMs);
            _logger.LogInfo("Work orders report generation completed");
            return finalMs.ToArray();
        }

        private int GetLastColumnIndex(bool showPrice, bool showTime)
        {
            int baseCol = 6; // ID, Date, Client, Vehicle, Status, Description
            if (showPrice) baseCol++;
            if (showTime) baseCol++;
            return baseCol;
        }

        private string[] GetBaseHeaders(CultureInfo culture, bool showPrice, bool showTime)
        {
            bool isFrench = culture.TwoLetterISOLanguageName == "fr";
            var headers = new List<string>();
            headers.Add("ID");
            headers.Add(isFrench ? "Date" : "Date");
            headers.Add(isFrench ? "Client" : "Client");
            headers.Add(isFrench ? "Véhicule" : "Vehicle");
            headers.Add(isFrench ? "Statut" : "Status");
            headers.Add(isFrench ? "Description" : "Description");
            if (showPrice)
                headers.Add(isFrench ? "Montant" : "Amount");
            if (showTime)
                headers.Add(isFrench ? "Temps (min)" : "Time (min)");
            return headers.ToArray();
        }

        private void WriteReportRows(IXLWorksheet ws, ref int row, List<ReportRow> rows, bool showPrice, bool showTime)
        {
            rows = rows.OrderBy(r => r.WorkOrder.Id).ThenBy(r => r.IsTravelRow ? 0 : 1).ToList();

            var culture = CultureInfo.CurrentUICulture;
            string[] headers = GetBaseHeaders(culture, showPrice, showTime);
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
                ws.Cell(row, i + 1).Style.Font.Bold = true;
                ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }
            row++;

            int? currentOrderId = null;
            bool groupAlternate = false;
            XLColor groupBgColor = XLColor.White;

            foreach (var r in rows)
            {
                if (currentOrderId != r.WorkOrder.Id)
                {
                    currentOrderId = r.WorkOrder.Id;
                    groupAlternate = !groupAlternate;
                    groupBgColor = groupAlternate ? XLColor.FromArgb(232, 232, 232) : XLColor.White;

                    if (row > 2)
                    {
                        ws.Row(row - 1).Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                        ws.Row(row - 1).Style.Border.BottomBorderColor = XLColor.DarkGray;
                    }
                }

                int col = 1;
                ws.Cell(row, col).Value = r.WorkOrder.Id;
                ws.Cell(row, col).Style.Fill.BackgroundColor = groupBgColor;
                col++;
                ws.Cell(row, col).Value = r.WorkOrder.OrderDate.ToString("dd/MM/yyyy");
                ws.Cell(row, col).Style.Fill.BackgroundColor = groupBgColor;
                col++;
                ws.Cell(row, col).Value = r.Client?.Name ?? "";
                ws.Cell(row, col).Style.Fill.BackgroundColor = groupBgColor;
                col++;
                ws.Cell(row, col).Value = r.Vehicle?.ChassisNumber ?? "";
                ws.Cell(row, col).Style.Fill.BackgroundColor = groupBgColor;
                col++;
                ws.Cell(row, col).Value = r.WorkOrder.Status.ToString();
                ws.Cell(row, col).Style.Fill.BackgroundColor = groupBgColor;
                col++;
                ws.Cell(row, col).Value = r.Description;
                ws.Cell(row, col).Style.Fill.BackgroundColor = groupBgColor;
                col++;
                if (showPrice)
                {
                    ws.Cell(row, col).Value = r.Amount;
                    ws.Cell(row, col).Style.Fill.BackgroundColor = groupBgColor;
                    ws.Cell(row, col).Style.NumberFormat.Format = "€#,##0.00";
                    col++;
                }
                if (showTime)
                {
                    string timeDisplay = (r.EstimatedMinutes.HasValue && r.EstimatedMinutes.Value > 0) ? r.EstimatedMinutes.Value.ToString() : "–";
                    ws.Cell(row, col).Value = timeDisplay;
                    ws.Cell(row, col).Style.Fill.BackgroundColor = groupBgColor;
                    col++;
                }
                row++;
            }

            if (rows.Any())
            {
                ws.Row(row - 1).Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                ws.Row(row - 1).Style.Border.BottomBorderColor = XLColor.DarkGray;
            }
            row++;
        }

        private class ReportRow
        {
            public WorkOrder WorkOrder { get; set; }
            public string Description { get; set; }
            public decimal Amount { get; set; }
            public Vehicle Vehicle { get; set; }
            public Client Client { get; set; }
            public bool HasTravel { get; set; }
            public bool IsTravelRow { get; set; }
            public int? EstimatedMinutes { get; set; }
        }

        // ========== CLIENTS REPORT ==========
        public async Task<byte[]> GenerateClientsReportAsync()
        {
            _logger.LogInfo("GenerateClientsReportAsync started");
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
            _logger.LogInfo($"Clients report generated with {clients.Count()} clients");
            return ms.ToArray();
        }

        // ========== VEHICLES REPORT ==========
        public async Task<byte[]> GenerateVehiclesReportAsync()
        {
            _logger.LogInfo("GenerateVehiclesReportAsync started");
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
            _logger.LogInfo($"Vehicles report generated with {vehicles.Count()} vehicles");
            return ms.ToArray();
        }

        // ========== MONTHLY SUMMARY ==========
        public async Task<byte[]> GenerateMonthlySummaryAsync(int month, int year)
        {
            _logger.LogInfo($"GenerateMonthlySummaryAsync started for {month}/{year}");
            var fromDate = new DateTime(year, month, 1);
            var toDate = fromDate.AddMonths(1).AddDays(-1);
            var orders = await _unitOfWork.WorkOrders.FindAsync(w => w.OrderDate >= fromDate && w.OrderDate <= toDate);
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
            worksheet.Cell("C4").Value = psaOrders.Sum(o => o.TotalAmount ?? 0);
            worksheet.Cell("C4").Style.NumberFormat.Format = "€#,##0.00";
            worksheet.Cell("A5").Value = "Direct Orders:";
            worksheet.Cell("B5").Value = directOrders.Count;
            worksheet.Cell("C5").Value = directOrders.Sum(o => o.TotalAmount ?? 0);
            worksheet.Cell("C5").Style.NumberFormat.Format = "€#,##0.00";
            worksheet.Cell("A6").Value = "Total:";
            worksheet.Cell("A6").Style.Font.Bold = true;
            worksheet.Cell("B6").Value = ordersList.Count;
            worksheet.Cell("C6").Value = ordersList.Sum(o => o.TotalAmount ?? 0);
            worksheet.Cell("C6").Style.Font.Bold = true;
            worksheet.Cell("C6").Style.NumberFormat.Format = "€#,##0.00";
            int row = 9;
            worksheet.Cell(row, 1).Value = "Daily Breakdown";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            row++;
            var dailyHeaders = new[] { "Date", "Orders", "Completed", "Revenue" };
            for (int i = 0; i < dailyHeaders.Length; i++)
                worksheet.Cell(row, i + 1).Value = dailyHeaders[i];
            row++;
            for (var date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                var dayOrders = ordersList.Where(o => o.OrderDate.Date == date.Date).ToList();
                if (dayOrders.Any())
                {
                    worksheet.Cell(row, 1).Value = date.ToString("dd/MM");
                    worksheet.Cell(row, 2).Value = dayOrders.Count;
                    worksheet.Cell(row, 3).Value = dayOrders.Count(o => o.Status == WorkStatus.Done);
                    worksheet.Cell(row, 4).Value = dayOrders.Sum(o => o.TotalAmount ?? 0);
                    worksheet.Cell(row, 4).Style.NumberFormat.Format = "€#,##0.00";
                    row++;
                }
            }
            worksheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            _logger.LogInfo($"Monthly summary generated for {month}/{year}");
            return stream.ToArray();
        }

        // ========== PSA PERFORMANCE REPORT ==========
        public async Task<byte[]> GeneratePSAPerformanceReportAsync(DateTime fromDate, DateTime toDate)
        {
            _logger.LogInfo($"GeneratePSAPerformanceReportAsync started from {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}");
            var orders = await _unitOfWork.WorkOrders.FindAsync(w => w.OrderDate >= fromDate && w.OrderDate <= toDate &&
                w.Vehicle != null && w.Vehicle.Client != null && w.Vehicle.Client.Type == ClientType.PSA);
            var ordersList = orders.ToList();
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("PSA Performance");
            worksheet.Cell("A1").Value = $"PSA Performance Report {fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 14;
            worksheet.Cell("A3").Value = "Total PSA Orders:";
            worksheet.Cell("B3").Value = ordersList.Count;
            var totalHours = ordersList.SelectMany(o => o.WorkTasks)
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
                worksheet.Cell(row, i + 1).Value = efficiencyHeaders[i];
            row++;
            var accessoryTasks = ordersList.SelectMany(o => o.WorkTasks)
                .Where(t => t.Accessory != null && t.EstimatedMinutes.HasValue && t.ActualMinutes.HasValue)
                .GroupBy(t => t.Accessory!.Name);
            foreach (var group in accessoryTasks)
            {
                var avgEst = group.Average(t => t.EstimatedMinutes!.Value);
                var avgAct = group.Average(t => t.ActualMinutes!.Value);
                var efficiency = avgEst > 0 ? (avgEst / avgAct) * 100 : 100;
                worksheet.Cell(row, 1).Value = group.Key;
                worksheet.Cell(row, 1).Style.NumberFormat.Format = "0";
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
            _logger.LogInfo($"PSA performance report generated");
            return stream.ToArray();
        }

        // ========== CSV EXPORT ==========
        public async Task<string> ExportToCsvAsync<T>(IEnumerable<T> data)
        {
            _logger.LogInfo($"ExportToCsvAsync started for type {typeof(T).Name}");
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
            _logger.LogInfo($"CSV export completed, {data.Count()} records");
            return csv.ToString();
        }

        // ========== TASKS REPORT (Accessories) ==========
        public async Task<byte[]> GenerateTasksReportAsync(ReportFilter filter)
        {
            _logger.LogInfo("GenerateTasksReportAsync started");

            var accessories = await _unitOfWork.Accessories.GetAllAsync();
            var list = accessories.OrderBy(a => a.Name).ToList();

            if (!list.Any())
            {
                using var emptyWorkbook = new XLWorkbook();
                var ws = emptyWorkbook.Worksheets.Add("No Data");
                ws.Cell(1, 1).Value = "No tasks (accessories) found.";
                using var stream = new MemoryStream();
                emptyWorkbook.SaveAs(stream);
                return stream.ToArray();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Tasks");

            worksheet.Cell(1, 1).Value = "Tasks Report";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Cell(2, 1).Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";

            int dataStartRow = 4;

            if (filter.SummaryOnly)
            {
                worksheet.Cell(dataStartRow, 1).Value = "Summary Statistics";
                worksheet.Cell(dataStartRow, 1).Style.Font.Bold = true;
                dataStartRow++;
                worksheet.Cell(dataStartRow, 1).Value = "Total Tasks:";
                worksheet.Cell(dataStartRow, 2).Value = list.Count;
                dataStartRow++;
                worksheet.Cell(dataStartRow, 1).Value = "Total Active Tasks:";
                worksheet.Cell(dataStartRow, 2).Value = list.Count(a => a.IsActive);
                dataStartRow++;
                worksheet.Cell(dataStartRow, 1).Value = "Total Inactive Tasks:";
                worksheet.Cell(dataStartRow, 2).Value = list.Count(a => !a.IsActive);
                dataStartRow++;
                worksheet.Cell(dataStartRow, 1).Value = "Average Price:";
                worksheet.Cell(dataStartRow, 2).Value = list.Average(a => a.Price ?? 0);
                worksheet.Cell(dataStartRow, 2).Style.NumberFormat.Format = "€#,##0.00";
                dataStartRow++;
            }
            else
            {
                if (filter.GroupByTaskType)
                {
                    var groups = list.GroupBy(a => a.TaskType).OrderBy(g => g.Key);
                    foreach (var group in groups)
                    {
                        worksheet.Cell(dataStartRow, 1).Value = group.Key.ToString();
                        worksheet.Cell(dataStartRow, 1).Style.Font.Bold = true;
                        worksheet.Cell(dataStartRow, 1).Style.Font.FontSize = 12;
                        dataStartRow++;
                        WriteTaskTable(worksheet, ref dataStartRow, group.ToList());
                        dataStartRow++;
                    }
                }
                else
                {
                    WriteTaskTable(worksheet, ref dataStartRow, list);
                }
            }

            worksheet.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            _logger.LogInfo("Tasks report generation completed");
            return ms.ToArray();
        }

        private void WriteTaskTable(IXLWorksheet ws, ref int row, List<Accessory> tasks)
        {
            string[] headers = { "Name", "Part Number", "Description", "Time (min)", "Price (€)", "Task Type", "Requires Password", "Active" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
                ws.Cell(row, i + 1).Style.Font.Bold = true;
                ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }
            row++;

            foreach (var t in tasks)
            {
                ws.Cell(row, 1).Value = t.Name;
                ws.Cell(row, 2).Value = t.PartNumber;
                ws.Cell(row, 3).Value = t.Description;
                ws.Cell(row, 4).Value = t.Time;
                ws.Cell(row, 5).Value = t.Price ?? 0;
                ws.Cell(row, 5).Style.NumberFormat.Format = "€#,##0.00";
                ws.Cell(row, 6).Value = t.TaskType.ToString();
                ws.Cell(row, 7).Value = t.RequiresPassword ? "Yes" : "No";
                ws.Cell(row, 8).Value = t.IsActive ? "Yes" : "No";
                row++;
            }
        }

        // ========== HELPER METHODS ==========
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