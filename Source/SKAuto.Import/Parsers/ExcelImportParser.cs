using ClosedXML.Excel;
using SKAuto.Core.DTOs; // Add this using
using System.Globalization;

namespace SKAuto.Import.Parsers
{
    public class ExcelImportParser
    {
        private static readonly HashSet<string> AllowedLivreur = new()
        {
            "ACCESSOIRE CITROEN",
            "ACCESSOIRE PEUGEOT"
        };

        public List<ImportWorkOrderDto> Parse(string filePath)
        {
            var results = new List<ImportWorkOrderDto>();

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);

            int headerRow = FindHeaderRow(worksheet);
            if (headerRow == 0)
                return results;

            var colMap = GetColumnMap(worksheet, headerRow);
            if (!colMap.ContainsKey("Vin") || !colMap.ContainsKey("Client") || !colMap.ContainsKey("Modele") || !colMap.ContainsKey("FinPrep") || !colMap.ContainsKey("Livreur"))
                return results;

            int row = headerRow + 1;
            while (!worksheet.Cell(row, 1).IsEmpty())
            {
                var vin = worksheet.Cell(row, colMap["Vin"]).GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(vin))
                {
                    row++;
                    continue;
                }

                var livreur = worksheet.Cell(row, colMap["Livreur"]).GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(livreur) || !AllowedLivreur.Contains(livreur))
                {
                    row++;
                    continue;
                }

                var client = worksheet.Cell(row, colMap["Client"]).GetString()?.Trim();
                var model = worksheet.Cell(row, colMap["Modele"]).GetString()?.Trim();
                var finPrepStr = worksheet.Cell(row, colMap["FinPrep"]).GetString()?.Trim();

                if (string.IsNullOrWhiteSpace(finPrepStr))
                {
                    row++;
                    continue;
                }

                if (!DateTime.TryParseExact(finPrepStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var orderDate))
                {
                    if (!DateTime.TryParse(finPrepStr, out orderDate))
                    {
                        row++;
                        continue;
                    }
                }

                results.Add(new ImportWorkOrderDto
                {
                    Chassis = vin,
                    Model = model ?? "",
                    ClientName = client ?? "",
                    OrderDate = orderDate,
                    Source = "Excel"
                });

                row++;
            }

            return results;
        }

        private int FindHeaderRow(IXLWorksheet worksheet)
        {
            for (int r = 1; r <= 10; r++)
            {
                var cell = worksheet.Cell(r, 1);
                if (cell.IsEmpty()) continue;
                var text = cell.GetString();
                if (text.Contains("N° (VIN)") || text.Contains("VIN"))
                    return r;
            }
            return 0;
        }

        private Dictionary<string, int> GetColumnMap(IXLWorksheet worksheet, int headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headers = new[] { "N° (VIN)", "Client", "Modèle", "Fin prép.", "Livreur" };
            var keys = new[] { "Vin", "Client", "Modele", "FinPrep", "Livreur" };

            for (int col = 1; col <= 20; col++)
            {
                var cell = worksheet.Cell(headerRow, col);
                if (cell.IsEmpty()) continue;
                var headerText = cell.GetString().Trim();
                for (int i = 0; i < headers.Length; i++)
                {
                    if (headerText.Contains(headers[i], StringComparison.OrdinalIgnoreCase))
                    {
                        map[keys[i]] = col;
                        break;
                    }
                }
            }
            return map;
        }
    }
}