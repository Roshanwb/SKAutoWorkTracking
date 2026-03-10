using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SKAuto.Core.Enums;

namespace SKAuto.Core.Entities
{
    public class WorkTask : BaseEntity
    {
        public int WorkOrderId { get; set; }
        public int AccessoryId { get; set; }
        public TaskType TaskType { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal? Price { get; set; }
        public int? EstimatedMinutes { get; set; }
        public int? ActualMinutes { get; set; }
        public WorkStatus TaskStatus { get; set; } = WorkStatus.Planned;
        public string? Notes { get; set; }

        // Navigation properties
        public virtual WorkOrder WorkOrder { get; set; } = null!;
        public virtual Accessory Accessory { get; set; } = null!;

        // Business methods
        public decimal CalculateTotal()
        {
            decimal total = 0;

            if (Price.HasValue)
                total += Price.Value * Quantity;


            return total;
        }

        public bool IsFittingTask => TaskType == TaskType.Fit;
    }
}
