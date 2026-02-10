using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// PdfReportGenerator.cs
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;

namespace SKAuto.Export.Pdf
{
    public class PdfReportGenerator
    {
        public async Task<byte[]> GenerateInvoiceAsync(WorkOrder workOrder)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new PdfWriter(memoryStream);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4);

            // Header
            document.Add(new Paragraph("SK AUTO")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(20)
                .SetBold());

            document.Add(new Paragraph("Automotive Services & Accessories")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(12));

            document.Add(new Paragraph("PSA Certified Partner")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(10)
                .SetFontColor(ColorConstants.GRAY));

            document.Add(new Paragraph(" ")
                .SetMarginBottom(20));

            // Invoice header
            var table = new Table(2).UseAllAvailableWidth();
            table.AddCell(new Cell().Add(new Paragraph("INVOICE").SetBold()).SetBorder(Border.NO_BORDER));
            table.AddCell(new Cell().Add(new Paragraph($"#{workOrder.Id}").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
            document.Add(table);

            // Client info
            document.Add(new Paragraph("Bill To:").SetBold().SetMarginTop(20));
            document.Add(new Paragraph(workOrder.Client?.Name ?? ""));
            if (!string.IsNullOrEmpty(workOrder.Client?.Address))
                document.Add(new Paragraph(workOrder.Client.Address));
            if (!string.IsNullOrEmpty(workOrder.Client?.ContactNumber))
                document.Add(new Paragraph($"Tel: {workOrder.Client.ContactNumber}"));

            // Order details
            document.Add(new Paragraph(" ").SetMarginTop(20));

            var detailsTable = new Table(4).UseAllAvailableWidth().SetMarginTop(10);
            detailsTable.AddHeaderCell("Order Date").SetBold();
            detailsTable.AddHeaderCell("Vehicle").SetBold();
            detailsTable.AddHeaderCell("Chassis").SetBold();
            detailsTable.AddHeaderCell("Status").SetBold();

            detailsTable.AddCell(workOrder.OrderDate.ToString("dd/MM/yyyy"));
            detailsTable.AddCell(workOrder.Vehicle?.Model ?? "");
            detailsTable.AddCell(workOrder.Vehicle?.ChassisNumber ?? "");
            detailsTable.AddCell(workOrder.Status.ToString());

            document.Add(detailsTable);

            // Items table
            document.Add(new Paragraph(" ").SetMarginTop(20));
            document.Add(new Paragraph("Items").SetBold());

            var itemsTable = new Table(5).UseAllAvailableWidth().SetMarginTop(10);
            itemsTable.AddHeaderCell("Description").SetBold();
            itemsTable.AddHeaderCell("Qty").SetBold();
            itemsTable.AddHeaderCell("Unit Price").SetBold();
            itemsTable.AddHeaderCell("Fitting").SetBold();
            itemsTable.AddHeaderCell("Amount").SetBold();

            foreach (var task in workOrder.WorkTasks)
            {
                itemsTable.AddCell(task.Accessory?.Name ?? "");
                itemsTable.AddCell(task.Quantity.ToString());
                itemsTable.AddCell($"€{task.UnitPrice ?? 0:0.00}");
                itemsTable.AddCell($"€{task.FittingPrice ?? 0:0.00}");
                itemsTable.AddCell($"€{task.CalculateTotal():0.00}");
            }

            document.Add(itemsTable);

            // Travel if any
            if (workOrder.Travels.Any())
            {
                document.Add(new Paragraph(" ").SetMarginTop(20));
                document.Add(new Paragraph("Travel").SetBold());

                var travelTable = new Table(3).UseAllAvailableWidth().SetMarginTop(10);
                travelTable.AddHeaderCell("Date").SetBold();
                travelTable.AddHeaderCell("Destination").SetBold();
                travelTable.AddHeaderCell("Cost").SetBold();

                foreach (var travel in workOrder.Travels)
                {
                    travelTable.AddCell(travel.TravelDate.ToString("dd/MM/yyyy"));
                    travelTable.AddCell(travel.Destination);
                    travelTable.AddCell($"€{travel.TravelCost ?? 0:0.00}");
                }

                document.Add(travelTable);
            }

            // Totals
            document.Add(new Paragraph(" ").SetMarginTop(30));

            var totalTable = new Table(2).UseAllAvailableWidth().SetHorizontalAlignment(HorizontalAlignment.RIGHT);
            totalTable.SetWidth(200);

            totalTable.AddCell(new Cell().Add(new Paragraph("Subtotal:").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
            totalTable.AddCell(new Cell().Add(new Paragraph($"€{workOrder.TotalAmount ?? 0:0.00}").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));

            totalTable.AddCell(new Cell().Add(new Paragraph("VAT (20%):").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
            totalTable.AddCell(new Cell().Add(new Paragraph($"€{(workOrder.TotalAmount ?? 0) * 0.2m:0.00}").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));

            totalTable.AddCell(new Cell().Add(new Paragraph("Total:").SetBold().SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
            totalTable.AddCell(new Cell().Add(new Paragraph($"€{(workOrder.TotalAmount ?? 0) * 1.2m:0.00}").SetBold().SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));

            document.Add(totalTable);

            // Footer
            document.Add(new Paragraph(" ")
                .SetMarginTop(50));

            document.Add(new Paragraph("Thank you for your business!")
                .SetTextAlignment(TextAlignment.CENTER));

            document.Add(new Paragraph("SK Auto - TVA: FRXXXXXXXXX")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(10)
                .SetFontColor(ColorConstants.GRAY));

            document.Close();
            return memoryStream.ToArray();
        }

        public async Task<byte[]> GenerateDailyReportAsync(DailyWorkSummaryDto summary)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new PdfWriter(memoryStream);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4);

            // Header
            document.Add(new Paragraph("DAILY WORK REPORT")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(18)
                .SetBold());

            document.Add(new Paragraph($"Date: {summary.Date:dd/MM/yyyy}")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(12));

            // Summary
            document.Add(new Paragraph(" ").SetMarginTop(20));
            document.Add(new Paragraph("Summary").SetBold().SetFontSize(14));

            var summaryTable = new Table(2).UseAllAvailableWidth().SetMarginTop(10);

            summaryTable.AddCell("Total Work Orders");
            summaryTable.AddCell(summary.TotalWorkOrders.ToString());

            summaryTable.AddCell("Completed Orders");
            summaryTable.AddCell(summary.CompletedOrders.ToString());

            summaryTable.AddCell("In Progress");
            summaryTable.AddCell(summary.InProgressOrders.ToString());

            summaryTable.AddCell("Total Revenue");
            summaryTable.AddCell($"€{summary.TotalRevenue:0.00}");

            summaryTable.AddCell("PSA Revenue");
            summaryTable.AddCell($"€{summary.TotalPSARevenue:0.00}");

            summaryTable.AddCell("Direct Revenue");
            summaryTable.AddCell($"€{summary.TotalDirectRevenue:0.00}");

            document.Add(summaryTable);

            // Client breakdown
            if (summary.ClientSummaries.Any())
            {
                document.Add(new Paragraph(" ").SetMarginTop(30));
                document.Add(new Paragraph("Clients").SetBold().SetFontSize(14));

                var clientTable = new Table(3).UseAllAvailableWidth().SetMarginTop(10);
                clientTable.AddHeaderCell("Client").SetBold();
                clientTable.AddHeaderCell("Orders").SetBold();
                clientTable.AddHeaderCell("Amount").SetBold();

                foreach (var client in summary.ClientSummaries)
                {
                    clientTable.AddCell(client.ClientName);
                    clientTable.AddCell(client.OrderCount.ToString());
                    clientTable.AddCell($"€{client.TotalAmount:0.00}");
                }

                document.Add(clientTable);
            }

            // Vehicle work
            if (summary.VehicleWork.Any())
            {
                document.Add(new Paragraph(" ").SetMarginTop(30));
                document.Add(new Paragraph("Vehicles").SetBold().SetFontSize(14));

                var vehicleTable = new Table(4).UseAllAvailableWidth().SetMarginTop(10);
                vehicleTable.AddHeaderCell("Chassis").SetBold();
                vehicleTable.AddHeaderCell("Model").SetBold();
                vehicleTable.AddHeaderCell("Accessories").SetBold();
                vehicleTable.AddHeaderCell("Cost").SetBold();

                foreach (var vehicle in summary.VehicleWork)
                {
                    vehicleTable.AddCell(vehicle.ChassisNumber);
                    vehicleTable.AddCell(vehicle.Model);
                    vehicleTable.AddCell(string.Join(", ", vehicle.AccessoriesFitted));
                    vehicleTable.AddCell($"€{vehicle.TotalCost:0.00}");
                }

                document.Add(vehicleTable);
            }

            document.Close();
            return memoryStream.ToArray();
        }
    }
}