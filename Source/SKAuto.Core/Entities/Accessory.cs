using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.Entities
{
    public class Accessory : BaseEntity
    {
        public string? PartNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? StandardFittingTime { get; set; } // Minutes
        public decimal? PSAHourlyRate { get; set; }
        public bool RequiresPassword { get; set; }

        // Navigation properties
        public virtual ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
        public virtual ICollection<ProtectedRate> ProtectedRates { get; set; } = new List<ProtectedRate>();
    }
}
