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
        private PdfFont _boldFont;
        private PdfFont _normalFont;
        private PdfFont _smallFont;

        // Professional color palette (from UI)
        private static readonly Color HeaderBackground = new DeviceRgb(44, 62, 80);      // #2c3e50
        private static readonly Color HeaderForeground = ColorConstants.WHITE;
        private static readonly Color AlternateRowBackground = new DeviceRgb(245, 245, 245); // #f5f5f5
        private static readonly Color BorderColor = new DeviceRgb(221, 221, 221);        // #ddd
        private static readonly Color TotalBackground = new DeviceRgb(230, 255, 230);  // new DeviceRgb(39, 174, 96, 50); #27ae60 with 50% alpha (semi-transparent)
        private static readonly Color AccentGreen = new DeviceRgb(39, 174, 96);          // #27ae60

        public PdfReportGenerator(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
        }

        private void InitializeFonts()
        {
            _boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            _normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            _smallFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        }

        public async Task<byte[]> GenerateWorkOrdersReportAsync(ReportFilter filter)
        {
            var orders = (await _unitOfWork.WorkOrders
                .FindAsync(w => w.OrderDate >= filter.From && w.OrderDate <= filter.To)).ToList();

            var vehicleIds = orders.Select(o => o.VehicleId).Distinct().ToList();
            var vehicles = (await _unitOfWork.Vehicles.FindAsync(v => vehicleIds.Contains(v.Id))).ToDictionary(v => v.Id);
            var clientIds = vehicles.Values.Select(v => v.ClientId).Distinct().ToList();
            var clients = (await _unitOfWork.Clients.FindAsync(c => clientIds.Contains(c.Id))).ToDictionary(c => c.Id);

            var orderIds = orders.Select(o => o.Id).ToList();
            var allTasks = (await _unitOfWork.WorkTasks.FindAsync(t => orderIds.Contains(t.WorkOrderId))).ToList();
            var taskLookup = allTasks.ToLookup(t => t.WorkOrderId);

            // Load accessories
            var accessoryIds = allTasks.Select(t => t.AccessoryId).Distinct().ToList();
            var accessories = (await _unitOfWork.Accessories.FindAsync(a => accessoryIds.Contains(a.Id)))
                .ToDictionary(a => a.Id, a => a.Name);

            // Apply filters
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
            document.SetMargins(36, 36, 36, 36); // 0.5 inch margins

            InitializeFonts();
            AddHeader(document, "Work Orders Report");

            // Filter summary in a styled box
            AddFilterSummary(document, filter, accessories);

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

                    // Week header with underline
                    Paragraph weekHeader = new Paragraph()
                        .Add($"Week {week} ({weekStart:dd/MM/yyyy} – {weekEnd:dd/MM/yyyy})")
                        .SetFont(_boldFont)
                        .SetFontSize(14)
                        .SetFontColor(HeaderBackground)
                        .SetMarginTop(15)
                        .SetMarginBottom(5);
                    document.Add(weekHeader);

                    if (!filter.SummaryOnly)
                    {
                        WriteWorkOrderTable(document, group.ToList(), vehicleDict, clientDict, taskLookup, accessories);
                    }

                    decimal weekTotal = group.Sum(o => o.TotalAmount ?? 0);
                    Paragraph weekTotalPara = new Paragraph()
                        .Add($"Week Total: {weekTotal:C}")
                        .SetFont(_boldFont)
                        .SetFontSize(11)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetMarginTop(5)
                        .SetMarginBottom(15)
                        .SetBackgroundColor(TotalBackground)
                        .SetPadding(5);
                    document.Add(weekTotalPara);
                }
            }
            else
            {
                if (!filter.SummaryOnly)
                {
                    WriteWorkOrderTable(document, orders, vehicleDict, clientDict, taskLookup, accessories);
                }
            }

            // Grand total
            if (!filter.SummaryOnly && orders.Any())
            {
                decimal grandTotal = orders.Sum(o => o.TotalAmount ?? 0);
                Paragraph grandTotalPara = new Paragraph()
                    .Add($"GRAND TOTAL: {grandTotal:C}")
                    .SetFont(_boldFont)
                    .SetFontSize(14)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetMarginTop(20)
                    .SetBackgroundColor(TotalBackground)
                    .SetPadding(8);
                document.Add(grandTotalPara);
            }

            document.Close();
            return ms.ToArray();
        }

        private void AddFilterSummary(Document document, ReportFilter filter, Dictionary<int, string> accessories)
        {
            // Create a styled div-like container with light gray background
            Paragraph summary = new Paragraph()
                .SetBackgroundColor(AlternateRowBackground)
                .SetPadding(8)
                .SetBorder(new SolidBorder(BorderColor, 1))
                .SetMarginBottom(15);

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
            ILookup<int, WorkTask> taskLookup, Dictionary<int, string> accessories)
        {
            Table table = new Table(8).UseAllAvailableWidth();
            table.SetMarginTop(10);
            table.SetMarginBottom(10);

            // Header row with professional styling
            string[] headers = { "ID", "Date", "Client", "Vehicle", "Status", "Tasks", "Task Names", "Total" };
            foreach (string h in headers)
            {
                Cell headerCell = new Cell()
                    .Add(new Paragraph(h).SetFont(_boldFont).SetFontSize(10).SetFontColor(HeaderForeground))
                    .SetBackgroundColor(HeaderBackground)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(BorderColor, 1))
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

                // Create cells with borders and consistent padding
                Cell idCell = CreateCell(o.Id.ToString());
                Cell dateCell = CreateCell(o.OrderDate.ToString("dd/MM/yyyy"));
                Cell clientCell = CreateCell(clientName);
                Cell vehicleCell = CreateCell(vehicle?.Model ?? "");
                Cell statusCell = CreateCell(o.Status.ToString());
                Cell tasksCountCell = CreateCell(taskCount.ToString());
                Cell tasksNameCell = CreateCell(taskNames);
                Cell totalCell = CreateCell($"{o.TotalAmount ?? 0:C}", TextAlignment.RIGHT);

                // Alternate row background
                if (alternate)
                {
                    ApplyAlternateBackground(idCell, dateCell, clientCell, vehicleCell, statusCell, tasksCountCell, tasksNameCell, totalCell);
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

        private Cell CreateCell(string text, TextAlignment alignment = TextAlignment.LEFT)
        {
            return new Cell()
                .Add(new Paragraph(text).SetFont(_normalFont).SetFontSize(9))
                .SetTextAlignment(alignment)
                .SetBorder(new SolidBorder(BorderColor, 1))
                .SetPadding(4);
        }

        private void ApplyAlternateBackground(params Cell[] cells)
        {
            foreach (var cell in cells)
                cell.SetBackgroundColor(AlternateRowBackground);
        }

        public async Task<byte[]> GenerateClientsReportAsync()
        {
            var clients = await _unitOfWork.Clients.GetAllAsync();
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4);
            document.SetMargins(36, 36, 36, 36);

            InitializeFonts();
            AddHeader(document, "Clients Report");

            Table table = new Table(6).UseAllAvailableWidth();
            table.SetMarginTop(10);

            string[] headers = { "ID", "Name", "Type", "Phone", "Email", "Active" };
            foreach (string h in headers)
            {
                Cell headerCell = new Cell()
                    .Add(new Paragraph(h).SetFont(_boldFont).SetFontSize(10).SetFontColor(HeaderForeground))
                    .SetBackgroundColor(HeaderBackground)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(BorderColor, 1))
                    .SetPadding(6);
                table.AddCell(headerCell);
            }

            bool alternate = false;
            foreach (var c in clients.OrderBy(c => c.Name))
            {
                Cell idCell = CreateCell(c.Id.ToString());
                Cell nameCell = CreateCell(c.Name);
                Cell typeCell = CreateCell(c.Type.ToString());
                Cell phoneCell = CreateCell(c.Phone ?? "");
                Cell emailCell = CreateCell(c.Email ?? "");
                Cell activeCell = CreateCell(c.IsActive ? "Yes" : "No", TextAlignment.CENTER);

                if (alternate)
                    ApplyAlternateBackground(idCell, nameCell, typeCell, phoneCell, emailCell, activeCell);

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
            var vehicles = await _unitOfWork.Vehicles.GetAllAsync();
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4);
            document.SetMargins(36, 36, 36, 36);

            InitializeFonts();
            AddHeader(document, "Vehicles Report");

            Table table = new Table(6).UseAllAvailableWidth();
            table.SetMarginTop(10);

            string[] headers = { "ID", "Chassis", "Make", "Model", "Year", "Client" };
            foreach (string h in headers)
            {
                Cell headerCell = new Cell()
                    .Add(new Paragraph(h).SetFont(_boldFont).SetFontSize(10).SetFontColor(HeaderForeground))
                    .SetBackgroundColor(HeaderBackground)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBorder(new SolidBorder(BorderColor, 1))
                    .SetPadding(6);
                table.AddCell(headerCell);
            }

            bool alternate = false;
            foreach (var v in vehicles.OrderBy(v => v.ChassisNumber))
            {
                Cell idCell = CreateCell(v.Id.ToString());
                Cell chassisCell = CreateCell(v.ChassisNumber);
                Cell makeCell = CreateCell(v.Make ?? "");
                Cell modelCell = CreateCell(v.Model ?? "");
                Cell yearCell = CreateCell(v.Year?.ToString() ?? "");
                Cell clientCell = CreateCell(v.Client?.Name ?? "");

                if (alternate)
                    ApplyAlternateBackground(idCell, chassisCell, makeCell, modelCell, yearCell, clientCell);

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

        private void AddHeader(Document document, string title)
        {
            if (File.Exists(_logoPath))
            {
                try
                {
                    ImageData imageData = ImageDataFactory.Create(_logoPath);
                    Image logo = new Image(imageData).ScaleToFit(100, 50);
                    document.Add(logo);
                }
                catch
                {
                    // Ignore if logo fails to load
                }
            }

            Paragraph company = new Paragraph("SK Auto")
                .SetFont(_boldFont)
                .SetFontSize(20)
                .SetFontColor(HeaderBackground)
                .SetMarginTop(10);
            document.Add(company);

            Paragraph titlePara = new Paragraph(title)
                .SetFont(_boldFont)
                .SetFontSize(16)
                .SetFontColor(HeaderBackground)
                .SetMarginBottom(10);
            document.Add(titlePara);

            SolidLine line = new SolidLine(1f);
            line.SetColor(BorderColor);
            document.Add(new LineSeparator(line));
            document.Add(new Paragraph(" ").SetFont(_normalFont));
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