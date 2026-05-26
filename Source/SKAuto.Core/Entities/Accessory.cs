using System;
using System.Collections.Generic;
using SKAuto.Core.Enums;

namespace SKAuto.Core.Entities
{
    public class Accessory : BaseEntity
    {
        public string? PartNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? Time { get; set; } // Minutes
        public decimal? Price { get; set; }
        public bool RequiresPassword { get; set; }
        public bool IsActive { get; set; } = true;

        // NEW: Task type for this accessory (default Fit)
        public TaskType TaskType { get; set; } = TaskType.Fit;

        // Navigation properties
        public virtual ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
        public virtual ICollection<ProtectedRate> ProtectedRates { get; set; } = new List<ProtectedRate>();
    }
}