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
using SKAuto.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace SKAuto.Export.Pdf
{
    public class PdfReportGenerator
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly string _logoPath;
        private readonly IConfigurationService _configService;

        public PdfReportGenerator(IUnitOfWork unitOfWork, IConfigurationService configService)
        {
            _unitOfWork = unitOfWork;
            _configService = configService;
            _logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
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

        public async Task<byte[]> GenerateWorkOrdersReportAsync(ReportFilter filter)
        {
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

            var orders = (await _unitOfWork.WorkOrders
                .FindAsync(w => w.OrderDate >= filter.From && w.OrderDate <= filter.To)).ToList();

            var vehicleIds = orders.Select(o => o.VehicleId).Distinct().ToList();
            var vehicles = (await _unitOfWork.Vehicles.FindAsync(v => vehicleIds.Contains(v.Id))).ToDictionary(v => v.Id);
            var clientIds = vehicles.Values.Select(v => v.ClientId).Distinct().ToList();
            var clients = (await _unitOfWork.Clients.FindAsync(c => clientIds.Contains(c.Id))).ToDictionary(c => c.Id);

            var orderIds = orders.Select(o => o.Id).ToList();
            var allTasks = (await _unitOfWork.WorkTasks.FindAsync(t => orderIds.Contains(t.WorkOrderId))).ToList();
            var taskLookup = allTasks.ToLookup(t => t.WorkOrderId);

            var accessoryIds = allTasks.Select(t => t.AccessoryId).Distinct().ToList();
            var accessories = (await _unitOfWork.Accessories.FindAsync(a => accessoryIds.Contains(a.Id)))
                .ToDictionary(a => a.Id, a => a.Name);

            if (filter.TaskType.HasValue)
            {
                var ordersWithTask = allTasks
                    .Where(t => t.TaskType == filter.TaskType.Value)
                    .Select(t => t.WorkOrderId)
                    .Distinct()
                    .ToHashSet();
                orders = orders.Where(o => ordersWithTask.Contains(o.Id)).ToList();
            }

            if (filter.WorkStatus.HasValue)
            {
                orders = orders.Where(o => o.Status == filter.WorkStatus.Value).ToList();
            }

            if (filter.AccessoryId.HasValue)
            {
                var ordersWithAccessory = allTasks
                    .Where(t => t.AccessoryId == filter.AccessoryId.Value)
                    .Select(t => t.WorkOrderId)
                    .Distinct()
                    .ToHashSet();
                orders = orders.Where(o => ordersWithAccessory.Contains(o.Id)).ToList();
            }

            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4.Rotate());
            document.SetMargins(36, 36, 36, 36);

            AddHeader(document, "Work Orders Report", headerFont, normalFont, headerBg, borderColor);
            AddFilterSummary(document, filter, accessories, altRowBg, borderColor, normalFont);

            var vehicleDict = vehicles;
            var clientDict = clients;

            if (filter.GroupByWeek)
            {
                var weekGroups = orders
                    .GroupBy(w => GetIsoWeek(w.OrderDate))
                    .OrderBy(g => g.Key);

                foreach (var group in weekGroups)
                {
                    int week = group.Key;
                    DateTime weekStart = GetStartOfWeek(group.First().OrderDate);
                    DateTime weekEnd = weekStart.AddDays(6);

                    Paragraph weekHeader = new Paragraph()
                        .Add($"Week {week} ({weekStart:dd/MM/yyyy} – {weekEnd:dd/MM/yyyy})")
                        .SetFont(headerFont)
                        .SetFontSize(14)
                        .SetFontColor(headerBg)
                        .SetMarginTop(15)
                        .SetMarginBottom(5);
                    document.Add(weekHeader);

                    if (!filter.SummaryOnly)
                    {
                        WriteWorkOrderTable(document, group.ToList(), vehicleDict, clientDict, taskLookup, accessories,
                            headerFont, normalFont, headerBg, headerFg, borderColor, altRowBg);
                    }

                    decimal weekTotal = group.Sum(o => o.TotalAmount ?? 0);
                    Paragraph weekTotalPara = new Paragraph()
                        .Add($"Week Total: {weekTotal:C}")
                        .SetFont(headerFont)
                        .SetFontSize(11)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetMarginTop(5)
                        .SetMarginBottom(15)
                        .SetBackgroundColor(totalBg)
                        .SetPadding(5);
                    document.Add(weekTotalPara);
                }
            }
            else
            {
                if (!filter.SummaryOnly)
                {
                    WriteWorkOrderTable(document, orders, vehicleDict, clientDict, taskLookup, accessories,
                        headerFont, normalFont, headerBg, headerFg, borderColor, altRowBg);
                }
            }

            if (!filter.SummaryOnly && orders.Any())
            {
                decimal grandTotal = orders.Sum(o => o.TotalAmount ?? 0);
                Paragraph grandTotalPara = new Paragraph()
                    .Add($"GRAND TOTAL: {grandTotal:C}")
                    .SetFont(headerFont)
                    .SetFontSize(14)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetMarginTop(20)
                    .SetBackgroundColor(totalBg)
                    .SetPadding(8);
                document.Add(grandTotalPara);
            }

            document.Close();
            return ms.ToArray();
        }

        private void AddFilterSummary(Document document, ReportFilter filter, Dictionary<int, string> accessories,
            Color bg, Color border, PdfFont font)
        {
            Paragraph summary = new Paragraph()
                .SetBackgroundColor(bg)
                .SetPadding(8)
                .SetBorder(new SolidBorder(border, 1))
                .SetMarginBottom(15)
                .SetFont(font);

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

        private void WriteWorkOrderTable(Document document, List<WorkOrder> orders,
            Dictionary<int, Vehicle> vehicleDict, Dictionary<int, Client> clientDict,
            ILookup<int, WorkTask> taskLookup, Dictionary<int, string> accessories,
            PdfFont headerFont, PdfFont normalFont, Color headerBg, Color headerFg, Color borderColor, Color altRowBg)
        {
            Table table = new Table(8).UseAllAvailableWidth();
            table.SetMarginTop(10);
            table.SetMarginBottom(10);

            string[] headers = { "ID", "Date", "Client", "Vehicle", "Status", "Tasks", "Task Names", "Total" };
            foreach (string h in headers)
            {
                Cell headerCell = new Cell()
                    .Add(new Paragraph(h).SetFont(headerFont).SetFontSize(10).SetFontColor(headerFg))
                    .SetBackgroundColor(headerBg)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(borderColor, 1))
                    .SetPadding(6);
                table.AddCell(headerCell);
            }

            bool alternate = false;
            foreach (var o in orders)
            {
                vehicleDict.TryGetValue(o.VehicleId, out var vehicle);
                string clientName = vehicle != null && clientDict.TryGetValue(vehicle.ClientId, out var client) ? client.Name : "";
                var tasks = taskLookup[o.Id].ToList();
                int taskCount = tasks.Count;
                string taskNames = string.Join(", ", tasks.Select(t => accessories.GetValueOrDefault(t.AccessoryId, "?")));

                Cell idCell = CreateCell(o.Id.ToString(), normalFont, borderColor);
                Cell dateCell = CreateCell(o.OrderDate.ToString("dd/MM/yyyy"), normalFont, borderColor);
                Cell clientCell = CreateCell(clientName, normalFont, borderColor);
                Cell vehicleCell = CreateCell(vehicle?.Model ?? "", normalFont, borderColor);
                Cell statusCell = CreateCell(o.Status.ToString(), normalFont, borderColor);
                Cell tasksCountCell = CreateCell(taskCount.ToString(), normalFont, borderColor);
                Cell tasksNameCell = CreateCell(taskNames, normalFont, borderColor);
                Cell totalCell = CreateCell($"{o.TotalAmount ?? 0:C}", normalFont, borderColor, TextAlignment.RIGHT);

                if (alternate)
                {
                    idCell.SetBackgroundColor(altRowBg);
                    dateCell.SetBackgroundColor(altRowBg);
                    clientCell.SetBackgroundColor(altRowBg);
                    vehicleCell.SetBackgroundColor(altRowBg);
                    statusCell.SetBackgroundColor(altRowBg);
                    tasksCountCell.SetBackgroundColor(altRowBg);
                    tasksNameCell.SetBackgroundColor(altRowBg);
                    totalCell.SetBackgroundColor(altRowBg);
                }

                table.AddCell(idCell);
                table.AddCell(dateCell);
                table.AddCell(clientCell);
                table.AddCell(vehicleCell);
                table.AddCell(statusCell);
                table.AddCell(tasksCountCell);
                table.AddCell(tasksNameCell);
                table.AddCell(totalCell);

                alternate = !alternate;
            }

            document.Add(table);
        }

        private Cell CreateCell(string text, PdfFont font, Color borderColor, TextAlignment alignment = TextAlignment.LEFT)
        {
            return new Cell()
                .Add(new Paragraph(text).SetFont(font).SetFontSize(9))
                .SetTextAlignment(alignment)
                .SetBorder(new SolidBorder(borderColor, 1))
                .SetPadding(4);
        }

        public async Task<byte[]> GenerateClientsReportAsync()
        {
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
                    .SetBackgroundColor(headerBg)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(borderColor, 1))
                    .SetPadding(6);
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
            return ms.ToArray();
        }

        public async Task<byte[]> GenerateVehiclesReportAsync()
        {
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
                    .SetBackgroundColor(headerBg)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(borderColor, 1))
                    .SetPadding(6);
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
            return ms.ToArray();
        }

        private void AddHeader(Document document, string title, PdfFont boldFont, PdfFont normalFont, Color headerBg, Color borderColor)
        {
            if (File.Exists(_logoPath))
            {
                try
                {
                    ImageData imageData = ImageDataFactory.Create(_logoPath);
                    Image logo = new Image(imageData).ScaleToFit(100, 50);
                    document.Add(logo);
                }
                catch { }
            }

            Paragraph company = new Paragraph("SK Auto")
                .SetFont(boldFont)
                .SetFontSize(20)
                .SetFontColor(headerBg)
                .SetMarginTop(10);
            document.Add(company);

            Paragraph titlePara = new Paragraph(title)
                .SetFont(boldFont)
                .SetFontSize(16)
                .SetFontColor(headerBg)
                .SetMarginBottom(10);
            document.Add(titlePara);

            SolidLine line = new SolidLine(1f);
            line.SetColor(borderColor);
            document.Add(new LineSeparator(line));
            document.Add(new Paragraph(" ").SetFont(normalFont));
        }

        private int GetIsoWeek(DateTime date)
        {
            return CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }
    }
}