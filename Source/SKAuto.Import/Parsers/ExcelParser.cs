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
    public class ExcelParser : IImportParser
    {
        public async Task<ImportResult> ParseAsync(string filePath)
        {
            var result = new ImportResult();

            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);

                var workOrders = new List<WorkOrder>();
                var row = 2; // Skip header

                while (!worksheet.Cell(row, 1).IsEmpty())
                {
                    try
                    {
                        var workOrder = ParseWorkOrderRow(worksheet, row);
                        if (workOrder != null)
                        {
                            workOrders.Add(workOrder);
                            result.RecordsProcessed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Row {row}: {ex.Message}");
                    }
                    row++;
                }

                result.WorkOrders = workOrders;
                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error parsing Excel file: {ex.Message}");
            }

            return result;
        }

        private WorkOrder? ParseWorkOrderRow(IXLWorksheet worksheet, int row)
        {
            // Basic validation
            var chassis = worksheet.Cell(row, 1).GetString();
            var clientName = worksheet.Cell(row, 2).GetString();

            if (string.IsNullOrEmpty(chassis) || string.IsNullOrEmpty(clientName))
                return null;

            var workOrder = new WorkOrder
            {
                OrderDate = ParseDate(worksheet.Cell(row, 3).GetString()) ?? DateTime.Today,
                OrderType = ParseOrderType(worksheet.Cell(row, 4).GetString()),
                Status = ParseStatus(worksheet.Cell(row, 5).GetString()) ?? WorkStatus.Planned,
                Notes = worksheet.Cell(row, 6).GetString()
            };

            // Parse tasks if present
            var taskColumn = 7;
            while (!worksheet.Cell(row, taskColumn).IsEmpty())
            {
                var task = ParseTask(worksheet, row, taskColumn);
                if (task != null)
                    workOrder.WorkTasks.Add(task);

                taskColumn += 4; // Move to next task group
            }

            return workOrder;
        }

        private WorkTask ParseTask(IXLWorksheet worksheet, int row, int startColumn)
        {
            var accessoryName = worksheet.Cell(row, startColumn).GetString();
            if (string.IsNullOrEmpty(accessoryName))
                return null;

            return new WorkTask
            {
                TaskType = ParseTaskType(worksheet.Cell(row, startColumn + 1).GetString()),
                Quantity = ParseInt(worksheet.Cell(row, startColumn + 2).GetString()) ?? 1,
                UnitPrice = ParseDecimal(worksheet.Cell(row, startColumn + 3).GetString()),
                EstimatedMinutes = ParseInt(worksheet.Cell(row, startColumn + 4).GetString())
            };
        }

        // Helper methods for parsing
        private DateTime? ParseDate(string value)
        {
            if (DateTime.TryParse(value, out DateTime date))
                return date;
            return null;
        }

        private OrderType ParseOrderType(string value)
        {
            return value?.ToLower() switch
            {
                "psa" or "psa_contract" => OrderType.PSA_Contract,
                "direct_fitting" => OrderType.Direct_Fitting,
                "direct_sale" => OrderType.Direct_Sale,
                _ => OrderType.Direct_Fitting
            };
        }

        private WorkStatus? ParseStatus(string value)
        {
            return value?.ToLower() switch
            {
                "planned" => WorkStatus.Planned,
                "inprogress" or "in_progress" => WorkStatus.InProgress,
                "blocked" => WorkStatus.Blocked,
                "done" => WorkStatus.Done,
                _ => null
            };
        }

        private TaskType ParseTaskType(string value)
        {
            return value?.ToLower() switch
            {
                "sell" => TaskType.Sell,
                "remove" => TaskType.Remove,
                _ => TaskType.Fit
            };
        }

        private int? ParseInt(string value)
        {
            if (int.TryParse(value, out int result))
                return result;
            return null;
        }

        private decimal? ParseDecimal(string value)
        {
            if (decimal.TryParse(value, out decimal result))
                return result;
            return null;
        }
    }
}