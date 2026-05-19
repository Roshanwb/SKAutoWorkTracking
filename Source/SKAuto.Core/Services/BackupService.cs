using CsvHelper;
using CsvHelper.Configuration;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
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

        // ---------- Backup Database ----------
        public async Task<string> BackupDatabaseAsync(string backupFolder)
        {
            var fileName = $"SKAuto_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var destPath = Path.Combine(backupFolder, fileName);
            Directory.CreateDirectory(backupFolder);
            File.Copy(_dbPath, destPath, true);
            await Task.CompletedTask;
            return destPath;
        }

        // ---------- Export ----------
        public async Task<string> ExportDataAsync(string exportFolder)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var tempDir = Path.Combine(Path.GetTempPath(), $"SKAuto_Export_{timestamp}");
            Directory.CreateDirectory(tempDir);

            await ExportTableToCsvAsync(await _unitOfWork.Clients.GetAllAsync(), tempDir, "Clients.csv");
            await ExportTableToCsvAsync(await _unitOfWork.Accessories.GetAllAsync(), tempDir, "Accessories.csv");
            await ExportTableToCsvAsync(await _unitOfWork.Vehicles.GetAllAsync(), tempDir, "Vehicles.csv");
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

        // ---------- Main Import ----------
        public async Task<BackupImportResult> ImportDataAsync(string zipPath, BackupImportOptions options)
        {
            var result = new BackupImportResult();
            var extractDir = Path.Combine(Path.GetTempPath(), $"SKAuto_Import_{Guid.NewGuid()}");
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Clients (natural key: Name)
                var clients = await ImportClientsAsync(extractDir, options, result);

                // 2. Accessories (natural key: Name) – no client relation
                var accessories = await ImportAccessoriesAsync(extractDir, options, result);

                // 3. Vehicles (requires ClientId)
                var vehicles = await ImportVehiclesAsync(extractDir, options, clients, result);

                // 4. WorkOrders (requires VehicleId)
                var workOrders = await ImportWorkOrdersAsync(extractDir, options, vehicles, result);

                // 5. WorkTasks (requires WorkOrderId, AccessoryId)
                await ImportWorkTasksAsync(extractDir, options, workOrders, accessories, result);

                // 6. Travels (requires WorkOrderId)
                await ImportTravelsAsync(extractDir, options, workOrders, result);

                // 7. ProtectedRates (requires AccessoryId)
                await ImportProtectedRatesAsync(extractDir, options, accessories, result);

                // 8. Users (natural key: Username)
                await ImportUsersAsync(extractDir, options, result);

                // 9. SourceDocuments (requires WorkOrderId)
                await ImportSourceDocumentsAsync(extractDir, options, workOrders, result);

                if (!options.DryRun)
                {
                    await _unitOfWork.CommitTransactionAsync();
                    _logger.LogInfo($"Import committed: {result.RowsInserted} inserted, {result.RowsUpdated} updated, {result.RowsSkipped} skipped.");
                }
                else
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    _logger.LogInfo($"Dry run completed: {result.RowsInserted} inserts, {result.RowsUpdated} updates, {result.RowsSkipped} skips.");
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

        // ========== HELPER: Read CSV with manual column mapping ==========
        private List<Dictionary<string, string>> ReadCsvRows(string filePath)
        {
            var rows = new List<Dictionary<string, string>>();
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                PrepareHeaderForMatch = args => args.Header.Trim(),
                HeaderValidated = null,
                MissingFieldFound = null
            });
            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord.ToList();
            while (csv.Read())
            {
                var row = new Dictionary<string, string>();
                foreach (var h in headers)
                {
                    var value = csv.GetField(h);
                    row[h] = value;
                }
                rows.Add(row);
            }
            return rows;
        }

        // ========== 1. CLIENTS ==========
        private async Task<Dictionary<string, Client>> ImportClientsAsync(string folder, BackupImportOptions options, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Clients.csv");
            if (!File.Exists(filePath)) return new Dictionary<string, Client>();

            var rows = ReadCsvRows(filePath);
            var imported = new Dictionary<string, Client>(StringComparer.OrdinalIgnoreCase);
            var allExisting = await _unitOfWork.Clients.GetAllAsync();
            var existingDict = allExisting.ToDictionary(c => c.Name?.Trim() ?? "", c => c, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var name = row.GetValueOrDefault("Name")?.Trim();
                if (string.IsNullOrEmpty(name)) continue;

                if (existingDict.TryGetValue(name, out var existing))
                {
                    result.Conflicts.Add(new Conflict { Table = "Clients", Key = name, ExistingValue = existing.Name, ImportedValue = name });
                    if (options.Conflict == ConflictResolution.Overwrite)
                    {
                        existing.Address = row.GetValueOrDefault("Address");
                        existing.Phone = row.GetValueOrDefault("Phone");
                        existing.Email = row.GetValueOrDefault("Email");
                        existing.Type = Enum.TryParse<ClientType>(row.GetValueOrDefault("Type"), out var t) ? t : ClientType.Direct;
                        existing.IsActive = row.GetValueOrDefault("IsActive") == "True" || row.GetValueOrDefault("IsActive") == "true" || row.GetValueOrDefault("IsActive") == "1";
                        if (!options.DryRun) await _unitOfWork.Clients.UpdateAsync(existing);
                        result.RowsUpdated++;
                    }
                    else result.RowsSkipped++;
                    imported[name] = existing;
                }
                else
                {
                    var newClient = new Client
                    {
                        Name = name,
                        Address = row.GetValueOrDefault("Address"),
                        Phone = row.GetValueOrDefault("Phone"),
                        Email = row.GetValueOrDefault("Email"),
                        Type = Enum.TryParse<ClientType>(row.GetValueOrDefault("Type"), out var t) ? t : ClientType.Direct,
                        IsActive = row.GetValueOrDefault("IsActive") == "True" || row.GetValueOrDefault("IsActive") == "true" || row.GetValueOrDefault("IsActive") == "1"
                    };
                    if (!options.DryRun) await _unitOfWork.Clients.AddAsync(newClient);
                    result.RowsInserted++;
                    imported[name] = newClient;
                    existingDict[name] = newClient;
                }
            }
            return imported;
        }

        // ========== 2. ACCESSORIES (no client relation) ==========
        private async Task<Dictionary<string, Accessory>> ImportAccessoriesAsync(string folder, BackupImportOptions options, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Accessories.csv");
            if (!File.Exists(filePath)) return new Dictionary<string, Accessory>();

            var rows = ReadCsvRows(filePath);
            var imported = new Dictionary<string, Accessory>(StringComparer.OrdinalIgnoreCase);
            var allExisting = await _unitOfWork.Accessories.GetAllAsync();
            var existingDict = allExisting.ToDictionary(a => a.Name?.Trim() ?? "", a => a, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var name = row.GetValueOrDefault("Name")?.Trim();
                if (string.IsNullOrEmpty(name)) continue;

                if (existingDict.TryGetValue(name, out var existing))
                {
                    result.Conflicts.Add(new Conflict { Table = "Accessories", Key = name, ExistingValue = existing.Name, ImportedValue = name });
                    if (options.Conflict == ConflictResolution.Overwrite)
                    {
                        existing.PartNumber = row.GetValueOrDefault("PartNumber");
                        existing.Description = row.GetValueOrDefault("Description");
                        existing.Time = int.TryParse(row.GetValueOrDefault("Time"), out var t) ? t : (int?)null;
                        existing.Price = decimal.TryParse(row.GetValueOrDefault("Price"), out var p) ? p : 0;
                        existing.RequiresPassword = row.GetValueOrDefault("RequiresPassword") == "True" || row.GetValueOrDefault("RequiresPassword") == "true" || row.GetValueOrDefault("RequiresPassword") == "1";
                        existing.IsActive = row.GetValueOrDefault("IsActive") == "True" || row.GetValueOrDefault("IsActive") == "true" || row.GetValueOrDefault("IsActive") == "1";
                        if (!options.DryRun) await _unitOfWork.Accessories.UpdateAsync(existing);
                        result.RowsUpdated++;
                    }
                    else result.RowsSkipped++;
                    imported[name] = existing;
                }
                else
                {
                    var newAcc = new Accessory
                    {
                        Name = name,
                        PartNumber = row.GetValueOrDefault("PartNumber"),
                        Description = row.GetValueOrDefault("Description"),
                        Time = int.TryParse(row.GetValueOrDefault("Time"), out var t) ? t : (int?)null,
                        Price = decimal.TryParse(row.GetValueOrDefault("Price"), out var p) ? p : 0,
                        RequiresPassword = row.GetValueOrDefault("RequiresPassword") == "True" || row.GetValueOrDefault("RequiresPassword") == "true" || row.GetValueOrDefault("RequiresPassword") == "1",
                        IsActive = row.GetValueOrDefault("IsActive") == "True" || row.GetValueOrDefault("IsActive") == "true" || row.GetValueOrDefault("IsActive") == "1"
                    };
                    if (!options.DryRun) await _unitOfWork.Accessories.AddAsync(newAcc);
                    result.RowsInserted++;
                    imported[name] = newAcc;
                    existingDict[name] = newAcc;
                }
            }
            return imported;
        }

        // ========== 3. VEHICLES ==========
        private async Task<Dictionary<string, Vehicle>> ImportVehiclesAsync(string folder, BackupImportOptions options,
            Dictionary<string, Client> clients, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Vehicles.csv");
            if (!File.Exists(filePath)) return new Dictionary<string, Vehicle>();

            var rows = ReadCsvRows(filePath);
            var imported = new Dictionary<string, Vehicle>(StringComparer.OrdinalIgnoreCase);
            var allExisting = await _unitOfWork.Vehicles.GetAllAsync();
            var existingDict = allExisting.ToDictionary(v => v.ChassisNumber?.Trim() ?? "", v => v, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var chassis = row.GetValueOrDefault("ChassisNumber")?.Trim();
                if (string.IsNullOrEmpty(chassis)) continue;

                var clientName = row.GetValueOrDefault("Client")?.Trim();
                if (string.IsNullOrEmpty(clientName))
                {
                    result.Conflicts.Add(new Conflict { Table = "Vehicles", Key = chassis, ImportedValue = "Missing client name" });
                    result.RowsSkipped++;
                    continue;
                }
                if (!clients.TryGetValue(clientName, out var client))
                {
                    result.Conflicts.Add(new Conflict { Table = "Vehicles", Key = chassis, ImportedValue = $"Client '{clientName}' not found" });
                    result.RowsSkipped++;
                    continue;
                }

                if (existingDict.TryGetValue(chassis, out var existing))
                {
                    result.Conflicts.Add(new Conflict { Table = "Vehicles", Key = chassis, ExistingValue = existing.ChassisNumber, ImportedValue = chassis });
                    if (options.Conflict == ConflictResolution.Overwrite)
                    {
                        existing.Make = row.GetValueOrDefault("Make");
                        existing.Model = row.GetValueOrDefault("Model");
                        existing.Year = int.TryParse(row.GetValueOrDefault("Year"), out var y) ? y : (int?)null;
                        existing.Registration = row.GetValueOrDefault("Registration");
                        existing.IsActive = row.GetValueOrDefault("IsActive") == "True" || row.GetValueOrDefault("IsActive") == "true" || row.GetValueOrDefault("IsActive") == "1";
                        existing.ClientId = client.Id;
                        if (!options.DryRun) await _unitOfWork.Vehicles.UpdateAsync(existing);
                        result.RowsUpdated++;
                    }
                    else result.RowsSkipped++;
                    imported[chassis] = existing;
                }
                else
                {
                    var newVehicle = new Vehicle
                    {
                        ChassisNumber = chassis,
                        Make = row.GetValueOrDefault("Make"),
                        Model = row.GetValueOrDefault("Model"),
                        Year = int.TryParse(row.GetValueOrDefault("Year"), out var y) ? y : (int?)null,
                        Registration = row.GetValueOrDefault("Registration"),
                        IsActive = row.GetValueOrDefault("IsActive") == "True" || row.GetValueOrDefault("IsActive") == "true" || row.GetValueOrDefault("IsActive") == "1",
                        ClientId = client.Id
                    };
                    if (!options.DryRun) await _unitOfWork.Vehicles.AddAsync(newVehicle);
                    result.RowsInserted++;
                    imported[chassis] = newVehicle;
                    existingDict[chassis] = newVehicle;
                }
            }
            return imported;
        }

        // ========== 4. WORK ORDERS ==========
        private async Task<Dictionary<int, WorkOrder>> ImportWorkOrdersAsync(string folder, BackupImportOptions options,
            Dictionary<string, Vehicle> vehicles, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "WorkOrders.csv");
            if (!File.Exists(filePath)) return new Dictionary<int, WorkOrder>();

            var rows = ReadCsvRows(filePath);
            var imported = new Dictionary<int, WorkOrder>();
            var existingOrders = await _unitOfWork.WorkOrders.GetAllAsync();
            var existingDict = existingOrders.ToDictionary(o => o.Id);

            foreach (var row in rows)
            {
                var id = int.TryParse(row.GetValueOrDefault("Id"), out var i) ? i : 0;
                var chassis = row.GetValueOrDefault("Vehicle")?.Trim();
                if (string.IsNullOrEmpty(chassis))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkOrders", Key = id.ToString(), ImportedValue = "Missing vehicle chassis" });
                    result.RowsSkipped++;
                    continue;
                }
                if (!vehicles.TryGetValue(chassis, out var vehicle))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkOrders", Key = id.ToString(), ImportedValue = $"Vehicle '{chassis}' not found" });
                    result.RowsSkipped++;
                    continue;
                }

                if (existingDict.TryGetValue(id, out var existing))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkOrders", Key = id.ToString(), ExistingValue = existing.Id.ToString(), ImportedValue = id.ToString() });
                    if (options.Conflict == ConflictResolution.Overwrite)
                    {
                        existing.OrderDate = DateTime.TryParse(row.GetValueOrDefault("OrderDate"), out var d) ? d : DateTime.Today;
                        existing.Status = Enum.TryParse<WorkStatus>(row.GetValueOrDefault("Status"), out var s) ? s : WorkStatus.Planned;
                        existing.OrderType = Enum.TryParse<OrderType>(row.GetValueOrDefault("OrderType"), out var ot) ? ot : OrderType.PSA_Contract;
                        existing.CompletedDate = DateTime.TryParse(row.GetValueOrDefault("CompletedDate"), out var cd) ? cd : (DateTime?)null;
                        existing.Notes = row.GetValueOrDefault("Notes");
                        existing.VehicleId = vehicle.Id;
                        existing.TotalAmount = decimal.TryParse(row.GetValueOrDefault("TotalAmount"), out var ta) ? ta : (decimal?)null;
                        if (!options.DryRun) await _unitOfWork.WorkOrders.UpdateAsync(existing);
                        result.RowsUpdated++;
                    }
                    else result.RowsSkipped++;
                    imported[existing.Id] = existing;
                }
                else
                {
                    var newOrder = new WorkOrder
                    {
                        Id = id,
                        OrderDate = DateTime.TryParse(row.GetValueOrDefault("OrderDate"), out var d) ? d : DateTime.Today,
                        Status = Enum.TryParse<WorkStatus>(row.GetValueOrDefault("Status"), out var s) ? s : WorkStatus.Planned,
                        OrderType = Enum.TryParse<OrderType>(row.GetValueOrDefault("OrderType"), out var ot) ? ot : OrderType.PSA_Contract,
                        CompletedDate = DateTime.TryParse(row.GetValueOrDefault("CompletedDate"), out var cd) ? cd : (DateTime?)null,
                        Notes = row.GetValueOrDefault("Notes"),
                        VehicleId = vehicle.Id,
                        TotalAmount = decimal.TryParse(row.GetValueOrDefault("TotalAmount"), out var ta) ? ta : (decimal?)null
                    };
                    if (!options.DryRun) await _unitOfWork.WorkOrders.AddAsync(newOrder);
                    result.RowsInserted++;
                    imported[newOrder.Id] = newOrder;
                    existingDict[newOrder.Id] = newOrder;
                }
            }
            return imported;
        }

        // ========== 5. WORK TASKS ==========
        private async Task ImportWorkTasksAsync(string folder, BackupImportOptions options,
            Dictionary<int, WorkOrder> workOrders, Dictionary<string, Accessory> accessories, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "WorkTasks.csv");
            if (!File.Exists(filePath)) return;

            var rows = ReadCsvRows(filePath);
            var existingTasks = await _unitOfWork.WorkTasks.GetAllAsync();
            var existingDict = existingTasks.ToDictionary(t => t.Id);

            foreach (var row in rows)
            {
                var id = int.TryParse(row.GetValueOrDefault("Id"), out var i) ? i : 0;
                var workOrderId = int.TryParse(row.GetValueOrDefault("WorkOrderId"), out var woId) ? woId : 0;
                if (!workOrders.TryGetValue(workOrderId, out var workOrder))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkTasks", Key = id.ToString(), ImportedValue = $"WorkOrder {workOrderId} not found" });
                    result.RowsSkipped++;
                    continue;
                }

                var accessoryName = row.GetValueOrDefault("Accessory")?.Trim();
                if (string.IsNullOrEmpty(accessoryName))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkTasks", Key = id.ToString(), ImportedValue = "Missing accessory name" });
                    result.RowsSkipped++;
                    continue;
                }
                if (!accessories.TryGetValue(accessoryName, out var accessory))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkTasks", Key = id.ToString(), ImportedValue = $"Accessory '{accessoryName}' not found" });
                    result.RowsSkipped++;
                    continue;
                }

                if (existingDict.TryGetValue(id, out var existing))
                {
                    result.Conflicts.Add(new Conflict { Table = "WorkTasks", Key = id.ToString(), ExistingValue = existing.Id.ToString(), ImportedValue = id.ToString() });
                    if (options.Conflict == ConflictResolution.Overwrite)
                    {
                        existing.Quantity = int.TryParse(row.GetValueOrDefault("Quantity"), out var q) ? q : 1;
                        existing.TaskType = Enum.TryParse<TaskType>(row.GetValueOrDefault("TaskType"), out var tt) ? tt : TaskType.Fit;
                        existing.TaskStatus = Enum.TryParse<WorkStatus>(row.GetValueOrDefault("TaskStatus"), out var ts) ? ts : WorkStatus.Planned;
                        existing.Price = decimal.TryParse(row.GetValueOrDefault("Price"), out var p) ? p : 0;
                        existing.EstimatedMinutes = int.TryParse(row.GetValueOrDefault("EstimatedMinutes"), out var em) ? em : (int?)null;
                        existing.ActualMinutes = int.TryParse(row.GetValueOrDefault("ActualMinutes"), out var am) ? am : (int?)null;
                        existing.Notes = row.GetValueOrDefault("Notes");
                        existing.WorkOrderId = workOrder.Id;
                        existing.AccessoryId = accessory.Id;
                        if (!options.DryRun) await _unitOfWork.WorkTasks.UpdateAsync(existing);
                        result.RowsUpdated++;
                    }
                    else result.RowsSkipped++;
                }
                else
                {
                    var newTask = new WorkTask
                    {
                        Id = id,
                        Quantity = int.TryParse(row.GetValueOrDefault("Quantity"), out var q) ? q : 1,
                        TaskType = Enum.TryParse<TaskType>(row.GetValueOrDefault("TaskType"), out var tt) ? tt : TaskType.Fit,
                        TaskStatus = Enum.TryParse<WorkStatus>(row.GetValueOrDefault("TaskStatus"), out var ts) ? ts : WorkStatus.Planned,
                        Price = decimal.TryParse(row.GetValueOrDefault("Price"), out var p) ? p : 0,
                        EstimatedMinutes = int.TryParse(row.GetValueOrDefault("EstimatedMinutes"), out var em) ? em : (int?)null,
                        ActualMinutes = int.TryParse(row.GetValueOrDefault("ActualMinutes"), out var am) ? am : (int?)null,
                        Notes = row.GetValueOrDefault("Notes"),
                        WorkOrderId = workOrder.Id,
                        AccessoryId = accessory.Id
                    };
                    if (!options.DryRun) await _unitOfWork.WorkTasks.AddAsync(newTask);
                    result.RowsInserted++;
                    existingDict[newTask.Id] = newTask;
                }
            }
        }

        // ========== 6. TRAVELS ==========
        private async Task ImportTravelsAsync(string folder, BackupImportOptions options,
            Dictionary<int, WorkOrder> workOrders, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Travels.csv");
            if (!File.Exists(filePath)) return;

            var rows = ReadCsvRows(filePath);
            var existingTravels = await _unitOfWork.Travels.GetAllAsync();
            var existingDict = existingTravels.ToDictionary(t => t.Id);

            foreach (var row in rows)
            {
                var id = int.TryParse(row.GetValueOrDefault("Id"), out var i) ? i : 0;
                var workOrderId = int.TryParse(row.GetValueOrDefault("WorkOrderId"), out var woId) ? woId : 0;
                if (!workOrders.TryGetValue(workOrderId, out var workOrder))
                {
                    result.Conflicts.Add(new Conflict { Table = "Travels", Key = id.ToString(), ImportedValue = $"WorkOrder {workOrderId} not found" });
                    result.RowsSkipped++;
                    continue;
                }

                if (existingDict.TryGetValue(id, out var existing))
                {
                    result.Conflicts.Add(new Conflict { Table = "Travels", Key = id.ToString(), ExistingValue = existing.Id.ToString(), ImportedValue = id.ToString() });
                    if (options.Conflict == ConflictResolution.Overwrite)
                    {
                        existing.TravelDate = DateTime.TryParse(row.GetValueOrDefault("TravelDate"), out var d) ? d : DateTime.Today;
                        existing.Destination = row.GetValueOrDefault("Destination");
                        existing.DistanceKm = decimal.TryParse(row.GetValueOrDefault("DistanceKm"), out var km) ? km : (decimal?)null;
                        existing.TravelCost = decimal.TryParse(row.GetValueOrDefault("TravelCost"), out var cost) ? cost : (decimal?)null;
                        existing.Notes = row.GetValueOrDefault("Notes");
                        existing.WorkOrderId = workOrder.Id;
                        if (!options.DryRun) await _unitOfWork.Travels.UpdateAsync(existing);
                        result.RowsUpdated++;
                    }
                    else result.RowsSkipped++;
                }
                else
                {
                    var newTravel = new Travel
                    {
                        Id = id,
                        TravelDate = DateTime.TryParse(row.GetValueOrDefault("TravelDate"), out var d) ? d : DateTime.Today,
                        Destination = row.GetValueOrDefault("Destination"),
                        DistanceKm = decimal.TryParse(row.GetValueOrDefault("DistanceKm"), out var km) ? km : (decimal?)null,
                        TravelCost = decimal.TryParse(row.GetValueOrDefault("TravelCost"), out var cost) ? cost : (decimal?)null,
                        Notes = row.GetValueOrDefault("Notes"),
                        WorkOrderId = workOrder.Id
                    };
                    if (!options.DryRun) await _unitOfWork.Travels.AddAsync(newTravel);
                    result.RowsInserted++;
                    existingDict[newTravel.Id] = newTravel;
                }
            }
        }

        // ========== 7. PROTECTED RATES ==========
        private async Task ImportProtectedRatesAsync(string folder, BackupImportOptions options,
            Dictionary<string, Accessory> accessories, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "ProtectedRates.csv");
            if (!File.Exists(filePath)) return;

            var rows = ReadCsvRows(filePath);
            var existingRates = await _unitOfWork.ProtectedRates.GetAllAsync();
            var existingDict = existingRates.ToDictionary(r => r.Id);

            foreach (var row in rows)
            {
                var id = int.TryParse(row.GetValueOrDefault("Id"), out var i) ? i : 0;
                var accessoryName = row.GetValueOrDefault("Accessory")?.Trim();
                if (string.IsNullOrEmpty(accessoryName))
                {
                    result.Conflicts.Add(new Conflict { Table = "ProtectedRates", Key = id.ToString(), ImportedValue = "Missing accessory name" });
                    result.RowsSkipped++;
                    continue;
                }
                if (!accessories.TryGetValue(accessoryName, out var accessory))
                {
                    result.Conflicts.Add(new Conflict { Table = "ProtectedRates", Key = id.ToString(), ImportedValue = $"Accessory '{accessoryName}' not found" });
                    result.RowsSkipped++;
                    continue;
                }

                if (existingDict.TryGetValue(id, out var existing))
                {
                    result.Conflicts.Add(new Conflict { Table = "ProtectedRates", Key = id.ToString(), ExistingValue = existing.Id.ToString(), ImportedValue = id.ToString() });
                    if (options.Conflict == ConflictResolution.Overwrite)
                    {
                        existing.ValidFrom = DateTime.TryParse(row.GetValueOrDefault("ValidFrom"), out var vf) ? vf : DateTime.Today;
                        existing.ValidTo = DateTime.TryParse(row.GetValueOrDefault("ValidTo"), out var vt) ? vt : (DateTime?)null;
                        existing.HourlyRate = decimal.TryParse(row.GetValueOrDefault("HourlyRate"), out var hr) ? hr : 0;
                        existing.Currency = row.GetValueOrDefault("Currency") ?? "EUR";
                        existing.AccessoryId = accessory.Id;
                        if (!options.DryRun) await _unitOfWork.ProtectedRates.UpdateAsync(existing);
                        result.RowsUpdated++;
                    }
                    else result.RowsSkipped++;
                }
                else
                {
                    var newRate = new ProtectedRate
                    {
                        Id = id,
                        ValidFrom = DateTime.TryParse(row.GetValueOrDefault("ValidFrom"), out var vf) ? vf : DateTime.Today,
                        ValidTo = DateTime.TryParse(row.GetValueOrDefault("ValidTo"), out var vt) ? vt : (DateTime?)null,
                        HourlyRate = decimal.TryParse(row.GetValueOrDefault("HourlyRate"), out var hr) ? hr : 0,
                        Currency = row.GetValueOrDefault("Currency") ?? "EUR",
                        AccessoryId = accessory.Id
                    };
                    if (!options.DryRun) await _unitOfWork.ProtectedRates.AddAsync(newRate);
                    result.RowsInserted++;
                    existingDict[newRate.Id] = newRate;
                }
            }
        }

        // ========== 8. USERS ==========
        private async Task ImportUsersAsync(string folder, BackupImportOptions options, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "Users.csv");
            if (!File.Exists(filePath)) return;

            var rows = ReadCsvRows(filePath);
            var existingUsers = await _unitOfWork.Users.GetAllAsync();
            var existingDict = existingUsers.ToDictionary(u => u.Username?.Trim() ?? "", u => u, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var username = row.GetValueOrDefault("Username")?.Trim();
                if (string.IsNullOrEmpty(username)) continue;

                if (existingDict.TryGetValue(username, out var existing))
                {
                    result.Conflicts.Add(new Conflict { Table = "Users", Key = username, ExistingValue = existing.Username, ImportedValue = username });
                    if (options.Conflict == ConflictResolution.Overwrite)
                    {
                        existing.PasswordHash = row.GetValueOrDefault("PasswordHash");
                        existing.Role = row.GetValueOrDefault("Role") ?? "User";
                        existing.IsActive = row.GetValueOrDefault("IsActive") == "True" || row.GetValueOrDefault("IsActive") == "true" || row.GetValueOrDefault("IsActive") == "1";
                        if (!options.DryRun) await _unitOfWork.Users.UpdateAsync(existing);
                        result.RowsUpdated++;
                    }
                    else result.RowsSkipped++;
                }
                else
                {
                    var newUser = new User
                    {
                        Username = username,
                        PasswordHash = row.GetValueOrDefault("PasswordHash"),
                        Role = row.GetValueOrDefault("Role") ?? "User",
                        IsActive = row.GetValueOrDefault("IsActive") == "True" || row.GetValueOrDefault("IsActive") == "true" || row.GetValueOrDefault("IsActive") == "1"
                    };
                    if (!options.DryRun) await _unitOfWork.Users.AddAsync(newUser);
                    result.RowsInserted++;
                    existingDict[username] = newUser;
                }
            }
        }

        // ========== 9. SOURCE DOCUMENTS ==========
        private async Task ImportSourceDocumentsAsync(string folder, BackupImportOptions options,
            Dictionary<int, WorkOrder> workOrders, BackupImportResult result)
        {
            var filePath = Path.Combine(folder, "SourceDocuments.csv");
            if (!File.Exists(filePath)) return;

            var rows = ReadCsvRows(filePath);
            var existingDocs = await _unitOfWork.SourceDocuments.GetAllAsync();
            var existingDict = existingDocs.ToDictionary(d => d.FileHash ?? "", d => d, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var id = int.TryParse(row.GetValueOrDefault("Id"), out var i) ? i : 0;
                var workOrderId = int.TryParse(row.GetValueOrDefault("WorkOrderId"), out var woId) ? woId : 0;
                if (!workOrders.TryGetValue(workOrderId, out var workOrder))
                {
                    result.Conflicts.Add(new Conflict { Table = "SourceDocuments", Key = id.ToString(), ImportedValue = $"WorkOrder {workOrderId} not found" });
                    result.RowsSkipped++;
                    continue;
                }
                var fileHash = row.GetValueOrDefault("FileHash")?.Trim();
                if (string.IsNullOrEmpty(fileHash))
                {
                    result.Conflicts.Add(new Conflict { Table = "SourceDocuments", Key = id.ToString(), ImportedValue = "Missing file hash" });
                    result.RowsSkipped++;
                    continue;
                }

                if (existingDict.TryGetValue(fileHash, out var existing))
                {
                    result.Conflicts.Add(new Conflict { Table = "SourceDocuments", Key = fileHash, ExistingValue = existing.FileHash, ImportedValue = fileHash });
                    if (options.Conflict == ConflictResolution.Overwrite)
                    {
                        existing.DocumentType = row.GetValueOrDefault("DocumentType");
                        existing.FilePath = row.GetValueOrDefault("FilePath");
                        existing.OriginalFilename = row.GetValueOrDefault("OriginalFilename");
                        existing.WorkOrderId = workOrder.Id;
                        if (!options.DryRun) await _unitOfWork.SourceDocuments.UpdateAsync(existing);
                        result.RowsUpdated++;
                    }
                    else result.RowsSkipped++;
                }
                else
                {
                    var newDoc = new SourceDocument
                    {
                        Id = id,
                        DocumentType = row.GetValueOrDefault("DocumentType"),
                        FilePath = row.GetValueOrDefault("FilePath"),
                        FileHash = fileHash,
                        OriginalFilename = row.GetValueOrDefault("OriginalFilename"),
                        WorkOrderId = workOrder.Id
                    };
                    if (!options.DryRun) await _unitOfWork.SourceDocuments.AddAsync(newDoc);
                    result.RowsInserted++;
                    existingDict[fileHash] = newDoc;
                }
            }
        }
    }
}