using iText.Kernel.Pdf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace SKAuto.Import.Parsers
{
    public class SimplePdfParser
    {
        public List<PdfWorkOrder> Parse(string path)
        {
            var results = new List<PdfWorkOrder>();
            using (var pdf = UglyToad.PdfPig.PdfDocument.Open(path))
            {
                foreach (var page in pdf.GetPages())
                {
                    var text = page.Text;
                    // Simple heuristic: find lines with "Châssis" and capture the following word/number
                    var matches = Regex.Matches(text, @"Châssis[:\s]*([A-Z0-9]{11,17})", RegexOptions.IgnoreCase);
                    foreach (Match m in matches)
                    {
                        var chassis = m.Groups[1].Value;
                        // For now, set defaults; later we can try to extract nearby model/client/date
                        results.Add(new PdfWorkOrder
                        {
                            Chassis = chassis,
                            Model = "Unknown",
                            ClientName = "Unknown",
                            OrderDate = DateTime.Today
                        });
                    }
                }
            }
            return results;
        }
    }

    public class PdfWorkOrder
    {
        public string Chassis { get; set; }
        public string Model { get; set; }
        public string ClientName { get; set; }
        public DateTime OrderDate { get; set; }
    }
}