using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Draw;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using System.Globalization;

namespace SKAuto.Export.Pdf
{
    public class PdfReportGenerator
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly string _logoPath;
        private readonly IConfigurationService _configService;
        private readonly ILoggingService _logger;

        public PdfReportGenerator(IUnitOfWork unitOfWork, IConfigurationService configService, ILoggingService logger)
        {
            _unitOfWork = unitOfWork;
            _configService = configService;
            _logger = logger;
            _logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
        }

        private Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || !hex.StartsWith("#"))
                return ColorConstants.BLACK;
            var r = int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber);
            var g = int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber);
            var b = int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber);
            return new DeviceRgb(r, g, b);
        }

        private PdfFont GetFont(string fontFamily, bool bold = false)
        {
            return fontFamily?.ToLowerInvariant() switch
            {
                "times" or "times new roman" => PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN),
                "courier" => PdfFontFactory.CreateFont(StandardFonts.COURIER),
                _ => PdfFontFactory.CreateFont(StandardFonts.HELVETICA)
            };
        }

        private Cell CreateCell(string text, PdfFont font, Color borderColor, TextAlignment alignment = TextAlignment.LEFT)
        {
            string safeText = text ?? "";
            return new Cell()
                .Add(new Paragraph(safeText).SetFont(font).SetFontSize(9))
                .SetTextAlignment(alignment)
                .SetBorder(new SolidBorder(borderColor, 1))
                .SetPadding(4);
        }

        private Cell CreateHeaderCell(string text, PdfFont font, Color bg, Color fg)
        {
            return new Cell()
                .Add(new Paragraph(text).SetFont(font).SetFontSize(10).SetFontColor(fg))
                .SetBackgroundColor(bg).SetTextAlignment(TextAlignment.CENTER)
                .SetBorder(new SolidBorder(ColorConstants.BLACK, 1)).SetPadding(5);
        }

        private void AddHeader(Document document, string title, PdfFont boldFont, PdfFont normalFont, Color headerBg, Color borderColor)
        {
            if (File.Exists(_logoPath))
            {
                try
                {
                    ImageData imageData = ImageDataFactory.Create(_logoPath);
                    Image logo = new Image(imageData).ScaleToFit(120, 120);
                    document.Add(logo);
                }
                catch (Exception ex) { _logger.LogWarning($"Could not load logo: {ex.Message}"); }
            }
            Paragraph titlePara = new Paragraph(title)
                .SetFont(boldFont).SetFontSize(12).SetFontColor(headerBg).SetMarginBottom(10);
            document.Add(titlePara);
            SolidLine line = new SolidLine(1f);
            line.SetColor(borderColor);
            document.Add(new LineSeparator(line));
            document.Add(new Paragraph(" ").SetFont(normalFont));
        }

        private void AddFilterSummary(Document document, ReportFilter filter, Dictionary<int, string> accessories,
            Color bg, Color border, PdfFont font)
        {
            Paragraph summary = new Paragraph()
                .SetBackgroundColor(bg).SetPadding(8).SetBorder(new SolidBorder(border, 1))
                .SetMarginBottom(15).SetFont(font);
            summary.Add($"Period: {filter.From:dd/MM/yyyy} – {filter.To:dd/MM/yyyy}\n");
            summary.Add($"Task Type: {(filter.TaskType.HasValue ? filter.TaskType.Value.ToString() : "All")}\n");
            summary.Add($"Status: {(filter.WorkStatus.HasValue ? filter.WorkStatus.Value.ToString() : "All")}\n");
            if (filter.AccessoryId.HasValue && accessories.TryGetValue(filter.AccessoryId.Value, out var accName))
                summary.Add($"Accessory: {accName}\n");
            else
                summary.Add($"Accessory: All\n");
            summary.Add($"Group by Week: {(filter.GroupByWeek ? "Yes" : "No")}");
            if (filter.SummaryOnly)
                summary.Add($"\nSummary Only: Yes");
            document.Add(summary);
        }

        // ========== WORK ORDERS REPORT ==========
        public async Task<byte[]> GenerateWorkOrdersReportAsync(ReportFilter filter, bool showPrice, bool showTime)
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

            var appConfig = await _configService.GetAsync<AppConfig>("AppConfig") ?? new AppConfig();
            var colors = appConfig.ReportColors;
            var fonts = appConfig.ReportFonts;
            var headerBg = HexToColor(colors.HeaderBackground);
            var headerFg = HexToColor(colors.HeaderForeground);
            var altRowBg = HexToColor(colors.AlternateRowBackground);
            var borderColor = HexToColor(colors.Border);
            var totalBg = HexToColor(colors.TotalBackground);
            var headerFont = GetFont(fonts.FontFamily, true);
            var normalFont = GetFont(fonts.FontFamily, false);

            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4.Rotate());
            document.SetMargins(36, 36, 36, 36);

            var culture = CultureInfo.CurrentUICulture;
            bool isFrench = culture.TwoLetterISOLanguageName == "fr";

            string title = isFrench ? "Rapport des ordres de travail" : "Work Orders Report";
            string noDataMsg = isFrench ? "Aucun enregistrement ne correspond aux filtres sélectionnés." : "No records match the selected filters.";

            AddHeader(document, title, headerFont, normalFont, headerBg, borderColor);

            // Filter summary (localized)
            Paragraph summary = new Paragraph()
                .SetBackgroundColor(altRowBg).SetPadding(8).SetBorder(new SolidBorder(borderColor, 1))
                .SetMarginBottom(15).SetFont(normalFont);
            summary.Add($"{(isFrench ? "Période" : "Period")}: {filter.From:dd/MM/yyyy} – {filter.To:dd/MM/yyyy}\n");
            summary.Add($"Task Type: {(filter.TaskType.HasValue ? filter.TaskType.Value.ToString() : "All")}\n");
            summary.Add($"Status: {(filter.WorkStatus.HasValue ? filter.WorkStatus.Value.ToString() : "All")}\n");
            summary.Add($"Order Type: {(filter.OrderType.HasValue ? filter.OrderType.Value.GetDisplayName() : "All")}\n");
            summary.Add($"Clients: {(filter.ClientIds != null && filter.ClientIds.Any() ? "Selected" : "All")}\n");
            if (filter.AccessoryId.HasValue && accessories.TryGetValue(filter.AccessoryId.Value, out var accName))
                summary.Add($"Accessory: {accName}\n");
            else
                summary.Add($"Accessory: All\n");
            summary.Add($"Group by Week: {(filter.GroupByWeek ? "Yes" : "No")}");
            if (filter.SummaryOnly)
                summary.Add($"\n{(isFrench ? "Résumé uniquement" : "Summary Only")}: Yes");
            document.Add(summary);

            if (!reportRows.Any())
            {
                // Generate empty report with headers and "No data" message
                Table emptyTable = new Table(GetColumnCount(showPrice, showTime)).UseAllAvailableWidth();
                emptyTable.SetMarginTop(10);
                string[] headers = GetBaseHeaders(culture, showPrice, showTime);
                foreach (string h in headers)
                {
                    Cell headerCell = new Cell()
                        .Add(new Paragraph(h).SetFont(headerFont).SetFontSize(10).SetFontColor(headerFg))
                        .SetBackgroundColor(headerBg).SetTextAlignment(TextAlignment.CENTER)
                        .SetBorder(new SolidBorder(borderColor, 1)).SetPadding(6);
                    emptyTable.AddCell(headerCell);
                }
                Cell noDataCell = new Cell(1, GetColumnCount(showPrice, showTime))
                    .Add(new Paragraph(noDataMsg).SetFont(normalFont).SetFontSize(11))
                    .SetTextAlignment(TextAlignment.CENTER);
                emptyTable.AddCell(noDataCell);
                document.Add(emptyTable);
                document.Close();
                return ms.ToArray();
            }

            if (filter.GroupByWeek)
            {
                var weekGroups = reportRows.GroupBy(r => GetIsoWeek(r.WorkOrder.OrderDate)).OrderBy(g => g.Key);
                foreach (var group in weekGroups)
                {
                    int week = group.Key;
                    DateTime weekStart = GetStartOfWeek(group.First().WorkOrder.OrderDate);
                    DateTime weekEnd = weekStart.AddDays(6);
                    Paragraph weekHeader = new Paragraph()
                        .Add($"Week {week} ({weekStart:dd/MM/yyyy} – {weekEnd:dd/MM/yyyy})")
                        .SetFont(headerFont).SetFontSize(12).SetFontColor(headerBg)
                        .SetMarginTop(15).SetMarginBottom(5);
                    document.Add(weekHeader);
                    WriteReportRows(document, group.ToList(), normalFont, headerBg, headerFg, borderColor, altRowBg, showPrice, showTime);
                    decimal weekTotal = group.Sum(r => r.Amount);
                    Paragraph weekTotalPara = new Paragraph()
                        .Add($"Week Total: {weekTotal:C}").SetFont(headerFont).SetFontSize(11)
                        .SetTextAlignment(TextAlignment.RIGHT).SetMarginTop(5).SetMarginBottom(15)
                        .SetBackgroundColor(totalBg).SetPadding(5);
                    document.Add(weekTotalPara);
                }
            }
            else
            {
                WriteReportRows(document, reportRows, normalFont, headerBg, headerFg, borderColor, altRowBg, showPrice, showTime);
            }

            if (!filter.SummaryOnly && reportRows.Any())
            {
                decimal grandTotal = reportRows.Sum(r => r.Amount);
                Paragraph grandTotalPara = new Paragraph()
                    .Add($"GRAND TOTAL: {grandTotal:C}").SetFont(headerFont).SetFontSize(14)
                    .SetTextAlignment(TextAlignment.RIGHT).SetMarginTop(20)
                    .SetBackgroundColor(totalBg).SetPadding(8);
                document.Add(grandTotalPara);
            }

            document.Close();
            _logger.LogInfo("PDF work orders report generation completed");
            return ms.ToArray();
        }

        private int GetColumnCount(bool showPrice, bool showTime)
        {
            int baseCol = 6;
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

        private void WriteReportRows(Document document, List<ReportRow> rows, PdfFont normalFont,
            Color headerBg, Color headerFg, Color borderColor, Color altRowBg,
            bool showPrice, bool showTime)
        {
            rows = rows.OrderBy(r => r.WorkOrder.Id).ThenBy(r => r.IsTravelRow ? 0 : 1).ToList();

            int colCount = GetColumnCount(showPrice, showTime);
            Table table = new Table(colCount).UseAllAvailableWidth();
            table.SetMarginTop(10).SetMarginBottom(10);

            var culture = CultureInfo.CurrentUICulture;
            string[] headers = GetBaseHeaders(culture, showPrice, showTime);
            foreach (string h in headers)
            {
                Cell headerCell = new Cell()
                    .Add(new Paragraph(h).SetFont(normalFont).SetFontSize(10).SetFontColor(headerFg))
                    .SetBackgroundColor(headerBg).SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(borderColor, 1)).SetPadding(6);
                table.AddCell(headerCell);
            }

            int? currentOrderId = null;
            bool groupAlternate = false;
            Color groupBgColor = altRowBg;

            foreach (var r in rows)
            {
                if (currentOrderId != r.WorkOrder.Id)
                {
                    currentOrderId = r.WorkOrder.Id;
                    groupAlternate = !groupAlternate;
                    groupBgColor = groupAlternate ? new DeviceRgb(212, 212, 212) : ColorConstants.WHITE; // darker gray
                }

                Cell idCell = CreateCell(r.WorkOrder.Id.ToString(), normalFont, borderColor);
                Cell dateCell = CreateCell(r.WorkOrder.OrderDate.ToString("dd/MM/yyyy"), normalFont, borderColor);
                Cell clientCell = CreateCell(r.Client?.Name ?? "", normalFont, borderColor);
                Cell vehicleCell = CreateCell(r.Vehicle?.ChassisNumber ?? "", normalFont, borderColor);
                Cell statusCell = CreateCell(r.WorkOrder.Status.ToString(), normalFont, borderColor);
                Cell descCell = CreateCell(r.Description, normalFont, borderColor);
                Cell amountCell = showPrice ? CreateCell($"{r.Amount:C}", normalFont, borderColor, TextAlignment.RIGHT) : null;
                string timeDisplay = (r.EstimatedMinutes.HasValue && r.EstimatedMinutes.Value > 0) ? r.EstimatedMinutes.Value.ToString() : "–";
                Cell timeCell = showTime ? CreateCell(timeDisplay, normalFont, borderColor) : null;

                idCell.SetBackgroundColor(groupBgColor);
                dateCell.SetBackgroundColor(groupBgColor);
                clientCell.SetBackgroundColor(groupBgColor);
                vehicleCell.SetBackgroundColor(groupBgColor);
                statusCell.SetBackgroundColor(groupBgColor);
                descCell.SetBackgroundColor(groupBgColor);
                if (amountCell != null) amountCell.SetBackgroundColor(groupBgColor);
                if (timeCell != null) timeCell.SetBackgroundColor(groupBgColor);

                if (rows.LastOrDefault() == r || rows.FirstOrDefault(ro => ro.WorkOrder.Id != currentOrderId) == r)
                {
                    idCell.SetBorderBottom(new SolidBorder(ColorConstants.DARK_GRAY, 2));
                    dateCell.SetBorderBottom(new SolidBorder(ColorConstants.DARK_GRAY, 2));
                    clientCell.SetBorderBottom(new SolidBorder(ColorConstants.DARK_GRAY, 2));
                    vehicleCell.SetBorderBottom(new SolidBorder(ColorConstants.DARK_GRAY, 2));
                    statusCell.SetBorderBottom(new SolidBorder(ColorConstants.DARK_GRAY, 2));
                    descCell.SetBorderBottom(new SolidBorder(ColorConstants.DARK_GRAY, 2));
                    if (amountCell != null) amountCell.SetBorderBottom(new SolidBorder(ColorConstants.DARK_GRAY, 2));
                    if (timeCell != null) timeCell.SetBorderBottom(new SolidBorder(ColorConstants.DARK_GRAY, 2));
                }

                table.AddCell(idCell);
                table.AddCell(dateCell);
                table.AddCell(clientCell);
                table.AddCell(vehicleCell);
                table.AddCell(statusCell);
                table.AddCell(descCell);
                if (showPrice) table.AddCell(amountCell);
                if (showTime) table.AddCell(timeCell);
            }

            document.Add(table);
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
            var appConfig = await _configService.GetAsync<AppConfig>("AppConfig") ?? new AppConfig();
            var colors = appConfig.ReportColors;
            var fonts = appConfig.ReportFonts;
            var headerBg = HexToColor(colors.HeaderBackground);
            var headerFg = HexToColor(colors.HeaderForeground);
            var borderColor = HexToColor(colors.Border);
            var altRowBg = HexToColor(colors.AlternateRowBackground);
            var headerFont = GetFont(fonts.FontFamily, true);
            var normalFont = GetFont(fonts.FontFamily, false);

            var clients = await _unitOfWork.Clients.GetAllAsync();
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4);
            document.SetMargins(36, 36, 36, 36);
            AddHeader(document, "Clients Report", headerFont, normalFont, headerBg, borderColor);

            Table table = new Table(6).UseAllAvailableWidth();
            table.SetMarginTop(10);
            string[] headers = { "ID", "Name", "Type", "Phone", "Email", "Active" };
            foreach (string h in headers)
            {
                Cell headerCell = new Cell()
                    .Add(new Paragraph(h).SetFont(headerFont).SetFontSize(10).SetFontColor(headerFg))
                    .SetBackgroundColor(headerBg).SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(borderColor, 1)).SetPadding(6);
                table.AddCell(headerCell);
            }

            bool alternate = false;
            foreach (var c in clients.OrderBy(c => c.Name))
            {
                Cell idCell = CreateCell(c.Id.ToString(), normalFont, borderColor);
                Cell nameCell = CreateCell(c.Name, normalFont, borderColor);
                Cell typeCell = CreateCell(c.Type.ToString(), normalFont, borderColor);
                Cell phoneCell = CreateCell(c.Phone ?? "", normalFont, borderColor);
                Cell emailCell = CreateCell(c.Email ?? "", normalFont, borderColor);
                Cell activeCell = CreateCell(c.IsActive ? "Yes" : "No", normalFont, borderColor, TextAlignment.CENTER);
                if (alternate)
                {
                    idCell.SetBackgroundColor(altRowBg);
                    nameCell.SetBackgroundColor(altRowBg);
                    typeCell.SetBackgroundColor(altRowBg);
                    phoneCell.SetBackgroundColor(altRowBg);
                    emailCell.SetBackgroundColor(altRowBg);
                    activeCell.SetBackgroundColor(altRowBg);
                }
                table.AddCell(idCell);
                table.AddCell(nameCell);
                table.AddCell(typeCell);
                table.AddCell(phoneCell);
                table.AddCell(emailCell);
                table.AddCell(activeCell);
                alternate = !alternate;
            }
            document.Add(table);
            document.Close();
            _logger.LogInfo($"Clients report generated with {clients.Count()} clients");
            return ms.ToArray();
        }

        // ========== VEHICLES REPORT ==========
        public async Task<byte[]> GenerateVehiclesReportAsync()
        {
            _logger.LogInfo("GenerateVehiclesReportAsync started");
            var appConfig = await _configService.GetAsync<AppConfig>("AppConfig") ?? new AppConfig();
            var colors = appConfig.ReportColors;
            var fonts = appConfig.ReportFonts;
            var headerBg = HexToColor(colors.HeaderBackground);
            var headerFg = HexToColor(colors.HeaderForeground);
            var borderColor = HexToColor(colors.Border);
            var altRowBg = HexToColor(colors.AlternateRowBackground);
            var headerFont = GetFont(fonts.FontFamily, true);
            var normalFont = GetFont(fonts.FontFamily, false);

            var vehicles = await _unitOfWork.Vehicles.GetAllAsync();
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4);
            document.SetMargins(36, 36, 36, 36);
            AddHeader(document, "Vehicles Report", headerFont, normalFont, headerBg, borderColor);

            Table table = new Table(6).UseAllAvailableWidth();
            table.SetMarginTop(10);
            string[] headers = { "ID", "Chassis", "Make", "Model", "Year", "Client" };
            foreach (string h in headers)
            {
                Cell headerCell = new Cell()
                    .Add(new Paragraph(h).SetFont(headerFont).SetFontSize(10).SetFontColor(headerFg))
                    .SetBackgroundColor(headerBg).SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(borderColor, 1)).SetPadding(6);
                table.AddCell(headerCell);
            }

            bool alternate = false;
            foreach (var v in vehicles.OrderBy(v => v.ChassisNumber))
            {
                Cell idCell = CreateCell(v.Id.ToString(), normalFont, borderColor);
                Cell chassisCell = CreateCell(v.ChassisNumber, normalFont, borderColor);
                Cell makeCell = CreateCell(v.Make ?? "", normalFont, borderColor);
                Cell modelCell = CreateCell(v.Model ?? "", normalFont, borderColor);
                Cell yearCell = CreateCell(v.Year?.ToString() ?? "", normalFont, borderColor);
                Cell clientCell = CreateCell(v.Client?.Name ?? "", normalFont, borderColor);
                if (alternate)
                {
                    idCell.SetBackgroundColor(altRowBg);
                    chassisCell.SetBackgroundColor(altRowBg);
                    makeCell.SetBackgroundColor(altRowBg);
                    modelCell.SetBackgroundColor(altRowBg);
                    yearCell.SetBackgroundColor(altRowBg);
                    clientCell.SetBackgroundColor(altRowBg);
                }
                table.AddCell(idCell);
                table.AddCell(chassisCell);
                table.AddCell(makeCell);
                table.AddCell(modelCell);
                table.AddCell(yearCell);
                table.AddCell(clientCell);
                alternate = !alternate;
            }
            document.Add(table);
            document.Close();
            _logger.LogInfo($"Vehicles report generated with {vehicles.Count()} vehicles");
            return ms.ToArray();
        }

        // ========== TASKS REPORT (Accessories) ==========
        public async Task<byte[]> GenerateTasksReportAsync(ReportFilter filter)
        {
            _logger.LogInfo("GenerateTasksReportAsync started");

            var appConfig = await _configService.GetAsync<AppConfig>("AppConfig") ?? new AppConfig();
            var colors = appConfig.ReportColors;
            var fonts = appConfig.ReportFonts;
            var headerBg = HexToColor(colors.HeaderBackground);
            var headerFg = HexToColor(colors.HeaderForeground);
            var altRowBg = HexToColor(colors.AlternateRowBackground);
            var borderColor = HexToColor(colors.Border);
            var headerFont = GetFont(fonts.FontFamily, true);
            var normalFont = GetFont(fonts.FontFamily, false);

            var accessories = await _unitOfWork.Accessories.GetAllAsync();
            var list = accessories.OrderBy(a => a.Name).ToList();

            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4.Rotate());
            document.SetMargins(36, 36, 36, 36);

            AddHeader(document, "Tasks Report", headerFont, normalFont, headerBg, borderColor);
            document.Add(new Paragraph($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}").SetFont(normalFont));
            document.Add(new Paragraph(" "));

            if (filter.SummaryOnly)
            {
                Table summaryTable = new Table(2).UseAllAvailableWidth();
                summaryTable.AddCell(CreateHeaderCell("Summary", headerFont, headerBg, headerFg));
                summaryTable.AddCell(CreateHeaderCell("Value", headerFont, headerBg, headerFg));
                summaryTable.AddCell(CreateCell("Total Tasks:", normalFont, borderColor));
                summaryTable.AddCell(CreateCell(list.Count.ToString(), normalFont, borderColor));
                summaryTable.AddCell(CreateCell("Active Tasks:", normalFont, borderColor));
                summaryTable.AddCell(CreateCell(list.Count(a => a.IsActive).ToString(), normalFont, borderColor));
                summaryTable.AddCell(CreateCell("Inactive Tasks:", normalFont, borderColor));
                summaryTable.AddCell(CreateCell(list.Count(a => !a.IsActive).ToString(), normalFont, borderColor));
                summaryTable.AddCell(CreateCell("Average Price:", normalFont, borderColor));
                summaryTable.AddCell(CreateCell($"{list.Average(a => a.Price ?? 0):C}", normalFont, borderColor, TextAlignment.RIGHT));
                document.Add(summaryTable);
            }
            else
            {
                if (filter.GroupByTaskType)
                {
                    var groups = list.GroupBy(a => a.TaskType).OrderBy(g => g.Key);
                    foreach (var group in groups)
                    {
                        Paragraph groupTitle = new Paragraph(group.Key.ToString())
                            .SetFont(headerFont).SetFontSize(11).SetFontColor(headerBg).SetMarginTop(10);
                        document.Add(groupTitle);
                        WriteTaskTable(document, group.ToList(), normalFont, headerBg, headerFg, borderColor, altRowBg);
                    }
                }
                else
                {
                    WriteTaskTable(document, list, normalFont, headerBg, headerFg, borderColor, altRowBg);
                }
            }

            document.Close();
            _logger.LogInfo("Tasks report generation completed");
            return ms.ToArray();
        }

        private void WriteTaskTable(Document document, List<Accessory> tasks, PdfFont normalFont,
            Color headerBg, Color headerFg, Color borderColor, Color altRowBg)
        {
            Table table = new Table(8).UseAllAvailableWidth();
            table.SetMarginTop(5).SetMarginBottom(5);
            string[] headers = { "Name", "Part #", "Description", "Time", "Price", "Type", "Password", "Active" };
            foreach (string h in headers)
            {
                Cell headerCell = new Cell()
                    .Add(new Paragraph(h).SetFont(normalFont).SetFontSize(10).SetFontColor(headerFg))
                    .SetBackgroundColor(headerBg).SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(borderColor, 1)).SetPadding(5);
                table.AddCell(headerCell);
            }

            bool alternate = false;
            foreach (var t in tasks)
            {
                string name = t.Name ?? "";
                string partNumber = t.PartNumber ?? "";
                string description = t.Description ?? "";
                string time = t.Time?.ToString() ?? "";
                string price = t.Price.HasValue ? t.Price.Value.ToString("C") : "€0.00";
                string taskType = t.TaskType.ToString();
                string requiresPassword = t.RequiresPassword ? "Yes" : "No";
                string isActive = t.IsActive ? "Yes" : "No";

                Cell nameCell = CreateCell(name, normalFont, borderColor);
                Cell partCell = CreateCell(partNumber, normalFont, borderColor);
                Cell descCell = CreateCell(description, normalFont, borderColor);
                Cell timeCell = CreateCell(time, normalFont, borderColor);
                Cell priceCell = CreateCell(price, normalFont, borderColor, TextAlignment.RIGHT);
                Cell typeCell = CreateCell(taskType, normalFont, borderColor);
                Cell passwordCell = CreateCell(requiresPassword, normalFont, borderColor);
                Cell activeCell = CreateCell(isActive, normalFont, borderColor);

                if (alternate)
                {
                    nameCell.SetBackgroundColor(altRowBg);
                    partCell.SetBackgroundColor(altRowBg);
                    descCell.SetBackgroundColor(altRowBg);
                    timeCell.SetBackgroundColor(altRowBg);
                    priceCell.SetBackgroundColor(altRowBg);
                    typeCell.SetBackgroundColor(altRowBg);
                    passwordCell.SetBackgroundColor(altRowBg);
                    activeCell.SetBackgroundColor(altRowBg);
                }

                table.AddCell(nameCell);
                table.AddCell(partCell);
                table.AddCell(descCell);
                table.AddCell(timeCell);
                table.AddCell(priceCell);
                table.AddCell(typeCell);
                table.AddCell(passwordCell);
                table.AddCell(activeCell);

                alternate = !alternate;
            }
            document.Add(table);
        }

        // ========== HELPER METHODS ==========
        private int GetIsoWeek(DateTime date) =>
            CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }
    }
}