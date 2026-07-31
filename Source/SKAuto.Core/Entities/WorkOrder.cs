using SKAuto.Core.Enums;

namespace SKAuto.Core.Entities
{
    public class WorkOrder : BaseEntity
    {
        public int VehicleId { get; set; }
        public string? OrderReference { get; set; }
        public OrderType OrderType { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? PlannedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public WorkStatus Status { get; set; } = WorkStatus.Planned;
        public decimal? TotalAmount { get; set; }
        public string? Notes { get; set; }

        // NEW: Billing status
        public BillingStatus BillingStatus { get; set; } = BillingStatus.ToDo;

        // Navigation properties
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