namespace SKAuto.Core.Interfaces
{
    public interface IAuditService
    {
        Task LogImportAsync(string filePath, int records, string user);
        Task LogExportAsync(string reportType, DateTime date, string user);
        Task<IEnumerable<AuditEntry>> GetAuditLogAsync(DateTime from, DateTime to);
    }

    public class AuditEntry
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}