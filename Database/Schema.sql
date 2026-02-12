-- Enable FK enforcement
PRAGMA foreign_keys = ON;

-- ============================================
-- 1. CLIENTS
-- ============================================
CREATE TABLE Clients (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE COLLATE NOCASE,
    ClientCode TEXT,
    Type TEXT NOT NULL DEFAULT 'PSA' CHECK(Type IN ('PSA', 'Direct')),
    Address TEXT,
    Phone TEXT,
    Email TEXT,
    Notes TEXT,
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    UpdatedAt TEXT
);

-- ============================================
-- 2. VEHICLES
-- ============================================
CREATE TABLE Vehicles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChassisNumber TEXT NOT NULL UNIQUE COLLATE NOCASE,
    Model TEXT NOT NULL,
    Brand TEXT,
    Year INTEGER,
    Registration TEXT,
    ClientId INTEGER,           -- if vehicle is tied to a specific client
    Notes TEXT,
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    UpdatedAt TEXT,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE SET NULL
);

-- ============================================
-- 3. ACCESSORIES
-- ============================================
CREATE TABLE Accessories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PartNumber TEXT,
    Name TEXT NOT NULL UNIQUE COLLATE NOCASE,
    Category TEXT,
    UnitPrice DECIMAL(10,2),    -- for direct sale
    FittingPrice DECIMAL(10,2), -- for direct fitting
    PSADurationMinutes INTEGER, -- standard time for PSA work
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    UpdatedAt TEXT
);

-- ============================================
-- 4. PROTECTED_RATES (PSA hourly rates per accessory)
-- ============================================
CREATE TABLE ProtectedRates (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AccessoryId INTEGER NOT NULL,
    ValidFrom TEXT NOT NULL,
    ValidTo TEXT,
    HourlyRate DECIMAL(10,2) NOT NULL,
    Currency TEXT DEFAULT 'EUR',
    EncryptedData BLOB,         -- optional encryption
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    FOREIGN KEY (AccessoryId) REFERENCES Accessories(Id) ON DELETE CASCADE
);

-- ============================================
-- 5. WORK_ORDERS
-- ============================================
CREATE TABLE WorkOrders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ClientId INTEGER NOT NULL,
    VehicleId INTEGER NOT NULL,
    OrderReference TEXT,
    OrderType TEXT NOT NULL CHECK(OrderType IN ('PSA_Contract', 'Direct_Fitting', 'Direct_Sale')),
    OrderDate TEXT NOT NULL,
    PlannedDate TEXT,
    CompletedDate TEXT,
    Status TEXT NOT NULL DEFAULT 'Planned' CHECK(Status IN ('Planned', 'InProgress', 'Blocked', 'Done')),
    WeekNumber INTEGER,
    Notes TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    UpdatedAt TEXT,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id),
    FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
);

-- ============================================
-- 6. WORK_TASKS
-- ============================================
CREATE TABLE WorkTasks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkOrderId INTEGER NOT NULL,
    AccessoryId INTEGER NOT NULL,
    TaskType TEXT NOT NULL CHECK(TaskType IN ('Fit', 'Sell', 'Remove')),
    Quantity INTEGER NOT NULL DEFAULT 1,
    UnitPrice DECIMAL(10,2),    -- price per unit (direct sale)
    FittingPrice DECIMAL(10,2), -- price for fitting (direct)
    EstimatedMinutes INTEGER,
    ActualMinutes INTEGER,
    TaskStatus TEXT NOT NULL DEFAULT 'Planned' CHECK(TaskStatus IN ('Planned', 'InProgress', 'Blocked', 'Done')),
    Notes TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    UpdatedAt TEXT,
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE,
    FOREIGN KEY (AccessoryId) REFERENCES Accessories(Id)
);

-- ============================================
-- 7. TRAVELS (optional)
-- ============================================
CREATE TABLE Travels (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkOrderId INTEGER NOT NULL,
    TravelDate TEXT NOT NULL,
    Destination TEXT NOT NULL,
    DistanceKm DECIMAL(5,2),
    TravelCost DECIMAL(10,2),
    Notes TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE
);

-- ============================================
-- 8. SOURCE_DOCUMENTS (for audit)
-- ============================================
CREATE TABLE SourceDocuments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkOrderId INTEGER NOT NULL,
    DocumentType TEXT NOT NULL,
    FilePath TEXT NOT NULL,
    FileHash TEXT NOT NULL UNIQUE,
    OriginalFilename TEXT,
    ImportedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE
);

-- ============================================
-- INDEXES FOR PERFORMANCE
-- ============================================
CREATE INDEX IX_WorkOrders_OrderDate ON WorkOrders(OrderDate);
CREATE INDEX IX_WorkOrders_Status ON WorkOrders(Status);
CREATE INDEX IX_WorkOrders_ClientId ON WorkOrders(ClientId);
CREATE INDEX IX_WorkTasks_WorkOrderId ON WorkTasks(WorkOrderId);
CREATE INDEX IX_WorkTasks_TaskStatus ON WorkTasks(TaskStatus);
CREATE INDEX IX_Vehicles_ChassisNumber ON Vehicles(ChassisNumber);
CREATE INDEX IX_Vehicles_Model ON Vehicles(Model);
CREATE INDEX IX_Clients_Name ON Clients(Name);