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
                .Include(w => w.WorkTasks)
                    .ThenInclude(t => t.Accessory)
                .ToListAsync();

            var summary = new DailyWorkSummaryDto
            {
                Date = date,
                TotalWorkOrders = orders.Count,
                CompletedOrders = orders.Count(o => o.Status == WorkStatus.Done),
                InProgressOrders = orders.Count(o => o.Status == WorkStatus.InProgress),
                TotalRevenue = orders.Where(o => o.TotalAmount.HasValue).Sum(o => o.TotalAmount.Value)
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

        public async Task<WeeklySummaryDto> GetWeeklySummaryAsync(DateTime date)
        {
            // Determine week boundaries (Monday to Sunday)
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            var diff = (7 + (date.DayOfWeek - culture.DateTimeFormat.FirstDayOfWeek)) % 7;
            var weekStart = date.AddDays(-diff).Date;
            var weekEnd = weekStart.AddDays(7).AddSeconds(-1); // end of Sunday

            var orders = await _dbSet
                .Where(w => w.OrderDate >= weekStart && w.OrderDate <= weekEnd)
                .Include(w => w.Client)
                .ToListAsync();

            var summary = new WeeklySummaryDto
            {
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                TotalWorkOrders = orders.Count,
                CompletedOrders = orders.Count(o => o.Status == WorkStatus.Done),
                InProgressOrders = orders.Count(o => o.Status == WorkStatus.InProgress),
                TotalRevenue = orders.Where(o => o.TotalAmount.HasValue).Sum(o => o.TotalAmount.Value),
                ClientSummaries = orders
                    .GroupBy(o => o.Client.Name)
                    .Select(g => new ClientSummaryDto
                    {
                        ClientName = g.Key,
                        OrderCount = g.Count(),
                        TotalAmount = g.Where(o => o.TotalAmount.HasValue).Sum(o => o.TotalAmount.Value)
                    })
                    .ToList()
            };
            return summary;
        }
    }
}