using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SKAuto.Core.Entities;

namespace SKAuto.Data.Database
{
    public class DatabaseContext : DbContext
    {
        public DbSet<Client> Clients { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<Accessory> Accessories { get; set; }
        public DbSet<ProtectedRate> ProtectedRates { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<WorkTask> WorkTasks { get; set; }
        public DbSet<Travel> Travels { get; set; }
        public DbSet<SourceDocument> SourceDocuments { get; set; }
        public DbSet<User> Users { get; set; }

        private readonly string _databasePath;

        public DatabaseContext()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var skautoPath = Path.Combine(appData, "SKAuto");
            Directory.CreateDirectory(skautoPath);
            _databasePath = Path.Combine(skautoPath, "SKAuto.db");
        }

        public DatabaseContext(string databasePath) => _databasePath = databasePath;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.EnableSensitiveDataLogging();   // <-- ADD THIS
                optionsBuilder.UseSqlite($"Data Source={_databasePath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Enums as strings
            modelBuilder.Entity<Client>()
                .Property(c => c.Type)
                .HasConversion<string>();

            modelBuilder.Entity<WorkOrder>()
                .Property(w => w.OrderType)
                .HasConversion<string>();

            modelBuilder.Entity<WorkOrder>()
                .Property(w => w.Status)
                .HasConversion<string>();

            modelBuilder.Entity<WorkTask>()
                .Property(t => t.TaskType)
                .HasConversion<string>();

            modelBuilder.Entity<WorkTask>()
                .Property(t => t.TaskStatus)
                .HasConversion<string>();

            // NEW: Convert Accessory.TaskType enum to string
            modelBuilder.Entity<Accessory>()
                .Property(a => a.TaskType)
                .HasConversion<string>();

            // Unique constraints
            modelBuilder.Entity<Client>()
                .HasIndex(c => c.Name)
                .IsUnique();

            modelBuilder.Entity<Vehicle>()
                .HasIndex(v => v.ChassisNumber)
                .IsUnique();

            modelBuilder.Entity<Accessory>()
                .HasIndex(a => a.Name)
                .IsUnique();

            modelBuilder.Entity<SourceDocument>()
                .HasIndex(s => s.FileHash)
                .IsUnique();

            // Client self‑reference (parent)
            modelBuilder.Entity<Client>()
                .HasOne(c => c.ParentClient)
                .WithMany(c => c.ChildClients)
                .HasForeignKey(c => c.ParentClientId)
                .OnDelete(DeleteBehavior.SetNull);

            // Vehicle → Client (one client, many vehicles)
            modelBuilder.Entity<Vehicle>()
                .HasOne(v => v.Client)
                .WithMany(c => c.Vehicles)
                .HasForeignKey(v => v.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // WorkOrder → Vehicle (each order belongs to one vehicle)
            modelBuilder.Entity<WorkOrder>()
                .HasOne(w => w.Vehicle)
                .WithMany(v => v.WorkOrders)
                .HasForeignKey(w => w.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            // WorkTask relationships (unchanged)
            modelBuilder.Entity<WorkTask>()
                .HasOne(t => t.WorkOrder)
                .WithMany(w => w.WorkTasks)
                .HasForeignKey(t => t.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WorkTask>()
                .HasOne(t => t.Accessory)
                .WithMany(a => a.WorkTasks)
                .HasForeignKey(t => t.AccessoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Travel (unchanged)
            modelBuilder.Entity<Travel>()
                .HasOne(t => t.WorkOrder)
                .WithMany(w => w.Travels)
                .HasForeignKey(t => t.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // SourceDocument (unchanged)
            modelBuilder.Entity<SourceDocument>()
                .HasOne(s => s.WorkOrder)
                .WithMany(w => w.SourceDocuments)
                .HasForeignKey(s => s.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // ProtectedRate (unchanged)
            modelBuilder.Entity<ProtectedRate>()
                .HasOne(p => p.Accessory)
                .WithMany(a => a.ProtectedRates)
                .HasForeignKey(p => p.AccessoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Default timestamps
            modelBuilder.Entity<Client>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Vehicle>()
                .Property(v => v.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // ... similar for other entities (if not already set)

            // Global query filters (optional)
            modelBuilder.Entity<Client>()
                .HasQueryFilter(c => c.IsActive);

            modelBuilder.Entity<Accessory>()
                .HasQueryFilter(a => a.IsActive);

            modelBuilder.Entity<Vehicle>()
                .HasQueryFilter(v => v.IsActive);
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseEntity &&
                           (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                if (entry.Entity is BaseEntity entity)
                {
                    if (entry.State == EntityState.Added)
                        entity.CreatedAt = DateTime.UtcNow;
                    else
                        entity.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}

