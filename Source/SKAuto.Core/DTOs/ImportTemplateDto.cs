using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.DTOs
{
    public class ImportTemplateDto
    {
        public string ChassisNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public List<ImportTaskDto> Tasks { get; set; } = new();
        public ImportTravelDto? Travel { get; set; }
    }

    public class ImportTaskDto
    {
        public string AccessoryName { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty; // "fit", "sell"
        public int Quantity { get; set; } = 1;
        public decimal? UnitPrice { get; set; }
        public decimal? FittingPrice { get; set; }
        public int? EstimatedMinutes { get; set; }
    }

    public class ImportTravelDto
    {
        public string Destination { get; set; } = string.Empty;
        public decimal? DistanceKm { get; set; }
        public decimal? TravelCost { get; set; }
    }
}