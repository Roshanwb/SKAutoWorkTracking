using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Import.Models
{
    public class CsvImportRow
    {
        public string Chassis { get; set; } = "";
        public string Model { get; set; } = "";
        public string ClientName { get; set; } = "";
        public DateTime OrderDate { get; set; }
    }
}