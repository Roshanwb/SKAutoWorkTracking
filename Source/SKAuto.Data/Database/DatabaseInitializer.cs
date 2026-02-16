using Microsoft.EntityFrameworkCore;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
                    new Accessory { Name = "Logo", SellingPrice = 50, StandardFittingTime = 90 },
                    new Accessory { Name = "Barre de toit", SellingPrice = 40, StandardFittingTime = 40 },
                    new Accessory { Name = "Attelage", SellingPrice = 60, StandardFittingTime = 60 },
                    new Accessory { Name = "Crochet", SellingPrice = 60, StandardFittingTime = 60 },
                    new Accessory { Name = "Boitier", SellingPrice = 60, StandardFittingTime = 60 },
                    new Accessory { Name = "Controle", SellingPrice = 0, StandardFittingTime = 10 },
                    new Accessory { Name = "Grille", SellingPrice = 90, StandardFittingTime = 90 },
                    new Accessory { Name = "Housse", SellingPrice = 30, StandardFittingTime = 30 },
                    new Accessory { Name = "Balisage", SellingPrice = 30, StandardFittingTime = 30 },
                    new Accessory { Name = "Antivol", SellingPrice = 60, StandardFittingTime = 60 },
                    new Accessory { Name = "Alarm", SellingPrice = 50, StandardFittingTime = 90 },
                    new Accessory { Name = "Pose Camera SK", SellingPrice = 0, StandardFittingTime = 60 },
                    new Accessory { Name = "Pose Ecran SK", SellingPrice = 0, StandardFittingTime = 60 }
                };
                await _context.Accessories.AddRangeAsync(testAccessories);
                await _context.SaveChangesAsync();
            }

            // Seed Vehicles if empty
            if (!await _context.Vehicles.AnyAsync())
            {
                var testVehicles = new[]
                {
                    new Vehicle { ChassisNumber = "VF3XXXXXXXXXXXXXX", Make = "Citroën", Model = "Berlingo" },
                    new Vehicle { ChassisNumber = "VF7YYYYYYYYYYYYYY", Make = "Peugeot", Model = "208" },
                    new Vehicle { ChassisNumber = "VF1ZZZZZZZZZZZZZZ", Make = "Citroën", Model = "Jumpy" }
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