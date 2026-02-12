using Microsoft.EntityFrameworkCore;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SKAuto.Data.Repository;

namespace SKAuto.Data.Repository
{
    public class WorkOrderRepository : BaseRepository<WorkOrder>, IRepository<WorkOrder>
    {
        public WorkOrderRepository(DatabaseContext context) : base(context) { }

        public async Task<WorkOrder?> GetWithDetailsAsync(int id)
        {
            return await _dbSet
                .Include(w => w.Client)
                .Include(w => w.Vehicle)
                .Include(w => w.WorkTasks)
                    .ThenInclude(t => t.Accessory)
                .Include(w => w.Travels)
                .Include(w => w.SourceDocuments)
                .FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task<IEnumerable<WorkOrderDto>> GetDailyWorkOrdersAsync(DateTime date)
        {
            var orders = await _dbSet
                .Where(w => w.OrderDate.Date == date.Date)
                .Include(w => w.Client)
                .Include(w => w.Vehicle)
                .Include(w => w.WorkTasks)
                .Include(w => w.Travels)
                .ToListAsync();

            return orders.Select(WorkOrderDto.FromEntity);
        }

        public async Task<IEnumerable<WorkOrder>> GetByStatusAsync(WorkStatus status)
        {
            return await _dbSet
                .Where(w => w.Status == status)
                .Include(w => w.Client)
                .Include(w => w.Vehicle)
                .ToListAsync();
        }

        public async Task<IEnumerable<WorkOrder>> GetByClientAsync(int clientId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _dbSet.Where(w => w.ClientId == clientId);

            if (fromDate.HasValue)
                query = query.Where(w => w.OrderDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(w => w.OrderDate <= toDate.Value);

            return await query
                .Include(w => w.Vehicle)
                .Include(w => w.WorkTasks)
                    .ThenInclude(t => t.Accessory)
                .OrderByDescending(w => w.OrderDate)
                .ToListAsync();
        }

        public async Task<DailyWorkSummaryDto> GetDailySummaryAsync(DateTime date)
        {
            var orders = await _dbSet
                .Where(w => w.OrderDate.Date == date.Date)
                .Include(w => w.Client)
                .Include(w => w.Vehicle)
                .Include(w => w.WorkTasks)
                    .ThenInclude(t => t.Accessory)
                .Include(w => w.Travels)
                .ToListAsync();

            var summary = new DailyWorkSummaryDto
            {
                Date = date,
                TotalWorkOrders = orders.Count,
                CompletedOrders = orders.Count(o => o.Status == WorkStatus.Done),
                InProgressOrders = orders.Count(o => o.Status == WorkStatus.InProgress),
                TotalRevenue = orders.Where(o => o.TotalAmount.HasValue).Sum(o => o.TotalAmount.Value),
                TotalPSARevenue = orders
                    .Where(o => o.Client.Type == ClientType.PSA && o.TotalAmount.HasValue)
                    .Sum(o => o.TotalAmount.Value),
                TotalDirectRevenue = orders
                    .Where(o => o.Client.Type == ClientType.Direct && o.TotalAmount.HasValue)
                    .Sum(o => o.TotalAmount.Value)
            };

            // Client summaries
            summary.ClientSummaries = orders
                .GroupBy(o => o.Client.Name)
                .Select(g => new ClientSummaryDto
                {
                    ClientName = g.Key,
                    OrderCount = g.Count(),
                    TotalAmount = g.Where(o => o.TotalAmount.HasValue).Sum(o => o.TotalAmount.Value)
                })
                .ToList();

            // Vehicle work
            summary.VehicleWork = orders
                .Select(o => new VehicleWorkDto
                {
                    ChassisNumber = o.Vehicle.ChassisNumber,
                    Model = o.Vehicle.Model,
                    AccessoriesFitted = o.WorkTasks
                        .Where(t => t.TaskType == TaskType.Fit)
                        .Select(t => t.Accessory.Name)
                        .Distinct()
                        .ToList(),
                    TotalCost = o.TotalAmount ?? 0
                })
                .ToList();

            return summary;
        }

        public async Task<int> GetOrderCountForDateAsync(DateTime date)
        {
            return await _dbSet.CountAsync(w => w.OrderDate.Date == date.Date);
        }

        public async Task<decimal> GetTotalRevenueForPeriodAsync(DateTime fromDate, DateTime toDate)
        {
            var total = await _dbSet
                .Where(w => w.OrderDate >= fromDate && w.OrderDate <= toDate && w.TotalAmount.HasValue)
                .SumAsync(w => w.TotalAmount.Value);

            return total;
        }
    }
}