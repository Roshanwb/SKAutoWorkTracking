using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualBasic.FileIO;
using SKAuto.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SKAuto.Import.Parsers
{
    public class ParcCarrieresCsvParser
    {
        // Allowed Etat values – work is finished
        private static readonly HashSet<string> AllowedEtat = new(StringComparer.OrdinalIgnoreCase)
        {
            "Préparé",
            "Livré",
            "Prêt",
            "Non préparé",
            "Atelier"
            // add "Terminé" if needed
        };

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

            // Find required columns (case‑insensitive, partial match)
            int vinCol = FindColumn(headers, new[] { "N° (VIN)", "VIN", "N°" });
            int clientCol = FindColumn(headers, new[] { "Client" });
            int modeleCol = FindColumn(headers, new[] { "Modèle", "Modele" });
            int marqueCol = FindColumn(headers, new[] { "Marque" }); // fallback for model
            int finPrepCol = FindColumn(headers, new[] { "Fin prép.", "Fin prep.", "Fin", "Date fin" });
            int etatCol = FindColumn(headers, new[] { "Etat" });

            if (vinCol == -1 || clientCol == -1 || (modeleCol == -1 && marqueCol == -1) || finPrepCol == -1 || etatCol == -1)
                return results; // missing essential columns

            int modelCol = modeleCol != -1 ? modeleCol : 4;// oveerideed 5th column 

            // Process data rows
            while (!parser.EndOfData)
            {
                string[]? fields = parser.ReadFields();
                if (fields == null) continue;

                string vin = SafeGet(fields, vinCol);
                if (string.IsNullOrWhiteSpace(vin)) continue;

                string etat = SafeGet(fields, etatCol);
                if (!AllowedEtat.Contains(etat)) continue;

                string client = SafeGet(fields, clientCol);
                string model = SafeGet(fields, modelCol);
                string finPrepStr = SafeGet(fields, finPrepCol);

                if (string.IsNullOrWhiteSpace(finPrepStr))
                    finPrepStr = "01/01/1900"; // default date if missing

                // Parse date (expect DD/MM/YYYY)
                if (!DateTime.TryParseExact(finPrepStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var orderDate))
                    continue;

                results.Add(new ImportWorkOrderDto
                {
                    Chassis = vin,
                    Model = model,
                    ClientName = client,
                    OrderDate = orderDate,
                    Source = "ParcCarrières csv"
                });
            }

            return results;
        }

        private static int FindColumn(string[] headers, string[] possibleNames)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                string header = (headers[i] ?? "").Trim();
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