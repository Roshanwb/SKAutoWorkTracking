using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;
using SKAuto.Data.Repository;

namespace SKAuto.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DatabaseContext _context;

        // Private repository instances
        private ClientRepository _clientRepository;
        private VehicleRepository _vehicleRepository;
        private AccessoryRepository _accessoryRepository;
        private WorkOrderRepository _workOrderRepository;
        private WorkTaskRepository _workTaskRepository;
        private TravelRepository _travelRepository;
        private SourceDocumentRepository _sourceDocumentRepository;
        private ProtectedRateRepository _protectedRateRepository;
        private UserRepository _userRepository;  // ADD THIS

        public UnitOfWork(DatabaseContext context)
        {
            _context = context;
        }

        public IRepository<Client> Clients =>
            _clientRepository ??= new ClientRepository(_context);

        public IRepository<Vehicle> Vehicles =>
            _vehicleRepository ??= new VehicleRepository(_context);

        public IRepository<Accessory> Accessories =>
            _accessoryRepository ??= new AccessoryRepository(_context);

        public IRepository<WorkOrder> WorkOrders =>
            _workOrderRepository ??= new WorkOrderRepository(_context);

        public IRepository<WorkTask> WorkTasks =>
            _workTaskRepository ??= new WorkTaskRepository(_context);

        public IRepository<Travel> Travels =>
            _travelRepository ??= new TravelRepository(_context);

        public IRepository<SourceDocument> SourceDocuments =>
            _sourceDocumentRepository ??= new SourceDocumentRepository(_context);

        public IRepository<ProtectedRate> ProtectedRates =>
            _protectedRateRepository ??= new ProtectedRateRepository(_context);

        // ADD THIS PROPERTY
        public IRepository<User> Users =>
            _userRepository ??= new UserRepository(_context);

        // Transaction methods
        public async Task<int> CompleteAsync() => await _context.SaveChangesAsync();
        public async Task BeginTransactionAsync() => await _context.Database.BeginTransactionAsync();
        public async Task CommitTransactionAsync() => await _context.Database.CommitTransactionAsync();
        public async Task RollbackTransactionAsync() => await _context.Database.RollbackTransactionAsync();
        public void Dispose() => _context.Dispose();
    }
}