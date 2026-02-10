using SKAuto.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.Interfaces
{
    public interface IImportService
    {
        Task<ImportResult> ImportPSAPlanAsync(string filePath);
        Task<ImportResult> ImportClientOrderAsync(string filePath);
        Task<ImportResult> ImportExcelTemplateAsync(string filePath);
        Task<ImportResult> ValidateImportAsync(ImportResult preliminaryResult);
        Task CommitImportAsync(ImportResult validatedResult);
    }

    public class ImportResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public int RecordsProcessed { get; set; }
        public int RecordsAdded { get; set; }
        public int RecordsUpdated { get; set; }
        public List<WorkOrder> WorkOrders { get; set; } = new();
        public string Summary => $"{RecordsProcessed} processed, {RecordsAdded} added, {RecordsUpdated} updated";
    }
}