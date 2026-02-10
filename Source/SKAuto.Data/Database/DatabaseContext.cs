using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<WorkTask> WorkTasks { get; set; }
        public DbSet<Travel> Travels { get; set; }
        public DbSet<SourceDocument> SourceDocuments { get; set; }
        public DbSet<ProtectedRate> ProtectedRates { get; set; }

        private readonly string _databasePath;

        public DatabaseContext()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var skautoPath = Path.Combine(appData, "SKAuto");
            Directory.CreateDirectory(skautoPath);
            _databasePath = Path.Combine(skautoPath, "SKAuto.db");
        }

        public DatabaseContext(string databasePath)
        {
            _databasePath = databasePath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite($"Data Source={_databasePath}");
                optionsBuilder.EnableSensitiveDataLogging(false);
                optionsBuilder.LogTo(message => System.Diagnostics.Debug.WriteLine(message));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure enums as strings
            modelBuilder.Entity<Client>()
                .Property(c => c.Type)
                .HasConversion(new EnumToStringConverter<Core.Enums.ClientType>());

            modelBuilder.Entity<WorkOrder>()
                .Property(w => w.OrderType)
                .HasConversion(new EnumToStringConverter<Core.Enums.OrderType>());

            modelBuilder.Entity<WorkOrder>()
                .Property(w => w.Status)
                .HasConversion(new EnumToStringConverter<Core.Enums.WorkStatus>());

            modelBuilder.Entity<WorkTask>()
                .Property(t => t.TaskType)
                .HasConversion(new EnumToStringConverter<Core.Enums.TaskType>());

            modelBuilder.Entity<WorkTask>()
                .Property(t => t.TaskStatus)
                .HasConversion(new EnumToStringConverter<Core.Enums.WorkStatus>());

            // Configure unique constraints
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

            // Configure relationships
            modelBuilder.Entity<WorkOrder>()
                .HasOne(w => w.Client)
                .WithMany(c => c.WorkOrders)
                .HasForeignKey(w => w.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkOrder>()
                .HasOne(w => w.Vehicle)
                .WithMany(v => v.WorkOrders)
                .HasForeignKey(w => w.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

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

            modelBuilder.Entity<Travel>()
                .HasOne(t => t.WorkOrder)
                .WithMany(w => w.Travels)
                .HasForeignKey(t => t.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SourceDocument>()
                .HasOne(s => s.WorkOrder)
                .WithMany(w => w.SourceDocuments)
                .HasForeignKey(s => s.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProtectedRate>()
                .HasOne(p => p.Accessory)
                .WithMany(a => a.ProtectedRates)
                .HasForeignKey(p => p.AccessoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure check constraints (SQLite doesn't support CHECK directly)
            // We'll enforce these in code

            // Configure default values
            modelBuilder.Entity<WorkOrder>()
                .Property(w => w.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<WorkOrder>()
                .Property(w => w.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Configure computed columns (if needed, SQLite doesn't support computed columns)
            // We'll handle in code

            // Configure indices for performance
            modelBuilder.Entity<WorkOrder>()
                .HasIndex(w => w.OrderDate);

            modelBuilder.Entity<WorkOrder>()
                .HasIndex(w => w.Status);

            modelBuilder.Entity<WorkOrder>()
                .HasIndex(w => new { w.ClientId, w.OrderDate });

            modelBuilder.Entity<Vehicle>()
                .HasIndex(v => v.Model);

            modelBuilder.Entity<WorkTask>()
                .HasIndex(t => t.TaskStatus);
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
                    {
                        entity.CreatedAt = DateTime.UtcNow;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        entity.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
        }
    }
}