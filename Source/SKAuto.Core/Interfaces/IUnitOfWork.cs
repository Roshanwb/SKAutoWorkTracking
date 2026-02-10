using SKAuto.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}