using System;
using System.Collections.Generic;

namespace SKAuto.Core.DTOs
{
    public class WeeklySummaryDto
    {
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public int TotalWorkOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int InProgressOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<ClientSummaryDto> ClientSummaries { get; set; } = new();
    }
}