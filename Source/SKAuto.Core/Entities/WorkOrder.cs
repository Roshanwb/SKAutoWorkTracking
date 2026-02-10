using SKAuto.Core.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.Entities
{
    public class WorkOrder : BaseEntity
    {
        public int ClientId { get; set; }
        public int VehicleId { get; set; }
        public string? OrderReference { get; set; }
        public OrderType OrderType { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? PlannedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public WorkStatus Status { get; set; } = WorkStatus.Planned;
        public decimal? TotalAmount { get; set; }
        public string? Notes { get; set; }

        // Navigation properties
        public virtual Client Client { get; set; } = null!;
        public virtual Vehicle Vehicle { get; set; } = null!;
        public virtual ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
        public virtual ICollection<Travel> Travels { get; set; } = new List<Travel>();
        public virtual ICollection<SourceDocument> SourceDocuments { get; set; } = new List<SourceDocument>();

        // Business methods
        public bool IsCompleted => Status == WorkStatus.Done;
        public bool CanAddTravel => Status == WorkStatus.InProgress;

        public void CalculateTotal()
        {
            TotalAmount = WorkTasks.Sum(t => t.CalculateTotal()) +
                         Travels.Sum(t => t.TravelCost ?? 0);
        }
    }
}
