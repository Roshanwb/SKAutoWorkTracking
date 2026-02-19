using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.Entities
{
    public class Travel : BaseEntity
    {
        public int WorkOrderId { get; set; }
        public DateTime TravelDate { get; set; }
        public string Destination { get; set; } = string.Empty;
        public decimal? DistanceKm { get; set; }
        public decimal? TravelCost { get; set; }
        public string? Notes { get; set; }

        // Navigation property
        public virtual WorkOrder WorkOrder { get; set; } = null!;
    }
}
