using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using SKAuto.Core.DTOs;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Import.Parsers
{
    public class PdfParser : IImportParser
    {
        public async Task<ImportResult> ParseAsync(string filePath)
        {
            var result = new ImportResult();

            try
            {
                var text = ExtractTextFromPdf(filePath);
                var workOrders = ParseText(text);

                result.WorkOrders = workOrders;
                result.RecordsProcessed = workOrders.Count;
                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error parsing PDF: {ex.Message}");
            }

            return result;
        }

        private string ExtractTextFromPdf(string filePath)
        {
            using var pdfReader = new PdfReader(filePath);
            using var pdfDocument = new PdfDocument(pdfReader);

            var text = new System.Text.StringBuilder();
            for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
            {
                var strategy = new SimpleTextExtractionStrategy();
                var pageText = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page), strategy);
                text.AppendLine(pageText);
            }

            return text.ToString();
        }

        private List<Core.Entities.WorkOrder> ParseText(string text)
        {
            var workOrders = new List<Core.Entities.WorkOrder>();

            // Simple parsing logic - can be enhanced based on actual PDF format
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Core.Entities.WorkOrder? currentOrder = null;

            foreach (var line in lines)
            {
                if (line.Contains("Chassis:") || line.Contains("VIN:"))
                {
                    // Start new work order
                    if (currentOrder != null)
                        workOrders.Add(currentOrder);

                    currentOrder = new Core.Entities.WorkOrder();
                    // Extract chassis number
                }
                else if (currentOrder != null)
                {
                    // Parse other details
                    if (line.Contains("Accessory:"))
                    {
                        // Parse accessory
                    }
                }
            }

            if (currentOrder != null)
                workOrders.Add(currentOrder);

            return workOrders;
        }
    }
}