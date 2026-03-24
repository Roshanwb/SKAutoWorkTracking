using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace SKAuto.Import.Validators
{
    public class ImportValidator : IValidationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ChassisValidator _chassisValidator;

        public ImportValidator(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _chassisValidator = new ChassisValidator();
        }

        public async Task<ImportResult> ValidateImportAsync(ImportResult preliminaryResult)
        {
            // This method would be used with your existing import result structure.
            // Since we don't have the full context, we'll keep it as a placeholder.
            // The main validation methods below are the ones used.
            return preliminaryResult;
        }

        public async Task<ValidationResult> ValidateWorkOrderAsync(WorkOrder workOrder)
        {
            var result = new ValidationResult();

            // Validate chassis (via vehicle)
            if (workOrder.Vehicle == null)
            {
                result.AddError("Vehicle", "Vehicle is required");
            }
            else
            {
                var chassisResult = _chassisValidator.Validate(workOrder.Vehicle.ChassisNumber);
                if (!chassisResult.IsValid)
                {
                    foreach (var error in chassisResult.Errors)
                        result.AddError("Vehicle", error.Message);
                }
            }

            // Check if client exists (via vehicle's client)
            if (workOrder.Vehicle?.Client == null)
            {
                result.AddError("Client", "Client could not be determined from vehicle");
            }

            // Validate dates
            if (workOrder.OrderDate > DateTime.Today.AddDays(7))
            {
                result.AddWarning("OrderDate", "Order date is more than 7 days in the future");
            }

            if (workOrder.PlannedDate.HasValue && workOrder.PlannedDate < workOrder.OrderDate)
            {
                result.AddError("PlannedDate", "Planned date cannot be before order date");
            }

            // Validate tasks
            if (!workOrder.WorkTasks.Any())
            {
                result.AddError("Tasks", "Work order must have at least one task");
            }
            else
            {
                foreach (var task in workOrder.WorkTasks)
                {
                    var taskValidation = ValidateWorkTask(task);
                    if (!taskValidation.IsValid)
                    {
                        foreach (var error in taskValidation.Errors)
                            result.AddError($"Task-{task.Accessory?.Name}", error.Message);
                    }
                }
            }

            // PSA specific validations
            if (workOrder.OrderType == OrderType.PSA_Contract)
            {
                if (workOrder.WorkTasks.Any(t => t.Price.HasValue))
                {
                    result.AddWarning("PSA", "PSA orders should not have prices (uses PSA rates)");
                }

                if (workOrder.WorkTasks.Any(t => !t.EstimatedMinutes.HasValue))
                {
                    result.AddWarning("PSA", "PSA tasks should have time estimates");
                }
            }

            // Direct order validations
            if (workOrder.OrderType == OrderType.Direct_Sale || workOrder.OrderType == OrderType.Direct_Fitting)
            {
                if (workOrder.WorkTasks.Any(t => !t.Price.HasValue))
                {
                    result.AddError("Pricing", "Direct orders must have prices for all tasks");
                }
            }

            return result;
        }

        private ValidationResult ValidateWorkTask(WorkTask task)
        {
            var result = new ValidationResult();

            if (task.Accessory == null || string.IsNullOrEmpty(task.Accessory.Name))
            {
                result.AddError("Accessory", "Task must have an accessory");
            }

            if (task.Quantity <= 0)
            {
                result.AddError("Quantity", "Quantity must be greater than 0");
            }

            if (task.Quantity > 100)
            {
                result.AddWarning("Quantity", "Unusually high quantity");
            }

            if (task.Price.HasValue && task.Price < 0)
            {
                result.AddError("Price", "Price cannot be negative");
            }

            if (task.EstimatedMinutes.HasValue && task.EstimatedMinutes < 0)
            {
                result.AddError("EstimatedMinutes", "Time estimate cannot be negative");
            }

            if (task.EstimatedMinutes.HasValue && task.EstimatedMinutes > 480) // 8 hours
            {
                result.AddWarning("EstimatedMinutes", "Task time estimate exceeds 8 hours");
            }

            return result;
        }

        public async Task<ValidationResult> ValidateEODDataAsync(DateTime date)
        {
            var result = new ValidationResult();

            // Get all work orders for the date
            var orders = await _unitOfWork.WorkOrders.FindAsync(w => w.OrderDate.Date == date.Date);

            if (!orders.Any())
            {
                result.AddWarning("NoData", $"No work orders found for {date:dd/MM/yyyy}");
                return result;
            }

            // Check for incomplete orders
            var incompleteOrders = orders.Where(o => o.Status != WorkStatus.Done).ToList();
            if (incompleteOrders.Any())
            {
                result.AddWarning("Incomplete",
                    $"{incompleteOrders.Count} orders not marked as done: {string.Join(", ", incompleteOrders.Select(o => $"WO-{o.Id}"))}");
            }

            // Check for orders without tasks
            var ordersWithoutTasks = orders.Where(o => !o.WorkTasks.Any()).ToList();
            if (ordersWithoutTasks.Any())
            {
                result.AddError("NoTasks",
                    $"{ordersWithoutTasks.Count} orders have no tasks: {string.Join(", ", ordersWithoutTasks.Select(o => $"WO-{o.Id}"))}");
            }

            // Validate totals
            foreach (var order in orders)
            {
                order.CalculateTotal();

                if (order.TotalAmount.HasValue && order.TotalAmount < 0)
                {
                    result.AddError($"WO-{order.Id}", $"Negative total amount: {order.TotalAmount:C}");
                }

                if (order.OrderType == OrderType.Direct_Sale && !order.TotalAmount.HasValue)
                {
                    result.AddWarning($"WO-{order.Id}", "Direct sale order has no total amount");
                }
            }

            return result;
        }

        public async Task<ValidationResult> ValidateVehicleAsync(Vehicle vehicle)
        {
            var result = new ValidationResult();

            var chassisResult = _chassisValidator.Validate(vehicle.ChassisNumber);
            if (!chassisResult.IsValid)
            {
                foreach (var error in chassisResult.Errors)
                    result.AddError("ChassisNumber", error.Message);
            }

            if (string.IsNullOrEmpty(vehicle.Model))
            {
                result.AddError("Model", "Vehicle model is required");
            }

            // Check for duplicate chassis
            var existing = await _unitOfWork.Vehicles.FindAsync(v => v.ChassisNumber == vehicle.ChassisNumber);
            if (existing.Any(v => v.Id != vehicle.Id))
            {
                result.AddError("ChassisNumber", $"Vehicle with chassis {vehicle.ChassisNumber} already exists");
            }

            return result;
        }

        public async Task<ValidationResult> ValidateChassisNumberAsync(string chassisNumber)
        {
            var result = _chassisValidator.Validate(chassisNumber);

            if (result.IsValid)
            {
                var existing = await _unitOfWork.Vehicles.FindAsync(v => v.ChassisNumber == chassisNumber);
                if (existing.Any())
                {
                    result.AddWarning("Exists", $"Vehicle with chassis {chassisNumber} already exists in database");
                }
            }

            return result;
        }
    }
}