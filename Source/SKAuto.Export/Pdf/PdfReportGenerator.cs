using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Draw;
using iText.Layout;
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

namespace SKAuto.Export.Pdf
{
    public class PdfReportGenerator
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly string _logoPath;
        private PdfFont _boldFont;
        private PdfFont _normalFont;

        public PdfReportGenerator(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
        }

        private void InitializeFonts()
        {
            _boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            _normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
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

            // Apply task type filter
            if (filter.TaskType.HasValue)
            {
                var ordersWithTask = allTasks
                    .Where(t => t.TaskType == filter.TaskType.Value)
                    .Select(t => t.WorkOrderId)
                    .Distinct()
                    .ToHashSet();
                orders = orders.Where(o => ordersWithTask.Contains(o.Id)).ToList();
            }

            // Apply work status filter
            if (filter.WorkStatus.HasValue)
            {
                orders = orders.Where(o => o.Status == filter.WorkStatus.Value).ToList();
            }

            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4.Rotate());

            InitializeFonts();
            AddHeader(document, "Work Orders Report");

            document.Add(new Paragraph($"Period: {filter.From:dd/MM/yyyy} – {filter.To:dd/MM/yyyy}").SetFont(_normalFont));
            document.Add(new Paragraph($"Task Type: {(filter.TaskType.HasValue ? filter.TaskType.Value.ToString() : "All")}").SetFont(_normalFont));
            document.Add(new Paragraph($"Status: {(filter.WorkStatus.HasValue ? filter.WorkStatus.Value.ToString() : "All")}").SetFont(_normalFont));
            document.Add(new Paragraph(" "));

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
                        .Add($"Week {week} ({weekStart:dd/MM} – {weekEnd:dd/MM})")
                        .SetFont(_boldFont)
                        .SetFontSize(14)
                        .SetMarginTop(10);
                    document.Add(weekHeader);

                    if (!filter.SummaryOnly)
                    {
                        WriteWorkOrderDetails(document, group.ToList(), vehicleDict, clientDict, taskLookup);
                    }

