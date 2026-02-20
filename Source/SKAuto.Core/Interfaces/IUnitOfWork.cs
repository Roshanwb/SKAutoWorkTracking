using SKAuto.Core.Entities;

namespace SKAuto.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Client> Clients { get; }
        IRepository<Vehicle> Vehicles { get; }
        IRepository<Accessory> Accessories { get; }
        IRepository<WorkOrder> WorkOrders { get; }
        IRepository<WorkTask> WorkTasks { get; }
        IRepository<Travel> Travels { get; }
        IRepository<SourceDocument> SourceDocuments { get; }
        IRepository<ProtectedRate> ProtectedRates { get; }
        IRepository<User> Users { get; }  

        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}