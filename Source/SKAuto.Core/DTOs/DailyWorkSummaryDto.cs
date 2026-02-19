using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.DTOs
{
    public class DailyWorkSummaryDto
    {
        public DateTime Date { get; set; }
        public int TotalWorkOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int InProgressOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalPSARevenue { get; set; }
        public decimal TotalDirectRevenue { get; set; }
        public List<ClientSummaryDto> ClientSummaries { get; set; } = new();
        public List<VehicleWorkDto> VehicleWork { get; set; } = new();
    }

    public class ClientSummaryDto
    {
        public string ClientName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class VehicleWorkDto
    {
        public string ChassisNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public List<string> AccessoriesFitted { get; set; } = new();
        public decimal TotalCost { get; set; }
    }
}
