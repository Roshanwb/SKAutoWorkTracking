using SKAuto.Core.Enums;
using System;

namespace SKAuto.Core.DTOs
{
    public class ReportFilter
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public TaskType? TaskType { get; set; }
        public WorkStatus? WorkStatus { get; set; }
        public int? AccessoryId { get; set; }
        public bool GroupByWeek { get; set; }
        public bool SummaryOnly { get; set; }
    }
}