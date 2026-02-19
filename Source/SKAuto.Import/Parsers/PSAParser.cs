using ClosedXML.Excel;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Import.Parsers
{
    public class PSAParser : IImportParser
    {
        public async Task<ImportResult> ParseAsync(string filePath)
        {
            var result = new ImportResult();

            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1); // First sheet

                // Validate PSA template format
                if (!IsValidPSATemplate(worksheet))
                {
                    result.Errors.Add("Invalid PSA template format");
                    return result;
                }

                var workOrders = new List<WorkOrder>();
                var row = 2; // Assuming row 1 is header

                while (!worksheet.Cell(row, 1).IsEmpty())
                {
                    var importDto = ParseRow(worksheet, row);
                    if (importDto != null)
                    {
                        var workOrder = await ConvertToWorkOrderAsync(importDto);
                        workOrders.Add(workOrder);
                        result.RecordsProcessed++;
                    }
                    row++;
                }

                result.WorkOrders = workOrders;
                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error parsing PSA file: {ex.Message}");
            }

            return result;
        }

        private bool IsValidPSATemplate(IXLWorksheet worksheet)
        {
            // Check expected headers
            var headers = new[] { "Chassis", "Model", "Client", "Accessory", "Time Estimate" };
            for (int i = 0; i < headers.Length; i++)
            {
                if (worksheet.Cell(1, i + 1).GetString() != headers[i])
                    return false;
            }
            return true;
        }

        private ImportTemplateDto? ParseRow(IXLWorksheet worksheet, int row)
        {
            try
            {
                var dto = new ImportTemplateDto
                {
                    ChassisNumber = worksheet.Cell(row, 1).GetString(),
                    Model = worksheet.Cell(row, 2).GetString(),
                    ClientName = worksheet.Cell(row, 3).GetString(),
                    OrderDate = DateTime.Today
                };

                // Parse accessory and time
                var accessoryName = worksheet.Cell(row, 4).GetString();
                var timeEstimate = worksheet.Cell(row, 5).GetString();

                if (!string.IsNullOrEmpty(accessoryName))
                {
                    dto.Tasks.Add(new ImportTaskDto
                    {
                        AccessoryName = accessoryName,
                        TaskType = "fit",
                        Quantity = 1,
                        EstimatedMinutes = ParseTimeEstimate(timeEstimate)
                    });
                }

                return dto;
            }
            catch
            {
                return null;
            }
        }

        private int? ParseTimeEstimate(string time)
        {
            if (string.IsNullOrEmpty(time)) return null;

            // Parse formats like "1h30", "2h", "45min"
            if (time.Contains("h"))
            {
                var parts = time.Split('h');
                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[0], out int hours) &&
                        int.TryParse(parts[1].Replace("min", ""), out int minutes))
                        return hours * 60 + minutes;
                }
                else if (parts.Length == 1)
                {
                    if (int.TryParse(parts[0].Replace("h", ""), out int hours))
                        return hours * 60;
                }
            }
            else if (time.Contains("min"))
            {
                if (int.TryParse(time.Replace("min", ""), out int minutes))
                    return minutes;
            }

            return null;
        }

        private async Task<WorkOrder> ConvertToWorkOrderAsync(ImportTemplateDto dto)
        {
            var workOrder = new WorkOrder
            {
                OrderDate = dto.OrderDate,
                OrderType = OrderType.PSA_Contract,
                Status = WorkStatus.Planned
            };

            // Note: Client and Vehicle will be linked during validation
            // Accessories will be resolved during validation

            return workOrder;
        }
    }

    public interface IImportParser
    {
        Task<ImportResult> ParseAsync(string filePath);
    }
}