-- SK Auto Work Tracking System – SQLite Schema
-- Generated from real Monday.com export data
PRAGMA foreign_keys = ON;

-- =====================================================
-- 1. CLIENTS
-- =====================================================
CREATE TABLE Clients (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL COLLATE NOCASE,
    Code TEXT,
    Type TEXT NOT NULL CHECK(Type IN ('PSA', 'Direct')),
    Address TEXT,
    Phone TEXT,
    Email TEXT,
    Notes TEXT,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP
);

CREATE UNIQUE INDEX IX_Clients_Name ON Clients(Name COLLATE NOCASE);
CREATE INDEX IX_Clients_Type ON Clients(Type);
CREATE INDEX IX_Clients_Code ON Clients(Code);

-- =====================================================
-- 2. VEHICLES
-- =====================================================
CREATE TABLE Vehicles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChassisNumber TEXT NOT NULL COLLATE NOCASE,
    Model TEXT,
    Make TEXT,
    Year INTEGER,
    Registration TEXT,
    ClientId INTEGER,
    Notes TEXT,
    Status TEXT DEFAULT 'Active',
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX IX_Vehicles_ChassisNumber ON Vehicles(ChassisNumber COLLATE NOCASE);
CREATE INDEX IX_Vehicles_Model ON Vehicles(Model);
CREATE INDEX IX_Vehicles_ClientId ON Vehicles(ClientId);

-- =====================================================
-- 3. ACCESSORIES
-- =====================================================
CREATE TABLE Accessories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL COLLATE NOCASE,
    Category TEXT,
    PartNumber TEXT,
    UnitPrice DECIMAL(10,2),
    FittingPrice DECIMAL(10,2),
    StandardFittingTime INTEGER, -- minutes (PSA estimate)
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP
);

CREATE UNIQUE INDEX IX_Accessories_Name ON Accessories(Name COLLATE NOCASE);
CREATE INDEX IX_Accessories_Category ON Accessories(Category);

-- =====================================================
-- 4. PROTECTED RATES (PSA hourly rates – password protected)
-- =====================================================
CREATE TABLE ProtectedRates (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AccessoryId INTEGER NOT NULL,
    ValidFrom DATE NOT NULL,
    ValidTo DATE,
    HourlyRate DECIMAL(10,2) NOT NULL,
    Currency TEXT DEFAULT 'EUR',
    EncryptedRate BLOB, -- optional encryption
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (AccessoryId) REFERENCES Accessories(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ProtectedRates_AccessoryId ON ProtectedRates(AccessoryId);
CREATE INDEX IX_ProtectedRates_ValidFrom ON ProtectedRates(ValidFrom);

-- =====================================================
-- 5. WORK ORDERS
-- =====================================================
CREATE TABLE WorkOrders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ClientId INTEGER NOT NULL,
    VehicleId INTEGER NOT NULL,
    OrderReference TEXT, -- e.g. "TW1357/VEOLIA"
    OrderType TEXT NOT NULL CHECK(OrderType IN ('PSA_Contract', 'Direct_Fitting', 'Direct_Sale')),
    OrderDate DATE NOT NULL,
    PlannedDate DATE,
    CompletedDate DATE,
    Status TEXT NOT NULL DEFAULT 'Planned' CHECK(Status IN ('Planned', 'InProgress', 'Blocked', 'Done')),
    TotalAmount DECIMAL(10,2),
    Notes TEXT,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE RESTRICT,
    FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id) ON DELETE RESTRICT
);

CREATE INDEX IX_WorkOrders_OrderDate ON WorkOrders(OrderDate);
CREATE INDEX IX_WorkOrders_ClientId ON WorkOrders(ClientId);
CREATE INDEX IX_WorkOrders_VehicleId ON WorkOrders(VehicleId);
CREATE INDEX IX_WorkOrders_Status ON WorkOrders(Status);
CREATE INDEX IX_WorkOrders_OrderReference ON WorkOrders(OrderReference);

-- =====================================================
-- 6. WORK TASKS (one per accessory per work order)
-- =====================================================
CREATE TABLE WorkTasks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkOrderId INTEGER NOT NULL,
    AccessoryId INTEGER NOT NULL,
    TaskType TEXT NOT NULL CHECK(TaskType IN ('Fit', 'Sell', 'Remove', 'Controle', 'Polish', 'VP_VU', 'TATOUAGE', 'Other')),
    Quantity INTEGER NOT NULL DEFAULT 1 CHECK(Quantity > 0),
    UnitPrice DECIMAL(10,2),
    FittingPrice DECIMAL(10,2),
    EstimatedMinutes INTEGER,
    ActualMinutes INTEGER,
    TaskStatus TEXT NOT NULL DEFAULT 'Planned' CHECK(TaskStatus IN ('Planned', 'InProgress', 'Blocked', 'Done')),
    Notes TEXT,
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE,
    FOREIGN KEY (AccessoryId) REFERENCES Accessories(Id) ON DELETE RESTRICT
);

CREATE INDEX IX_WorkTasks_WorkOrderId ON WorkTasks(WorkOrderId);
CREATE INDEX IX_WorkTasks_AccessoryId ON WorkTasks(AccessoryId);
CREATE INDEX IX_WorkTasks_TaskStatus ON WorkTasks(TaskStatus);

-- =====================================================
-- 7. TRAVEL
-- =====================================================
CREATE TABLE Travels (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkOrderId INTEGER NOT NULL,
    TravelDate DATE NOT NULL,
    Destination TEXT NOT NULL,
    DistanceKm DECIMAL(5,2),
    TravelCost DECIMAL(10,2),
    Notes TEXT,
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Travels_WorkOrderId ON Travels(WorkOrderId);
CREATE INDEX IX_Travels_TravelDate ON Travels(TravelDate);

-- =====================================================
-- 8. SOURCE DOCUMENTS (PDF/Excel imports)
-- =====================================================
CREATE TABLE SourceDocuments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkOrderId INTEGER NOT NULL,
    DocumentType TEXT NOT NULL CHECK(DocumentType IN ('PSA_Plan', 'Client_Order', 'Invoice')),
    FilePath TEXT NOT NULL,
    FileHash TEXT NOT NULL UNIQUE,
    OriginalFilename TEXT,
    ImportedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE
);

CREATE INDEX IX_SourceDocuments_WorkOrderId ON SourceDocuments(WorkOrderId);
CREATE INDEX IX_SourceDocuments_FileHash ON SourceDocuments(FileHash);

-- =====================================================
-- 9. TRIGGERS (automatic UpdatedAt)
-- =====================================================
CREATE TRIGGER TR_Clients_UpdatedAt AFTER UPDATE ON Clients
BEGIN
    UPDATE Clients SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
END;

CREATE TRIGGER TR_Vehicles_UpdatedAt AFTER UPDATE ON Vehicles
BEGIN
    UPDATE Vehicles SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
END;

CREATE TRIGGER TR_Accessories_UpdatedAt AFTER UPDATE ON Accessories
BEGIN
    UPDATE Accessories SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
END;

CREATE TRIGGER TR_WorkOrders_UpdatedAt AFTER UPDATE ON WorkOrders
BEGIN
    UPDATE WorkOrders SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
END;