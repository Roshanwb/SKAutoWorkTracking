using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SKAuto.Core.Entities;
using SKAuto.Core.Enums;

namespace SKAuto.Core.DTOs
{
    public class WorkOrderDto
    {
        public int Id { get; set; }
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

        public static WorkOrderDto FromEntity(WorkOrder workOrder)
        {
            return new WorkOrderDto
            {
                Id = workOrder.Id,
                ClientName = workOrder.Client.Name,
                VehicleChassis = workOrder.Vehicle.ChassisNumber,
                VehicleModel = workOrder.Vehicle.Model,
                OrderType = workOrder.OrderType,
                OrderDate = workOrder.OrderDate,
                Status = workOrder.Status,
                TotalAmount = workOrder.TotalAmount,
                TaskCount = workOrder.WorkTasks.Count,
                HasTravel = workOrder.Travels.Any(),
                Notes = workOrder.Notes
            };
        }
    }
}