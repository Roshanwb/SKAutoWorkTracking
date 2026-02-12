using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;

namespace SKAuto.Import.Validators
{
    public class EODValidationService : IEODValidationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public EODValidationService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<ValidationResult> ValidateDayAsync(DateTime date)
        {
            var result = new ValidationResult();
            var orders = await _unitOfWork.WorkOrders
                .FindAsync(w => w.OrderDate.Date == date.Date);

            if (!orders.Any())
            {
                result.AddWarning("General", $"No work orders for {date:yyyy-MM-dd}");
                return result;
            }

            foreach (var order in orders)
            {
                // Each order must have at least one task
                if (!order.WorkTasks.Any())
                    result.AddError($"WO-{order.Id}", "No tasks assigned");

                // Tasks cannot be in 'Planned' if order date is today
                if (order.OrderDate.Date == date.Date)
                {
                    var plannedTasks = order.WorkTasks
                        .Where(t => t.TaskStatus == WorkStatus.Planned);
                    foreach (var task in plannedTasks)
                        result.AddWarning($"WO-{order.Id}",
                            $"Task '{task.Accessory?.Name}' still Planned on due date");
                }

                // Validate date logic
                if (order.CompletedDate.HasValue && order.CompletedDate < order.OrderDate)
                    result.AddError($"WO-{order.Id}", "Completed date before order date");
            }

            // Duplicate detection (same chassis + accessory on same day)
            var potentialDuplicates = orders
                .SelectMany(o => o.WorkTasks, (o, t) => new { o.Vehicle?.ChassisNumber, t.Accessory?.Name, o.OrderDate })
                .GroupBy(x => new { x.ChassisNumber, x.Name, x.OrderDate })
                .Where(g => g.Count() > 1);

            foreach (var dup in potentialDuplicates)
                result.AddWarning("Duplicate",
                    $"Vehicle {dup.Key.ChassisNumber} – Accessory '{dup.Key.Name}' " +
                    $"appears {dup.Count()} times on {dup.Key.OrderDate:yyyy-MM-dd}");

            return result;
        }
    }
}