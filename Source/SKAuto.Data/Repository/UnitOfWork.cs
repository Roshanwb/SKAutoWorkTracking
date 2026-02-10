using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;
using SKAuto.Data.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SKAuto.Core.Entities;

namespace SKAuto.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DatabaseContext _context;
        private IRepository<Client> _clients;
        private IRepository<Vehicle> _vehicles;
        private IRepository<Accessory> _accessories;
        private IRepository<WorkOrder> _workOrders;
        private IRepository<WorkTask> _workTasks;
        private IRepository<Travel> _travels;
        private IRepository<SourceDocument> _sourceDocuments;
        private IRepository<ProtectedRate> _protectedRates;

        public UnitOfWork(DatabaseContext context)
        {
            _context = context;
        }

        public IRepository<Client> Clients =>
            _clients ??= new ClientRepository(_context);

        public IRepository<Vehicle> Vehicles =>
            _vehicles ??= new VehicleRepository(_context);

        public IRepository<Accessory> Accessories =>
            _accessories ??= new BaseRepository<Accessory>(_context);

        public IRepository<WorkOrder> WorkOrders =>
            _workOrders ??= new WorkOrderRepository(_context);

        public IRepository<WorkTask> WorkTasks =>
            _workTasks ??= new BaseRepository<WorkTask>(_context);

        public IRepository<Travel> Travels =>
            _travels ??= new BaseRepository<Travel>(_context);

        public IRepository<SourceDocument> SourceDocuments =>
            _sourceDocuments ??= new BaseRepository<SourceDocument>(_context);

        public IRepository<ProtectedRate> ProtectedRates =>
            _protectedRates ??= new BaseRepository<ProtectedRate>(_context);

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task BeginTransactionAsync()
        {
            await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            await _context.Database.CommitTransactionAsync();
        }

        public async Task RollbackTransactionAsync()
        {
            await _context.Database.RollbackTransactionAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}