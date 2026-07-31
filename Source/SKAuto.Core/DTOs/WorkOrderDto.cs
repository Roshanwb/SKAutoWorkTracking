using SKAuto.Core.Entities;
using SKAuto.Core.Enums;

namespace SKAuto.Core.DTOs
{
    public class WorkOrderDto
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string VehicleChassis { get; set; } = string.Empty;
        public string VehicleModel { get; set; } = string.Empty;
        public OrderType OrderType { get; set; }
        public DateTime OrderDate { get; set; }
        public WorkStatus Status { get; set; }
        public decimal? TotalAmount { get; set; }
        public int TaskCount { get; set; }
        public bool HasTravel { get; set; }
        public string? Notes { get; set; }
        public BillingStatus BillingStatus { get; set; }
        public int AttachmentCount { get; set; }

        public static WorkOrderDto FromEntity(WorkOrder workOrder)
        {
            return new WorkOrderDto
            {
                Id = workOrder.Id,
                ClientId = workOrder.Vehicle?.ClientId ?? 0,
                ClientName = workOrder.Vehicle?.Client?.Name ?? string.Empty,
                VehicleChassis = workOrder.Vehicle?.ChassisNumber ?? string.Empty,
                VehicleModel = workOrder.Vehicle?.Model ?? string.Empty,
                OrderType = workOrder.OrderType,
                OrderDate = workOrder.OrderDate,
                Status = workOrder.Status,
                TotalAmount = workOrder.TotalAmount,
                TaskCount = workOrder.WorkTasks?.Count ?? 0,
                HasTravel = workOrder.Travels?.Any() ?? false,
                Notes = workOrder.Notes,
                BillingStatus = workOrder.BillingStatus,
                AttachmentCount = workOrder.SourceDocuments?.Count ?? 0
            };
        }
    }
}