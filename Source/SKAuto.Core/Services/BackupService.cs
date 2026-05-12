using CsvHelper;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace SKAuto.Core.Services
{
    public class BackupService : IBackupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly string _dbPath;
        private readonly ILoggingService _logger;

        public BackupService(IUnitOfWork unitOfWork, ILoggingService logger, string dbPath)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _dbPath = dbPath;
        }

        // ========== BACKUP / EXPORT ==========

        public async Task<string> BackupDatabaseAsync(string backupFolder)
        {
            var fileName = $"SKAuto_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var destPath = Path.Combine(backupFolder, fileName);
            Directory.CreateDirectory(backupFolder);
            File.Copy(_dbPath, destPath, true);
            return destPath;
        }

        public async Task<string> ExportDataAsync(string exportFolder)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var tempDir = Path.Combine(Path.GetTempPath(), $"SKAuto_Export_{timestamp}");
            Directory.CreateDirectory(tempDir);

            await ExportTableToCsvAsync(await _unitOfWork.Clients.GetAllAsync(), tempDir, "Clients.csv");
            await ExportTableToCsvAsync(await _unitOfWork.Vehicles.GetAllAsync(), tempDir, "Vehicles.csv");
            await ExportTableToCsvAsync(await _unitOfWork.Accessories.GetAllAsync(), tempDir, "Accessories.csv");
            await ExportTableToCsvAsync(await _unitOfWork.WorkOrders.GetAllAsync(), tempDir, "WorkOrders.csv");
            await ExportTableToCsvAsync(await _unitOfWork.WorkTasks.GetAllAsync(), tempDir, "WorkTasks.csv");
            await ExportTableToCsvAsync(await _unitOfWork.Travels.GetAllAsync(), tempDir, "Travels.csv");
            await ExportTableToCsvAsync(await _unitOfWork.ProtectedRates.GetAllAsync(), tempDir, "ProtectedRates.csv");
            await ExportTableToCsvAsync(await _unitOfWork.Users.GetAllAsync(), tempDir, "Users.csv");
            await ExportTableToCsvAsync(await _unitOfWork.SourceDocuments.GetAllAsync(), tempDir, "SourceDocuments.csv");

            var zipPath = Path.Combine(exportFolder, $"SKAuto_Export_{timestamp}.zip");
            ZipFile.CreateFromDirectory(tempDir, zipPath);
            Directory.Delete(tempDir, true);
            return zipPath;
        }

        private async Task ExportTableToCsvAsync<T>(IEnumerable<T> records, string folder, string filename)
        {
            using var writer = new StreamWriter(Path.Combine(folder, filename), false, System.Text.Encoding.UTF8);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(records);
        }

        // ========== IMPORT MAIN ==========

        public async Task<BackupImportResult> ImportDataAsync(string zipPath, BackupImportOptions options)
        {
            var result = new BackupImportResult();
            var extractDir = Path.Combine(Path.GetTempPath(), $"SKAuto_Import_{Guid.NewGuid()}");
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Import in dependency order
                var clients = await ImportClientsAsync(extractDir, options, result);
                var accessories = await ImportAccessoriesAsync(extractDir, options, result);
                var vehicles = await ImportVehiclesAsync(extractDir, options, clients, result);
                var workOrders = await ImportWorkOrdersAsync(extractDir, options, vehicles, result);
                await ImportWorkTasksAsync(extractDir, options, workOrders, accessories, result);
                await ImportTravelsAsync(extractDir, options, workOrders, result);
                await ImportProtectedRatesAsync(extractDir, options, accessories, result);
                await ImportUsersAsync(extractDir, options, result);
                await ImportSourceDocumentsAsync(extractDir, options, workOrders, result);

                if (!options.DryRun)
                {
                    await _unitOfWork.CommitTransactionAsync();
                    _logger.LogInfo($"Import committed: {result.RowsInserted} inserted, {result.RowsUpdated} updated, {result.RowsSkipped} skipped.");
                }
                else
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    _logger.LogInfo($"Dry run completed: {result.RowsInserted} would be inserted, {result.RowsUpdated} updated, {result.RowsSkipped} skipped. Conflicts: {result.Conflicts.Count}");
                }
                result.Success = true;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError($"Import failed: {ex.Message}", ex);
            }
            finally
            {
                Directory.Delete(extractDir, true);
            }
            return result;
        }

        // ========== CLIENT IMPORT ==========

        private async Task<Dictionary<string, Client>> ImportClientsAsync(string folder, BackupImportOptions options, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Clients.csv");
            if (!File.Exists(filePath)) return new Dictionary<string, Client>();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<Client>().ToList();
            var imported = new Dictionary<string, Client>(StringComparer.OrdinalIgnoreCase);

            foreach (var rec in records)
            {
                var existing = (await _unitOfWork.Clients.FindAsync(c => c.Name == rec.Name)).FirstOrDefault();
                if (existing != null)
                {
                    result.Conflicts.Add(new Conflict { Table = "Clients", Key = rec.Name, ExistingValue = existing.Name, ImportedValue = rec.Name });
                    switch (options.ClientConflict)
                    {
                        case ConflictResolution.Overwrite:
                            existing.Address = rec.Address;
                            existing.Phone = rec.Phone;
                            existing.Email = rec.Email;
                            existing.Type = rec.Type;
                            existing.IsActive = rec.IsActive;
                            if (!options.DryRun) await _unitOfWork.Clients.UpdateAsync(existing);
                            result.RowsUpdated++;
                            imported[rec.Name] = existing;
                            break;
                        case ConflictResolution.Merge:
                            if (!string.IsNullOrWhiteSpace(rec.Address)) existing.Address = rec.Address;
                            if (!string.IsNullOrWhiteSpace(rec.Phone)) existing.Phone = rec.Phone;
                            if (!string.IsNullOrWhiteSpace(rec.Email)) existing.Email = rec.Email;
                            existing.Type = rec.Type;
                            existing.IsActive = rec.IsActive;
                            if (!options.DryRun) await _unitOfWork.Clients.UpdateAsync(existing);
                            result.RowsUpdated++;
                            imported[rec.Name] = existing;
                            break;
                        default:
                            result.RowsSkipped++;
                            imported[rec.Name] = existing;
                            break;
                    }
                }
                else
                {
                    if (!options.DryRun) await _unitOfWork.Clients.AddAsync(rec);
                    result.RowsInserted++;
                    imported[rec.Name] = rec;
                }
            }
            return imported;
        }

        // ========== VEHICLE IMPORT ==========

        private async Task<Dictionary<string, Vehicle>> ImportVehiclesAsync(string folder, BackupImportOptions options,
            Dictionary<string, Client> clients, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Vehicles.csv");
            if (!File.Exists(filePath)) return new Dictionary<string, Vehicle>();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<Vehicle>().ToList();
            var imported = new Dictionary<string, Vehicle>(StringComparer.OrdinalIgnoreCase);

            foreach (var rec in records)
            {
                // Resolve ClientId from client name (CSV contains Client.Name)
                if (rec.Client == null || string.IsNullOrWhiteSpace(rec.Client.Name))
                {
                    result.Conflicts.Add(new Conflict { Table = "Vehicles", Key = rec.ChassisNumber, ImportedValue = "Missing client name" });
                    result.RowsSkipped++;
                    continue;
                }
                if (!clients.TryGetValue(rec.Client.Name, out var client))
                {
                    result.Conflicts.Add(new Conflict { Table = "Vehicles", Key = rec.ChassisNumber, ImportedValue = $"Client '{rec.Client.Name}' not found" });
                    result.RowsSkipped++;
                    continue;
                }
                rec.ClientId = client.Id;
                rec.Client = null;  // avoid navigation property conflict

                var existing = (await _unitOfWork.Vehicles.FindAsync(v => v.ChassisNumber == rec.ChassisNumber)).FirstOrDefault();
                if (existing != null)
                {
                    result.Conflicts.Add(new Conflict { Table = "Vehicles", Key = rec.ChassisNumber, ExistingValue = existing.ChassisNumber, ImportedValue = rec.ChassisNumber });
                    switch (options.VehicleConflict)
                    {
                        case ConflictResolution.Overwrite:
                            existing.Model = rec.Model;
                            existing.Make = rec.Make;
                            existing.Year = rec.Year;
                            existing.Registration = rec.Registration;
                            existing.IsActive = rec.IsActive;
                            existing.ClientId = rec.ClientId;
                            if (!options.DryRun) await _unitOfWork.Vehicles.UpdateAsync(existing);
                            result.RowsUpdated++;
                            imported[rec.ChassisNumber] = existing;
                            break;
                        case ConflictResolution.Merge:
                            if (!string.IsNullOrWhiteSpace(rec.Model)) existing.Model = rec.Model;
                            if (!string.IsNullOrWhiteSpace(rec.Make)) existing.Make = rec.Make;
                            if (rec.Year.HasValue) existing.Year = rec.Year;
                            if (!string.IsNullOrWhiteSpace(rec.Registration)) existing.Registration = rec.Registration;
                            existing.IsActive = rec.IsActive;
                            existing.ClientId = rec.ClientId;
                            if (!options.DryRun) await _unitOfWork.Vehicles.UpdateAsync(existing);
                            result.RowsUpdated++;
                            imported[rec.ChassisNumber] = existing;
                            break;
                        default:
                            result.RowsSkipped++;
                            imported[rec.ChassisNumber] = existing;
                            break;
                    }
                }
                else
                {
                    // New vehicle
                    if (!options.DryRun) await _unitOfWork.Vehicles.AddAsync(rec);
                    result.RowsInserted++;
                    imported[rec.ChassisNumber] = rec;
                }
            }
            return imported;
        }

        // ========== ACCESSORY IMPORT ==========

        private async Task<Dictionary<string, Accessory>> ImportAccessoriesAsync(string folder, BackupImportOptions options, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Accessories.csv");
            if (!File.Exists(filePath)) return new Dictionary<string, Accessory>();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<Accessory>().ToList();
            var imported = new Dictionary<string, Accessory>(StringComparer.OrdinalIgnoreCase);

            foreach (var rec in records)
            {
                // Use Name as business key
                var existing = (await _unitOfWork.Accessories.FindAsync(a => a.Name == rec.Name)).FirstOrDefault();
                if (existing != null)
                {
                    result.Conflicts.Add(new Conflict { Table = "Accessories", Key = rec.Name, ExistingValue = existing.Name, ImportedValue = rec.Name });
                    switch (options.AccessoryConflict)
                    {
                        case ConflictResolution.Overwrite:
                            existing.PartNumber = rec.PartNumber;
                            existing.Description = rec.Description;
                            existing.Time = rec.Time;
                            existing.Price = rec.Price;
                            existing.RequiresPassword = rec.RequiresPassword;
                            existing.IsActive = rec.IsActive;
                            if (!options.DryRun) await _unitOfWork.Accessories.UpdateAsync(existing);
                            result.RowsUpdated++;
                            imported[rec.Name] = existing;
                            break;
                        case ConflictResolution.Merge:
                            if (!string.IsNullOrWhiteSpace(rec.PartNumber)) existing.PartNumber = rec.PartNumber;
                            if (!string.IsNullOrWhiteSpace(rec.Description)) existing.Description = rec.Description;
                            if (rec.Time.HasValue) existing.Time = rec.Time;
                            if (rec.Price.HasValue) existing.Price = rec.Price;
                            existing.RequiresPassword = rec.RequiresPassword;
                            existing.IsActive = rec.IsActive;
                            if (!options.DryRun) await _unitOfWork.Accessories.UpdateAsync(existing);
                            result.RowsUpdated++;
                            imported[rec.Name] = existing;
                            break;
                        default:
                            result.RowsSkipped++;
                            imported[rec.Name] = existing;
                            break;
                    }
                }
                else
                {
                    // New accessory – let DB assign Id
                    rec.Id = 0;
                    if (!options.DryRun) await _unitOfWork.Accessories.AddAsync(rec);
                    result.RowsInserted++;
                    imported[rec.Name] = rec;
                }
            }
            return imported;
        }

        // ========== WORK ORDER IMPORT ==========

        private async Task<Dictionary<int, WorkOrder>> ImportWorkOrdersAsync(string folder, BackupImportOptions options,
            Dictionary<string, Vehicle> vehicles, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "WorkOrders.csv");
            if (!File.Exists(filePath)) return new Dictionary<int, WorkOrder>();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<WorkOrder>().ToList();
            var imported = new Dictionary<int, WorkOrder>();

            foreach (var rec in records)
            {
                // Find vehicle by ChassisNumber (CSV contains Vehicle.ChassisNumber)
                if (rec.Vehicle == null || string.IsNullOrWhiteSpace(rec.Vehicle.ChassisNumber))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkOrders", Key = rec.Id.ToString(), ImportedValue = "Missing vehicle chassis" });
                    result.RowsSkipped++;
                    continue;
                }
                if (!vehicles.TryGetValue(rec.Vehicle.ChassisNumber, out var vehicle))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkOrders", Key = rec.Id.ToString(), ImportedValue = $"Vehicle '{rec.Vehicle.ChassisNumber}' not found" });
                    result.RowsSkipped++;
                    continue;
                }
                rec.VehicleId = vehicle.Id;
                rec.Vehicle = null;

                var existing = await _unitOfWork.WorkOrders.GetByIdAsync(rec.Id);
                if (existing != null)
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkOrders", Key = rec.Id.ToString(), ExistingValue = existing.Id.ToString(), ImportedValue = rec.Id.ToString() });
                    switch (options.WorkOrderConflict)
                    {
                        case ConflictResolution.Overwrite:
                            existing.OrderDate = rec.OrderDate;
                            existing.Status = rec.Status;
                            existing.OrderType = rec.OrderType;
                            existing.CompletedDate = rec.CompletedDate;
                            existing.Notes = rec.Notes;
                            existing.VehicleId = rec.VehicleId;
                            existing.TotalAmount = rec.TotalAmount;
                            if (!options.DryRun) await _unitOfWork.WorkOrders.UpdateAsync(existing);
                            result.RowsUpdated++;
                            imported[existing.Id] = existing;
                            break;
                        case ConflictResolution.Merge:
                            existing.OrderDate = rec.OrderDate;
                            existing.Status = rec.Status;
                            if (!string.IsNullOrWhiteSpace(rec.Notes)) existing.Notes = rec.Notes;
                            existing.VehicleId = rec.VehicleId;
                            if (rec.TotalAmount.HasValue) existing.TotalAmount = rec.TotalAmount;
                            if (!options.DryRun) await _unitOfWork.WorkOrders.UpdateAsync(existing);
                            result.RowsUpdated++;
                            imported[existing.Id] = existing;
                            break;
                        default:
                            result.RowsSkipped++;
                            imported[existing.Id] = existing;
                            break;
                    }
                }
                else
                {
                    // New work order – keep CSV Id (or let DB assign? Safer to let DB assign)
                    // But foreign keys from WorkTasks reference this Id. So we must preserve the original Id.
                    // We'll use the Id from CSV and ensure it doesn't conflict.
                    if (!options.DryRun) await _unitOfWork.WorkOrders.AddAsync(rec);
                    result.RowsInserted++;
                    imported[rec.Id] = rec;
                }
            }
            return imported;
        }

        // ========== WORK TASK IMPORT ==========

        private async Task ImportWorkTasksAsync(string folder, BackupImportOptions options,
            Dictionary<int, WorkOrder> workOrders, Dictionary<string, Accessory> accessories, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "WorkTasks.csv");
            if (!File.Exists(filePath)) return;

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<WorkTask>().ToList();

            foreach (var rec in records)
            {
                // Resolve WorkOrder (by WorkOrderId) or from CSV relation
                if (!workOrders.TryGetValue(rec.WorkOrderId, out var workOrder))
                {
                    // Maybe the CSV contains WorkOrder reference? We can also look up by WorkOrderId if it was imported.
                    result.Conflicts.Add(new Conflict { Table = "WorkTasks", Key = rec.Id.ToString(), ImportedValue = $"WorkOrder {rec.WorkOrderId} not found" });
                    result.RowsSkipped++;
                    continue;
                }
                rec.WorkOrderId = workOrder.Id;

                // Resolve Accessory by Name
                if (rec.Accessory == null || string.IsNullOrWhiteSpace(rec.Accessory.Name))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkTasks", Key = rec.Id.ToString(), ImportedValue = "Missing accessory name" });
                    result.RowsSkipped++;
                    continue;
                }
                if (!accessories.TryGetValue(rec.Accessory.Name, out var accessory))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkTasks", Key = rec.Id.ToString(), ImportedValue = $"Accessory '{rec.Accessory.Name}' not found" });
                    result.RowsSkipped++;
                    continue;
                }
                rec.AccessoryId = accessory.Id;
                rec.Accessory = null;

                var existing = await _unitOfWork.WorkTasks.GetByIdAsync(rec.Id);
                if (existing != null)
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkTasks", Key = rec.Id.ToString(), ExistingValue = existing.Id.ToString(), ImportedValue = rec.Id.ToString() });
                    switch (options.WorkTaskConflict)
                    {
                        case ConflictResolution.Overwrite:
                            existing.Quantity = rec.Quantity;
                            existing.TaskType = rec.TaskType;
                            existing.TaskStatus = rec.TaskStatus;
                            existing.Price = rec.Price;
                            existing.EstimatedMinutes = rec.EstimatedMinutes;
                            existing.ActualMinutes = rec.ActualMinutes;
                            existing.Notes = rec.Notes;
                            existing.WorkOrderId = rec.WorkOrderId;
                            existing.AccessoryId = rec.AccessoryId;
                            if (!options.DryRun) await _unitOfWork.WorkTasks.UpdateAsync(existing);
                            result.RowsUpdated++;
                            break;
                        case ConflictResolution.Merge:
                            if (rec.Quantity != 0) existing.Quantity = rec.Quantity;
                            if (rec.TaskType != default) existing.TaskType = rec.TaskType;
                            if (rec.TaskStatus != default) existing.TaskStatus = rec.TaskStatus;
                            if (rec.Price.HasValue) existing.Price = rec.Price;
                            if (rec.EstimatedMinutes.HasValue) existing.EstimatedMinutes = rec.EstimatedMinutes;
                            if (rec.ActualMinutes.HasValue) existing.ActualMinutes = rec.ActualMinutes;
                            if (!string.IsNullOrWhiteSpace(rec.Notes)) existing.Notes = rec.Notes;
                            existing.WorkOrderId = rec.WorkOrderId;
                            existing.AccessoryId = rec.AccessoryId;
                            if (!options.DryRun) await _unitOfWork.WorkTasks.UpdateAsync(existing);
                            result.RowsUpdated++;
                            break;
                        default:
                            result.RowsSkipped++;
                            break;
                    }
                }
                else
                {
                    // New task – ensure Id is 0 to let DB assign
                    rec.Id = 0;
                    if (!options.DryRun) await _unitOfWork.WorkTasks.AddAsync(rec);
                    result.RowsInserted++;
                }
            }
        }

        // ========== TRAVEL IMPORT ==========

        private async Task ImportTravelsAsync(string folder, BackupImportOptions options,
            Dictionary<int, WorkOrder> workOrders, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Travels.csv");
            if (!File.Exists(filePath)) return;

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<Travel>().ToList();

            foreach (var rec in records)
            {
                if (!workOrders.TryGetValue(rec.WorkOrderId, out var workOrder))
                {
                    result.Conflicts.Add(new Conflict { Table = "Travels", Key = rec.Id.ToString(), ImportedValue = $"WorkOrder {rec.WorkOrderId} not found" });
                    result.RowsSkipped++;
                    continue;
                }
                rec.WorkOrderId = workOrder.Id;

                var existing = await _unitOfWork.Travels.GetByIdAsync(rec.Id);
                if (existing != null)
                {
                    result.Conflicts.Add(new Conflict { Table = "Travels", Key = rec.Id.ToString(), ExistingValue = existing.Id.ToString(), ImportedValue = rec.Id.ToString() });
                    switch (options.TravelConflict)
                    {
                        case ConflictResolution.Overwrite:
                            existing.TravelDate = rec.TravelDate;
                            existing.Destination = rec.Destination;
                            existing.DistanceKm = rec.DistanceKm;
                            existing.TravelCost = rec.TravelCost;
                            existing.Notes = rec.Notes;
                            existing.WorkOrderId = rec.WorkOrderId;
                            if (!options.DryRun) await _unitOfWork.Travels.UpdateAsync(existing);
                            result.RowsUpdated++;
                            break;
                        case ConflictResolution.Merge:
                            if (rec.TravelDate != default) existing.TravelDate = rec.TravelDate;
                            if (!string.IsNullOrWhiteSpace(rec.Destination)) existing.Destination = rec.Destination;
                            if (rec.DistanceKm.HasValue) existing.DistanceKm = rec.DistanceKm;
                            if (rec.TravelCost.HasValue) existing.TravelCost = rec.TravelCost;
                            if (!string.IsNullOrWhiteSpace(rec.Notes)) existing.Notes = rec.Notes;
                            existing.WorkOrderId = rec.WorkOrderId;
                            if (!options.DryRun) await _unitOfWork.Travels.UpdateAsync(existing);
                            result.RowsUpdated++;
                            break;
                        default:
                            result.RowsSkipped++;
                            break;
                    }
                }
                else
                {
                    rec.Id = 0;
                    if (!options.DryRun) await _unitOfWork.Travels.AddAsync(rec);
                    result.RowsInserted++;
                }
            }
        }

        // ========== PROTECTED RATES IMPORT ==========

        private async Task ImportProtectedRatesAsync(string folder, BackupImportOptions options,
            Dictionary<string, Accessory> accessories, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "ProtectedRates.csv");
            if (!File.Exists(filePath)) return;

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<ProtectedRate>().ToList();

            foreach (var rec in records)
            {
                // Resolve Accessory by Name
                if (rec.Accessory == null || string.IsNullOrWhiteSpace(rec.Accessory.Name))
                {
                    result.Conflicts.Add(new Conflict { Table = "ProtectedRates", Key = rec.Id.ToString(), ImportedValue = "Missing accessory name" });
                    result.RowsSkipped++;
                    continue;
                }
                if (!accessories.TryGetValue(rec.Accessory.Name, out var accessory))
                {
                    result.Conflicts.Add(new Conflict { Table = "ProtectedRates", Key = rec.Id.ToString(), ImportedValue = $"Accessory '{rec.Accessory.Name}' not found" });
                    result.RowsSkipped++;
                    continue;
                }
                rec.AccessoryId = accessory.Id;
                rec.Accessory = null;

                // Check for existing by AccessoryId + ValidFrom/To? We'll use Id as key.
                var existing = await _unitOfWork.ProtectedRates.GetByIdAsync(rec.Id);
                if (existing != null)
                {
                    result.Conflicts.Add(new Conflict { Table = "ProtectedRates", Key = rec.Id.ToString(), ExistingValue = existing.Id.ToString(), ImportedValue = rec.Id.ToString() });
                    switch (options.ProtectedRateConflict)
                    {
                        case ConflictResolution.Overwrite:
                            existing.ValidFrom = rec.ValidFrom;
                            existing.ValidTo = rec.ValidTo;
                            existing.HourlyRate = rec.HourlyRate;
                            existing.Currency = rec.Currency;
                            existing.EncryptedRate = rec.EncryptedRate;
                            existing.AccessoryId = rec.AccessoryId;
                            if (!options.DryRun) await _unitOfWork.ProtectedRates.UpdateAsync(existing);
                            result.RowsUpdated++;
                            break;
                        case ConflictResolution.Merge:
                            if (rec.ValidFrom != default) existing.ValidFrom = rec.ValidFrom;
                            if (rec.ValidTo.HasValue) existing.ValidTo = rec.ValidTo;
                            if (rec.HourlyRate != default) existing.HourlyRate = rec.HourlyRate;
                            if (!string.IsNullOrWhiteSpace(rec.Currency)) existing.Currency = rec.Currency;
                            if (rec.EncryptedRate != null) existing.EncryptedRate = rec.EncryptedRate       ;
                            existing.AccessoryId = rec.AccessoryId;
                            if (!options.DryRun) await _unitOfWork.ProtectedRates.UpdateAsync(existing);
                            result.RowsUpdated++;
                            break;
                        default:
                            result.RowsSkipped++;
                            break;
                    }
                }
                else
                {
                    rec.Id = 0;
                    if (!options.DryRun) await _unitOfWork.ProtectedRates.AddAsync(rec);
                    result.RowsInserted++;
                }
            }
        }

        // ========== USER IMPORT ==========

        private async Task ImportUsersAsync(string folder, BackupImportOptions options, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Users.csv");
            if (!File.Exists(filePath)) return;

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<User>().ToList();

            foreach (var rec in records)
            {
                var existing = (await _unitOfWork.Users.FindAsync(u => u.Username == rec.Username)).FirstOrDefault();
                if (existing != null)
                {
                    result.Conflicts.Add(new Conflict { Table = "Users", Key = rec.Username, ExistingValue = existing.Username, ImportedValue = rec.Username });
                    switch (options.UserConflict)
                    {
                        case ConflictResolution.Overwrite:
                            existing.PasswordHash = rec.PasswordHash;
                            existing.Role = rec.Role;
                            existing.IsActive = rec.IsActive;
                            if (!options.DryRun) await _unitOfWork.Users.UpdateAsync(existing);
                            result.RowsUpdated++;
                            break;
                        case ConflictResolution.Merge:
                            if (!string.IsNullOrWhiteSpace(rec.PasswordHash)) existing.PasswordHash = rec.PasswordHash;
                            existing.Role = rec.Role;
                            existing.IsActive = rec.IsActive;
                            if (!options.DryRun) await _unitOfWork.Users.UpdateAsync(existing);
                            result.RowsUpdated++;
                            break;
                        default:
                            result.RowsSkipped++;
                            break;
                    }
                }
                else
                {
                    rec.Id = 0;
                    if (!options.DryRun) await _unitOfWork.Users.AddAsync(rec);
                    result.RowsInserted++;
                }
            }
        }

        // ========== SOURCE DOCUMENT IMPORT ==========

        private async Task ImportSourceDocumentsAsync(string folder, BackupImportOptions options,
            Dictionary<int, WorkOrder> workOrders, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "SourceDocuments.csv");
            if (!File.Exists(filePath)) return;

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<SourceDocument>().ToList();

            foreach (var rec in records)
            {
                if (!workOrders.TryGetValue(rec.WorkOrderId, out var workOrder))
                {
                    result.Conflicts.Add(new Conflict { Table = "SourceDocuments", Key = rec.Id.ToString(), ImportedValue = $"WorkOrder {rec.WorkOrderId} not found" });
                    result.RowsSkipped++;
                    continue;
                }
                rec.WorkOrderId = workOrder.Id;

                // Use FileHash as unique key (or Id)
                var existing = (await _unitOfWork.SourceDocuments.FindAsync(s => s.FileHash == rec.FileHash)).FirstOrDefault();
                if (existing != null)
                {
                    result.Conflicts.Add(new Conflict { Table = "SourceDocuments", Key = rec.FileHash, ExistingValue = existing.FileHash, ImportedValue = rec.FileHash });
                    switch (options.SourceDocumentConflict)
                    {
                        case ConflictResolution.Overwrite:
                            existing.DocumentType = rec.DocumentType;
                            existing.FilePath = rec.FilePath;
                            existing.OriginalFilename = rec.OriginalFilename;
                            existing.CreatedAt = rec.CreatedAt;
                            existing.WorkOrderId = rec.WorkOrderId;
                            if (!options.DryRun) await _unitOfWork.SourceDocuments.UpdateAsync(existing);
                            result.RowsUpdated++;
                            break;
                        case ConflictResolution.Merge:
                            if (!string.IsNullOrWhiteSpace(rec.DocumentType)) existing.DocumentType = rec.DocumentType;
                            if (!string.IsNullOrWhiteSpace(rec.FilePath)) existing.FilePath = rec.FilePath;
                            if (!string.IsNullOrWhiteSpace(rec.OriginalFilename)) existing.OriginalFilename = rec.OriginalFilename;
                            if (rec.CreatedAt != default) existing.CreatedAt = rec.CreatedAt;
                            existing.WorkOrderId = rec.WorkOrderId;
                            if (!options.DryRun) await _unitOfWork.SourceDocuments.UpdateAsync(existing);
                            result.RowsUpdated++;
                            break;
                        default:
                            result.RowsSkipped++;
                            break;
                    }
                }
                else
                {
                    rec.Id = 0;
                    if (!options.DryRun) await _unitOfWork.SourceDocuments.AddAsync(rec);
                    result.RowsInserted++;
                }
            }
        }
    }
}