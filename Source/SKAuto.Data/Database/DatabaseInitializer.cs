using Microsoft.EntityFrameworkCore;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SKAuto.Data.Database
{
    public class DatabaseInitializer
    {
        private readonly DatabaseContext _context;

        public DatabaseInitializer(DatabaseContext context) => _context = context;

        public async Task InitializeAsync()
        {
            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Seed test data if tables are empty
            await SeedTestDataAsync();
        }

        private async Task SeedTestDataAsync()
        {
            // Seed Users if empty
            if (!await _context.Users.AnyAsync())
            {
                var users = new[]
                {
            new User
            {
                Username = "admin",
                PasswordHash = "admin",  // In production, hash this!
                Role = "Admin",
                IsActive = true
            },
            new User
            {
                Username = "user",
                PasswordHash = "user",
                Role = "User",
                IsActive = true
            }
        };
                await _context.Users.AddRangeAsync(users);
                await _context.SaveChangesAsync();
            }

            // Seed Clients if empty
            if (!await _context.Clients.AnyAsync())
            {
                var testClients = new[]
                {
            new Client { Name = "PSA Group", Type = ClientType.PSA },
            new Client { Name = "Veolia", Type = ClientType.PSA },
            new Client { Name = "SUEZ", Type = ClientType.PSA },
            new Client { Name = "EDF", Type = ClientType.PSA },
            new Client { Name = "ENGIE", Type = ClientType.PSA },
            new Client { Name = "Direct Client (Example)", Type = ClientType.Direct }
        };
                await _context.Clients.AddRangeAsync(testClients);
                await _context.SaveChangesAsync();
            }

            // Seed Accessories if empty
            if (!await _context.Accessories.AnyAsync())
            {
                var testAccessories = new[]
                {
            new Accessory { Name = "Logo", Price = 50, Time = 90 },
            new Accessory { Name = "Barre de toit", Price = 40, Time = 40 },
            new Accessory { Name = "Attelage", Price = 60, Time = 60 },
            new Accessory { Name = "Crochet", Price = 60, Time = 60 },
            new Accessory { Name = "Boitier", Price = 60, Time = 60 },
            new Accessory { Name = "Controle", Price = 0, Time = 10 },
            new Accessory { Name = "Grille", Price = 90, Time = 90 },
            new Accessory { Name = "Housse", Price = 30, Time = 30 },
            new Accessory { Name = "Balisage", Price = 30, Time = 30 },
            new Accessory { Name = "Antivol", Price = 60, Time = 60 },
            new Accessory { Name = "Alarm", Price = 50, Time = 90 },
            new Accessory { Name = "Pose Camera SK", Price = 0, Time = 60 },
            new Accessory { Name = "Pose Ecran SK", Price = 0, Time = 60 },
        };
                await _context.Accessories.AddRangeAsync(testAccessories);
                await _context.SaveChangesAsync();
            }

            // Seed Vehicles if empty – MUST set ClientId to an existing client
            if (!await _context.Vehicles.AnyAsync())
            {
                // Get a valid client ID (e.g., first PSA client)
                var psaClient = await _context.Clients.FirstOrDefaultAsync(c => c.Type == ClientType.PSA);
                int clientId = psaClient?.Id ?? 0;
                if (clientId == 0)
                {
                    // Fallback: create a default client if none exists
                    var defaultClient = new Client { Name = "Default", Type = ClientType.Direct };
                    _context.Clients.Add(defaultClient);
                    await _context.SaveChangesAsync();
                    clientId = defaultClient.Id;
                }

                var testVehicles = new[]
                {
            new Vehicle { ChassisNumber = "VF3XXXXXXXXXXXXXX", Make = "Citroën", Model = "Berlingo", ClientId = clientId },
            new Vehicle { ChassisNumber = "VF7YYYYYYYYYYYYYY", Make = "Peugeot", Model = "208", ClientId = clientId },
            new Vehicle { ChassisNumber = "VF1ZZZZZZZZZZZZZZ", Make = "Citroën", Model = "Jumpy", ClientId = clientId }
        };
                await _context.Vehicles.AddRangeAsync(testVehicles);
                await _context.SaveChangesAsync();
            }
        }

        public async Task BackupAsync(string backupFolder)
        {
            var source = _context.Database.GetDbConnection().DataSource;
            var fileName = $"SKAuto_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var dest = Path.Combine(backupFolder, fileName);
            Directory.CreateDirectory(backupFolder);
            File.Copy(source, dest, true);
            await Task.CompletedTask;
        }

        public async Task<bool> RestoreAsync(string backupFilePath)
        {
            if (!File.Exists(backupFilePath)) return false;
            var currentDb = _context.Database.GetDbConnection().DataSource;
            File.Copy(backupFilePath, currentDb, true);
            return true;
        }
    }
}