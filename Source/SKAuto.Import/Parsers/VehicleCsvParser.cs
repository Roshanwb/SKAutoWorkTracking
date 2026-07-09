using Microsoft.VisualBasic.FileIO;
using SKAuto.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SKAuto.Import.Parsers
{
    public class VehicleCsvParser
    {
        public List<VehicleImportDto> Parse(string filePath)
        {
            var results = new List<VehicleImportDto>();

            using var parser = new TextFieldParser(filePath, Encoding.Default);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(";");
            parser.HasFieldsEnclosedInQuotes = true;
            parser.TrimWhiteSpace = true;

            // Read header
            string[]? headers = parser.ReadFields();
            if (headers == null || headers.Length == 0)
                return results;

            var headerList = new List<string>();
            for (int i = 0; i < headers.Length; i++)
                headerList.Add((headers[i] ?? "").Trim());

            // Find required columns
            int vinCol = FindColumn(headerList, new[] { "N° (VIN)", "VIN", "N°" });
            int clientCol = FindColumn(headerList, new[] { "Client" });
            int modeleCol = FindColumn(headerList, new[] { "Modèle", "Modele" });

            // VIN and Client are mandatory; Model is optional (fallback to column index 4)
            if (vinCol == -1 || clientCol == -1)
                return results;

            int modelCol = modeleCol != -1 ? modeleCol : 4;

            while (!parser.EndOfData)
            {
                string[]? fields = parser.ReadFields();
                if (fields == null) continue;

                string vin = SafeGet(fields, vinCol);
                if (string.IsNullOrWhiteSpace(vin))
                    continue; // skip rows without VIN

                string client = SafeGet(fields, clientCol);
                string model = SafeGet(fields, modelCol);

                results.Add(new VehicleImportDto
                {
                    ChassisNumber = vin.Trim(),
                    Model = model.Trim(),
                    ClientName = client.Trim()
                });
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