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

        public async Task<BackupImportResult> ImportDataAsync(string zipPath, BackupImportOptions options)
        {
            var result = new BackupImportResult();
            var extractDir = Path.Combine(Path.GetTempPath(), $"SKAuto_Import_{Guid.NewGuid()}");
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var clients = await ImportClientsAsync(extractDir, options, result);
                var vehicles = await ImportVehiclesAsync(extractDir, options, clients, result);
                var accessories = await ImportAccessoriesAsync(extractDir, options, result);
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

        // ---- Helper import methods (using BackupImportResult) ----

        private async Task<Dictionary<string, Client>> ImportClientsAsync(string folder, BackupImportOptions options, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Clients.csv");
            if (!File.Exists(filePath)) return new Dictionary<string, Client>();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<Client>().ToList();
            var imported = new Dictionary<string, Client>();

            foreach (var rec in records)
            {
                var existing = (await _unitOfWork.Clients.FindAsync(c => c.Name == rec.Name)).ToList();
                if (existing.Any())
                {
                    var conflict = new Conflict
                    {
                        Table = "Clients",
                        Key = rec.Name,
                        ExistingValue = existing.First().Name,
                        ImportedValue = rec.Name
                    };
                    result.Conflicts.Add(conflict);
                    switch (options.ClientConflict)
                    {
                        case ConflictResolution.Skip:
                            result.RowsSkipped++;
                            break;
                        case ConflictResolution.Overwrite:
                            var target = existing.First();
                            target.Address = rec.Address;
                            target.Phone = rec.Phone;
                            target.Email = rec.Email;
                            target.Type = rec.Type;
                            target.IsActive = rec.IsActive;
                            if (!options.DryRun) await _unitOfWork.Clients.UpdateAsync(target);
                            result.RowsUpdated++;
                            imported[rec.Name] = target;
                            break;
                        case ConflictResolution.Merge:
                            var mergeTarget = existing.First();
                            if (rec.Address != null) mergeTarget.Address = rec.Address;
                            if (rec.Phone != null) mergeTarget.Phone = rec.Phone;
                            if (rec.Email != null) mergeTarget.Email = rec.Email;
                            if (!options.DryRun) await _unitOfWork.Clients.UpdateAsync(mergeTarget);
                            result.RowsUpdated++;
                            imported[rec.Name] = mergeTarget;
                            break;
                        case ConflictResolution.Prompt:
                            // handled later
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

        private async Task<Dictionary<string, Vehicle>> ImportVehiclesAsync(string folder, BackupImportOptions options,
            Dictionary<string, Client> clients, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Vehicles.csv");
            if (!File.Exists(filePath)) return new Dictionary<string, Vehicle>();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<Vehicle>().ToList();
            var imported = new Dictionary<string, Vehicle>();

            foreach (var rec in records)
            {
                if (rec.Client == null || string.IsNullOrEmpty(rec.Client.Name))
                {
                    result.Conflicts.Add(new Conflict
                    {
                        Table = "Vehicles",
                        Key = rec.ChassisNumber,
                        ExistingValue = "",
                        ImportedValue = "Missing client name"
                    });
                    result.RowsSkipped++;
                    continue;
                }
                if (!clients.TryGetValue(rec.Client.Name, out var client))
                {
                    result.Conflicts.Add(new Conflict
                    {
                        Table = "Vehicles",
                        Key = rec.ChassisNumber,
                        ExistingValue = "",
                        ImportedValue = $"Client '{rec.Client.Name}' not found"
                    });
                    result.RowsSkipped++;
                    continue;
                }
                rec.ClientId = client.Id;
                rec.Client = null;

                var existing = (await _unitOfWork.Vehicles.FindAsync(v => v.ChassisNumber == rec.ChassisNumber)).ToList();
                if (existing.Any())
                {
                    var conflict = new Conflict
                    {
                        Table = "Vehicles",
                        Key = rec.ChassisNumber,
                        ExistingValue = existing.First().ChassisNumber,
                        ImportedValue = rec.ChassisNumber
                    };
                    result.Conflicts.Add(conflict);
                    switch (options.VehicleConflict)
                    {
                        case ConflictResolution.Skip:
                            result.RowsSkipped++;
                            break;
                        case ConflictResolution.Overwrite:
                            var target = existing.First();
                            target.Model = rec.Model;
                            target.Make = rec.Make;
                            target.Year = rec.Year;
                            target.Registration = rec.Registration;
                            target.IsActive = rec.IsActive;
                            target.ClientId = rec.ClientId;
                            if (!options.DryRun) await _unitOfWork.Vehicles.UpdateAsync(target);
                            result.RowsUpdated++;
                            imported[rec.ChassisNumber] = target;
                            break;
                        case ConflictResolution.Merge:
                            var mergeTarget = existing.First();
                            if (rec.Model != null) mergeTarget.Model = rec.Model;
                            if (rec.Make != null) mergeTarget.Make = rec.Make;
                            if (rec.Year.HasValue) mergeTarget.Year = rec.Year;
                            if (rec.Registration != null) mergeTarget.Registration = rec.Registration;
                            mergeTarget.ClientId = rec.ClientId;
                            if (!options.DryRun) await _unitOfWork.Vehicles.UpdateAsync(mergeTarget);
                            result.RowsUpdated++;
                            imported[rec.ChassisNumber] = mergeTarget;
                            break;
                        case ConflictResolution.Prompt:
                            break;
                    }
                }
                else
                {
                    if (!options.DryRun) await _unitOfWork.Vehicles.AddAsync(rec);
                    result.RowsInserted++;
                    imported[rec.ChassisNumber] = rec;
                }
            }
            return imported;
        }

        // Other import methods (stubs – you must implement them similarly)
        private async Task<Dictionary<string, Accessory>> ImportAccessoriesAsync(string folder, BackupImportOptions options, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Accessories.csv");
            if (!File.Exists(filePath)) return new Dictionary<string, Accessory>();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            // Read all records from CSV
            var records = csv.GetRecords<Accessory>().ToList();
            var imported = new Dictionary<string, Accessory>(StringComparer.OrdinalIgnoreCase);

            foreach (var csvRecord in records)
            {
                // Use the Name as the business key (assuming it's unique)
                var existing = (await _unitOfWork.Accessories.FindAsync(a => a.Name == csvRecord.Name)).FirstOrDefault();

                if (existing != null)
                {
                    // Conflict exists
                    result.Conflicts.Add(new Conflict
                    {
                        Table = "Accessories",
                        Key = csvRecord.Name,
                        ExistingValue = existing.Name,
                        ImportedValue = csvRecord.Name
                    });

                    switch (options.AccessoryConflict)
                    {
                        case ConflictResolution.Overwrite:
                            // Overwrite all updatable fields from CSV
                            existing.PartNumber = csvRecord.PartNumber;
                            existing.Description = csvRecord.Description;
                            existing.Time = csvRecord.Time;
                            existing.Price = csvRecord.Price;
                            existing.RequiresPassword = csvRecord.RequiresPassword;
                            existing.IsActive = csvRecord.IsActive;
                            // Do NOT overwrite Id, CreatedAt, UpdatedAt – keep database values
                            if (!options.DryRun)
                                await _unitOfWork.Accessories.UpdateAsync(existing);
                            result.RowsUpdated++;
                            imported[csvRecord.Name] = existing;
                            break;

                        case ConflictResolution.Merge:
                            // Only overwrite fields that are non‑null/not default in CSV
                            if (!string.IsNullOrWhiteSpace(csvRecord.PartNumber))
                                existing.PartNumber = csvRecord.PartNumber;
                            if (!string.IsNullOrWhiteSpace(csvRecord.Description))
                                existing.Description = csvRecord.Description;
                            if (csvRecord.Time.HasValue)
                                existing.Time = csvRecord.Time;
                            if (csvRecord.Price.HasValue)
                                existing.Price = csvRecord.Price;
                            existing.RequiresPassword = csvRecord.RequiresPassword;
                            existing.IsActive = csvRecord.IsActive;
                            if (!options.DryRun)
                                await _unitOfWork.Accessories.UpdateAsync(existing);
                            result.RowsUpdated++;
                            imported[csvRecord.Name] = existing;
                            break;

                        case ConflictResolution.Skip:
                            result.RowsSkipped++;
                            imported[csvRecord.Name] = existing;
                            break;

                        case ConflictResolution.Prompt:
                            // Not implemented – fallback to Skip
                            result.RowsSkipped++;
                            imported[csvRecord.Name] = existing;
                            break;
                    }
                }
                else
                {
                    // No conflict – add new accessory
                    // IMPORTANT: Do NOT set Id from CSV; let DB generate new Id.
                    // Clear the Id to avoid duplicate key errors.
                    csvRecord.Id = 0;
                    if (!options.DryRun)
                        await _unitOfWork.Accessories.AddAsync(csvRecord);
                    result.RowsInserted++;
                    imported[csvRecord.Name] = csvRecord;
                }
            }

            return imported;
        }

        private async Task<Dictionary<int, WorkOrder>> ImportWorkOrdersAsync(string folder, BackupImportOptions options,
            Dictionary<string, Vehicle> vehicles, BackupImportResult result)
        {
            // TODO: Implement
            return new Dictionary<int, WorkOrder>();
        }

        private async Task ImportWorkTasksAsync(string folder, BackupImportOptions options,
            Dictionary<int, WorkOrder> workOrders, Dictionary<string, Accessory> accessories, BackupImportResult result)
        {
            await Task.CompletedTask;
        }

        private async Task ImportTravelsAsync(string folder, BackupImportOptions options,
            Dictionary<int, WorkOrder> workOrders, BackupImportResult result)
        {
            await Task.CompletedTask;
        }

        private async Task ImportProtectedRatesAsync(string folder, BackupImportOptions options,
            Dictionary<string, Accessory> accessories, BackupImportResult result)
        {
            await Task.CompletedTask;
        }

        private async Task ImportUsersAsync(string folder, BackupImportOptions options, BackupImportResult result)
        {
            await Task.CompletedTask;
        }

        private async Task ImportSourceDocumentsAsync(string folder, BackupImportOptions options,
            Dictionary<int, WorkOrder> workOrders, BackupImportResult result)
        {
            await Task.CompletedTask;
        }
    }
}