                    decimal weekTotal = group.Sum(o => o.TotalAmount ?? 0);
                    Paragraph weekTotalPara = new Paragraph()
                        .Add($"Week Total: €{weekTotal:0.00}")
                        .SetFont(_boldFont)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetMarginBottom(10);
                    document.Add(weekTotalPara);
                }
            }
            else
            {
                if (!filter.SummaryOnly)
                {
                    WriteWorkOrderDetails(document, orders, vehicleDict, clientDict, taskLookup);
                }
            }

            decimal grandTotal = orders.Sum(o => o.TotalAmount ?? 0);
            Paragraph grandTotalPara = new Paragraph()
                .Add($"GRAND TOTAL: €{grandTotal:0.00}")
                .SetFont(_boldFont)
                .SetFontSize(14)
                .SetTextAlignment(TextAlignment.RIGHT)
                .SetMarginTop(20);
            document.Add(grandTotalPara);

            document.Close();
            return ms.ToArray();
        }

        private void WriteWorkOrderDetails(Document document, List<WorkOrder> orders,
            Dictionary<int, Vehicle> vehicleDict, Dictionary<int, Client> clientDict,
            ILookup<int, WorkTask> taskLookup)
        {
            Table table = new Table(8).UseAllAvailableWidth(); // 8 columns: ID, Date, Client, Vehicle, Status, Task Count, Task Names, Total
            table.SetMarginTop(5);
            table.SetMarginBottom(5);

            string[] headers = { "ID", "Date", "Client", "Vehicle", "Status", "Tasks", "Task Names", "Total" };
            foreach (string h in headers)
            {
                Cell headerCell = new Cell().Add(new Paragraph(h).SetFont(_boldFont));
                headerCell.SetBackgroundColor(ColorConstants.LIGHT_GRAY);
                table.AddCell(headerCell);
            }

            bool alternate = false;
            foreach (var o in orders)
            {
                vehicleDict.TryGetValue(o.VehicleId, out var vehicle);
                string clientName = vehicle != null && clientDict.TryGetValue(vehicle.ClientId, out var client) ? client.Name : "";
                var tasks = taskLookup[o.Id].ToList();
                int taskCount = tasks.Count;
                string taskNames = string.Join(", ", tasks.Select(t => t.Accessory?.Name ?? "?"));

                Cell idCell = new Cell().Add(new Paragraph(o.Id.ToString()).SetFont(_normalFont));
                Cell dateCell = new Cell().Add(new Paragraph(o.OrderDate.ToString("dd/MM/yyyy")).SetFont(_normalFont));
                Cell clientCell = new Cell().Add(new Paragraph(clientName).SetFont(_normalFont));
                Cell vehicleCell = new Cell().Add(new Paragraph(vehicle?.Model ?? "").SetFont(_normalFont));
                Cell statusCell = new Cell().Add(new Paragraph(o.Status.ToString()).SetFont(_normalFont));
                Cell tasksCountCell = new Cell().Add(new Paragraph(taskCount.ToString()).SetFont(_normalFont));
                Cell tasksNameCell = new Cell().Add(new Paragraph(taskNames).SetFont(_normalFont));
                Cell totalCell = new Cell().Add(new Paragraph($"€{o.TotalAmount ?? 0:0.00}").SetFont(_normalFont));

                if (alternate)
                {
                    Color bg = new DeviceRgb(0xF2, 0xF2, 0xF2);
                    idCell.SetBackgroundColor(bg);
                    dateCell.SetBackgroundColor(bg);
                    clientCell.SetBackgroundColor(bg);
                    vehicleCell.SetBackgroundColor(bg);
                    statusCell.SetBackgroundColor(bg);
                    tasksCountCell.SetBackgroundColor(bg);
                    tasksNameCell.SetBackgroundColor(bg);
                    totalCell.SetBackgroundColor(bg);
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

        public async Task<byte[]> GenerateClientsReportAsync()
        {
            // (unchanged)
            var clients = await _unitOfWork.Clients.GetAllAsync();
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4);

            InitializeFonts();
            AddHeader(document, "Clients Report");

            Table table = new Table(6).UseAllAvailableWidth();
            string[] headers = { "ID", "Name", "Type", "Phone", "Email", "Active" };
            foreach (string h in headers)
            {
                Cell headerCell = new Cell().Add(new Paragraph(h).SetFont(_boldFont));
                headerCell.SetBackgroundColor(ColorConstants.LIGHT_GRAY);
                table.AddCell(headerCell);
            }

            bool alternate = false;
            foreach (var c in clients)
            {
                Cell idCell = new Cell().Add(new Paragraph(c.Id.ToString()).SetFont(_normalFont));
                Cell nameCell = new Cell().Add(new Paragraph(c.Name).SetFont(_normalFont));
                Cell typeCell = new Cell().Add(new Paragraph(c.Type.ToString()).SetFont(_normalFont));
                Cell phoneCell = new Cell().Add(new Paragraph(c.Phone ?? "").SetFont(_normalFont));
                Cell emailCell = new Cell().Add(new Paragraph(c.Email ?? "").SetFont(_normalFont));
                Cell activeCell = new Cell().Add(new Paragraph(c.IsActive ? "Yes" : "No").SetFont(_normalFont));

                if (alternate)
                {
                    Color bg = new DeviceRgb(0xF2, 0xF2, 0xF2);
                    idCell.SetBackgroundColor(bg);
                    nameCell.SetBackgroundColor(bg);
                    typeCell.SetBackgroundColor(bg);
                    phoneCell.SetBackgroundColor(bg);
                    emailCell.SetBackgroundColor(bg);
                    activeCell.SetBackgroundColor(bg);
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
            // (unchanged)
            var vehicles = await _unitOfWork.Vehicles.GetAllAsync();
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4);

            InitializeFonts();
            AddHeader(document, "Vehicles Report");

            Table table = new Table(6).UseAllAvailableWidth();
            string[] headers = { "ID", "Chassis", "Make", "Model", "Year", "Client" };
            foreach (string h in headers)
            {
                Cell headerCell = new Cell().Add(new Paragraph(h).SetFont(_boldFont));
                headerCell.SetBackgroundColor(ColorConstants.LIGHT_GRAY);
                table.AddCell(headerCell);
            }

            bool alternate = false;
            foreach (var v in vehicles)
            {
                Cell idCell = new Cell().Add(new Paragraph(v.Id.ToString()).SetFont(_normalFont));
                Cell chassisCell = new Cell().Add(new Paragraph(v.ChassisNumber).SetFont(_normalFont));
                Cell makeCell = new Cell().Add(new Paragraph(v.Make ?? "").SetFont(_normalFont));
                Cell modelCell = new Cell().Add(new Paragraph(v.Model ?? "").SetFont(_normalFont));
                Cell yearCell = new Cell().Add(new Paragraph(v.Year?.ToString() ?? "").SetFont(_normalFont));
                Cell clientCell = new Cell().Add(new Paragraph(v.Client?.Name ?? "").SetFont(_normalFont));

                if (alternate)
                {
                    Color bg = new DeviceRgb(0xF2, 0xF2, 0xF2);
                    idCell.SetBackgroundColor(bg);
                    chassisCell.SetBackgroundColor(bg);
                    makeCell.SetBackgroundColor(bg);
                    modelCell.SetBackgroundColor(bg);
                    yearCell.SetBackgroundColor(bg);
                    clientCell.SetBackgroundColor(bg);
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

        private void AddHeader(Document document, string title)
        {
            if (File.Exists(_logoPath))
            {
                ImageData imageData = ImageDataFactory.Create(_logoPath);
                Image logo = new Image(imageData).ScaleToFit(100, 50);
                document.Add(logo);
            }

            Paragraph p1 = new Paragraph("SK Auto")
                .SetFont(_boldFont)
                .SetFontSize(20)
                .SetMarginTop(10);
            document.Add(p1);

            Paragraph p2 = new Paragraph(title)
                .SetFont(_boldFont)
                .SetFontSize(16)
                .SetMarginBottom(20);
            document.Add(p2);

            document.Add(new LineSeparator(new SolidLine()));
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