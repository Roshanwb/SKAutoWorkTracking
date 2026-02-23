using Microsoft.VisualBasic.FileIO;
using SKAuto.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace SKAuto.Import.Parsers
{
    public class CsvImportParser
    {
        // No Livreur filter – import everything

        public List<ImportWorkOrderDto> Parse(string filePath)
        {
            var results = new List<ImportWorkOrderDto>();

            using var parser = new TextFieldParser(filePath, Encoding.Default);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(";");
            parser.HasFieldsEnclosedInQuotes = true;
            parser.TrimWhiteSpace = true;

            // Read header line
            string[]? headers = parser.ReadFields();
            if (headers == null || headers.Length == 0)
                return results;

            // Build a dictionary of column names (trimmed, original case)
            var headerList = new List<string>();
            for (int i = 0; i < headers.Length; i++)
                headerList.Add((headers[i] ?? "").Trim());

            // Map columns by exact/partial matching with precedence
            int vinCol = FindColumn(headerList, new[] { "N° (VIN)", "VIN", "N°" });
            int clientCol = FindColumn(headerList, new[] { "Client" });
            int modeleCol = FindColumn(headerList, new[] { "Modèle"}); // first choice
            //int marqueCol = FindColumn(headerList, new[] { "Marque" });           // fallback for model
            int finPrepCol = FindColumn(headerList, new[] { "Fin prép.", "Fin prep.", "Fin", "Date fin" });
            int livreurCol = FindColumn(headerList, new[] { "Livreur", "Type de livraison" });

            // Use modeleCol if found, otherwise use marqueCol
            int modelCol = modeleCol != -1 ? modeleCol : 3;

            // Required columns: Vin, Client, model, FinPrep
            if (vinCol == -1 || clientCol == -1 || modelCol == -1 || finPrepCol == -1)
            {
                Debug.WriteLine($"Missing required columns: Vin={vinCol}, Client={clientCol}, Model={modelCol}, FinPrep={finPrepCol}");
                return results;
            }

            int totalRows = 0;
            int skippedNoVin = 0;
            int skippedNoDate = 0;
            int skippedDateParse = 0;
            int imported = 0;

            while (!parser.EndOfData)
            {
                totalRows++;
                string[]? fields = parser.ReadFields();
                if (fields == null) continue;

                string vin = SafeGet(fields, vinCol);
                if (string.IsNullOrWhiteSpace(vin))
                {
                    skippedNoVin++;
                    continue;
                }

                string client = SafeGet(fields, clientCol);
                string model = SafeGet(fields, modelCol);
                string finPrepStr = SafeGet(fields, finPrepCol);

                if (string.IsNullOrWhiteSpace(finPrepStr))
                {
                    skippedNoDate++;
                    continue;
                }

                // Parse date – try common formats
                DateTime? orderDate = null;
                string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" };
                foreach (var fmt in formats)
                {
                    if (DateTime.TryParseExact(finPrepStr, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        orderDate = d;
                        break;
                    }
                }

                if (!orderDate.HasValue)
                {
                    if (!DateTime.TryParse(finPrepStr, out var d))
                    {
                        skippedDateParse++;
                        continue;
                    }
                    orderDate = d;
                }

                results.Add(new ImportWorkOrderDto
                {
                    Chassis = vin,
                    Model = model,
                    ClientName = client,
                    OrderDate = orderDate.Value,
                    Source = "CSV"
                });
                imported++;
            }

            if (imported == 0 && totalRows > 0)
            {
                Debug.WriteLine($"CSV Parse: Total={totalRows}, NoVIN={skippedNoVin}, NoDate={skippedNoDate}, DateParseFail={skippedDateParse}");
            }

            return results;
        }

        private static int FindColumn(List<string> headers, string[] possibleNames)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                string header = headers[i];
                foreach (var name in possibleNames)
                {
                    if (header.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        return i;
                }
            }
            return -1;
        }

        private static string SafeGet(string[] fields, int index)
        {
            return index >= 0 && index < fields.Length ? fields[index]?.Trim() ?? "" : "";
        }
    }
}