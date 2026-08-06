using SKAuto.Core.Enums;

namespace SKAuto.Core.DTOs
{
    public class ImportWorkOrderDto
    {
        public string Chassis { get; set; } = "";
        public string Model { get; set; } = "";
        public string ClientName { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string Source { get; set; } = ""; // "PDF" or "Excel"
        public OrderType TypeOfWork { get; set; } = OrderType.PSA_Sur_Site ;
        public bool HasDate { get; set; }                 // true if date was present and parsed
        public string Etat { get; set; } = "";
    }
}