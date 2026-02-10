using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Microsoft.EntityFrameworkCore;
using SKAuto.Core.Entities;

namespace SKAuto.Data.Database
{
    public class DatabaseInitializer
    {
        private readonly DatabaseContext _context;

        public DatabaseInitializer(DatabaseContext context)
        {
            _context = context;
        }

        public async Task InitializeAsync()
        {
            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Run migrations if any
            await _context.Database.MigrateAsync();

            // Seed initial data if database is empty
            await SeedInitialDataAsync();
        }

        private async Task SeedInitialDataAsync()
        {
            if (!await _context.Clients.AnyAsync())
            {
                var defaultClients = new List<Client>
                {
                    new Client { Name = "PSA Group", Type = Core.Enums.ClientType.PSA },
                    new Client { Name = "EDF", Type = Core.Enums.ClientType.Direct },
                    new Client { Name = "Veolia", Type = Core.Enums.ClientType.Direct },
                    new Client { Name = "ProxiServe", Type = Core.Enums.ClientType.Direct }
                };

                await _context.Clients.AddRangeAsync(defaultClients);
            }

            if (!await _context.Accessories.AnyAsync())
            {
                var defaultAccessories = new List<Accessory>
                {
                    new Accessory { Name = "Roof Rack", PartNumber = "RR-001", StandardFittingTime = 60 },
                    new Accessory { Name = "Tow Bar", PartNumber = "TB-001", StandardFittingTime = 90 },
                    new Accessory { Name = "Alloy Wheels", PartNumber = "AW-001", StandardFittingTime = 120 },
                    new Accessory { Name = "Parking Sensors", PartNumber = "PS-001", StandardFittingTime = 180 },
                    new Accessory { Name = "Car Mats", PartNumber = "CM-001", StandardFittingTime = 15 }
                };

                await _context.Accessories.AddRangeAsync(defaultAccessories);
            }

            await _context.SaveChangesAsync();
        }

        public async Task BackupDatabaseAsync(string backupPath)
        {
            if (File.Exists(_context.Database.GetDbConnection().DataSource))
            {
                var sourceFile = _context.Database.GetDbConnection().DataSource;
                var backupFile = Path.Combine(backupPath, $"SKAuto_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                File.Copy(sourceFile, backupFile, true);
            }
        }

        public async Task<bool> ValidateDatabaseAsync()
        {
            try
            {
                // Test connection
                await _context.Database.ExecuteSqlRawAsync("SELECT 1");

                // Check if all tables exist
                var tables = new[] { "Clients", "Vehicles", "Accessories", "WorkOrders", "WorkTasks", "Travels" };
                foreach (var table in tables)
                {
                    await _context.Database.ExecuteSqlRawAsync($"SELECT COUNT(*) FROM {table}");
                }

                return true;
            }
            catch (Exception ex)
            {
                // Log error
                return false;
            }
        }
    }
}