using System.Diagnostics;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SKAuto.Import.Parsers
{
    public class SimplePdfParser
    {
        private static readonly HashSet<string> FrenchMonths = new()
        {
            "janvier", "février", "mars", "avril", "mai", "juin",
            "juillet", "août", "septembre", "octobre", "novembre", "décembre"
        };

        private static readonly HashSet<string> ExcludedClientWords = new()
        {
            "ok", "acc", "kit", "logos", "conforme", "pneus", "att.", "att. RV"
        };

        private static readonly string[] Categories = new[]
        {
            "ACCESSOIRE CITROEN",
            "ACCESSOIRE PEUGEOT",
            "INTERVENANT EXT PEUGEOT",
            "TRANSPORT CITROEN I",
            "TRANSPORT CITROEN II",
            "TRANSPORT OPEL",
            "TRANSPORT PEUGEOT I",
            "TRANSPORT PEUGEOT II"
        };

        private static readonly HashSet<string> OutputCategories = new()
        {
            "ACCESSOIRE CITROEN",
            "ACCESSOIRE PEUGEOT"
        };

        private readonly string _logPath;

        public SimplePdfParser()
        {
            _logPath = Path.Combine(Path.GetTempPath(), "SKAuto_PdfParser.log");
            File.WriteAllText(_logPath, $"=== PDF Parser Log {DateTime.Now} ===\n");
        }

        private void Log(string message)
        {
            File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss} - {message}\n");
            Debug.WriteLine(message);
        }

        public List<PdfWorkOrder> Parse(string filePath)
        {
            var results = new List<PdfWorkOrder>();
            Log($"\n--- Processing file: {filePath} ---");

            using var pdf = PdfDocument.Open(filePath);
            if (pdf.NumberOfPages == 0)
            {
                Log("PDF has no pages.");
                return results;
            }

            // Extract date from first page
            var globalDate = ExtractDate(pdf.GetPage(1));
            Log($"Extracted global date: {globalDate:yyyy-MM-dd}");

            string currentCategory = "Unknown";

            foreach (var page in pdf.GetPages())
            {
                Log($"\n--- Page {page.Number} ---");
                var words = page.GetWords().ToList();
                Log($"Total words: {words.Count}");

                if (!words.Any()) continue;

                // Group words into lines by Y coordinate (with tolerance)
                var lines = words
                    .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                    .OrderByDescending(g => g.Key)
                    .Select(g => g.OrderBy(w => w.BoundingBox.Left).ToList())
                    .ToList();

                Log($"Grouped into {lines.Count} lines.");

                // Detect category from lines
                foreach (var line in lines)
                {
                    var lineText = string.Join(" ", line.Select(w => w.Text)).Trim();
                    foreach (var cat in Categories)
                    {
                        if (lineText.Contains(cat, StringComparison.OrdinalIgnoreCase))
                        {
                            currentCategory = cat;
                            Log($"Category detected: '{currentCategory}' from line: '{lineText}'");
                            break;
                        }
                    }
                }

                // Scan each line for VINs
                foreach (var line in lines)
                {
                    var lineWords = line.Select(w => w.Text).ToList();
                    var lineText = string.Join(" ", lineWords);

                    // Skip if line is too short
                    if (lineWords.Count < 2) continue;

                    // Look for a word that looks like a VIN
                    string vin = null;
                    int vinIndex = -1;
                    for (int i = 0; i < lineWords.Count; i++)
                    {
                        var candidate = CleanVin(lineWords[i]);
                        if (IsValidVin(candidate))
                        {
                            vin = candidate;
                            vinIndex = i;
                            break;
                        }
                    }

                    if (vin == null) continue;

                    // Extract model and client from words after VIN
                    string model = "";
                    string client = "";

                    // Next word (if exists) as model
                    if (vinIndex + 1 < lineWords.Count)
                        model = lineWords[vinIndex + 1].Trim();

                    // Next next word (if exists) as client
                    if (vinIndex + 2 < lineWords.Count)
                        client = lineWords[vinIndex + 2].Trim();

                    // Clean client
                    client = CleanClient(client);

                    // Only keep if category is in output list
                    if (OutputCategories.Contains(currentCategory))
                    {
                        results.Add(new PdfWorkOrder
                        {
                            Chassis = vin,
                            Model = model,
                            ClientName = client,
                            OrderDate = globalDate
                        });
                        Log($"Accepted: VIN={vin}, Model={model}, Client={client}, Cat={currentCategory}");
                    }
                    else
                    {
                        Log($"Skipped (category not output): VIN={vin}, Cat={currentCategory}");
                    }
                }
            }

            Log($"Total extracted from this file: {results.Count}");
            return results;
        }

        private DateTime ExtractDate(Page firstPage)
        {
            try
            {
                var text = firstPage.Text;
                var match = Regex.Match(text, @"Liste des rendez-vous pour le\s+(.+?)(?:\n|$)", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    Log("Date pattern not found, using today.");
                    return DateTime.Today;
                }

                var rawDate = match.Groups[1].Value.Trim();
                Log($"Raw date string: '{rawDate}'");

                var parts = rawDate.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return DateTime.Today;

                int day = 0, year = 0;
                string monthName = "";

                foreach (var part in parts)
                {
                    var lower = part.ToLowerInvariant();
                    if (FrenchMonths.Contains(lower))
                    {
                        monthName = lower;
                    }
                    else if (int.TryParse(part, out int num))
                    {
                        if (num > 31)
                            year = num;
                        else if (day == 0)
                            day = num;
                    }
                }

                if (day < 1 || day > 31)
                    day = DateTime.Today.Day;
                if (string.IsNullOrEmpty(monthName) || !FrenchMonths.Contains(monthName))
                    monthName = FrenchMonths.ElementAt(DateTime.Today.Month - 1);
                int month = Array.IndexOf(FrenchMonths.ToArray(), monthName) + 1;
                if (year < 2000 || year > 2100)
                    year = DateTime.Today.Year;

                Log($"Parsed date: {day}/{month}/{year}");
                return new DateTime(year, month, day);
            }
            catch (Exception ex)
            {
                Log($"Date parsing exception: {ex.Message}. Using today.");
                return DateTime.Today;
            }
        }

        private string CleanVin(string vin)
        {
            if (string.IsNullOrEmpty(vin)) return "";
            // Remove common non-VIN characters
            vin = Regex.Replace(vin, @"[^A-Z0-9]", "", RegexOptions.IgnoreCase);
            return vin;
        }

        private bool IsValidVin(string vin)
        {
            if (string.IsNullOrEmpty(vin) || vin.Length < 5 || vin.Length > 17)
                return false;

            // Reject common non-VIN patterns
            if (Regex.IsMatch(vin, @"^\d+$")) return false; // all digits
            if (Regex.IsMatch(vin, @"^\d{2}[A-Z]?\d{2}$")) return false; // like "25HT", "30E"
            if (Regex.IsMatch(vin, @"^\d{2}/\d{2}")) return false; // date

            // Must contain both letters and digits
            bool hasLetter = vin.Any(char.IsLetter);
            bool hasDigit = vin.Any(char.IsDigit);
            if (!hasLetter || !hasDigit) return false;

            // Likely starts with a letter (most VINs do)
            if (!char.IsLetter(vin[0])) return false;

            return true;
        }

        private string CleanClient(string client)
        {
            if (string.IsNullOrEmpty(client)) return "";

            foreach (var word in ExcludedClientWords)
                client = Regex.Replace(client, $@"\b{Regex.Escape(word)}\b", "", RegexOptions.IgnoreCase);

            client = Regex.Replace(client, @"\b\d{1,2}\b", "");
            client = Regex.Replace(client, @"\d{2}/\d{2}", "");
            client = Regex.Replace(client, @"\s+", " ").Trim();
            client = Regex.Replace(client, @"\s*/\s*$", "");

            return client;
        }
    }

    public class PdfWorkOrder
    {
        public string Chassis { get; set; } = "";
        public string Model { get; set; } = "";
        public string ClientName { get; set; } = "";
        public DateTime OrderDate { get; set; }
    }
}