-- Enable FK enforcement
PRAGMA foreign_keys = ON;

-- ============================================
-- CLIENTS (normalized with parent grouping)
-- ============================================
CREATE TABLE Clients (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
    ParentClientId INTEGER NULL,
    ClientCode TEXT NULL,
    Type TEXT NOT NULL DEFAULT 'Direct' CHECK(Type IN ('PSA', 'Direct')),
    Address TEXT NULL,
    Phone TEXT NULL,
    Email TEXT NULL,
    Notes TEXT NULL,
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    UpdatedAt TEXT NULL,
    FOREIGN KEY (ParentClientId) REFERENCES Clients(Id) ON DELETE SET NULL
);

CREATE INDEX IX_Clients_Name ON Clients(Name);
CREATE INDEX IX_Clients_ParentClientId ON Clients(ParentClientId);

-- ============================================
-- VEHICLES (chassis is the real-world unique key)
-- ============================================
CREATE TABLE Vehicles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChassisNumber TEXT NOT NULL COLLATE NOCASE UNIQUE,
    Make TEXT NULL,
    Model TEXT NULL,
    Year INTEGER NULL,
    Registration TEXT NULL,
    Notes TEXT NULL,
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    UpdatedAt TEXT NULL
);

CREATE INDEX IX_Vehicles_ChassisNumber ON Vehicles(ChassisNumber);
CREATE INDEX IX_Vehicles_Model ON Vehicles(Model);

-- ============================================
-- ACCESSORIES CATALOG (with PSA rates & pricing)
-- ============================================
CREATE TABLE Accessories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PartNumber TEXT NULL UNIQUE,
    Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
    Category TEXT NULL,                  -- e.g., 'Sticker', 'Electrical', 'Exterior'
    UnitPrice DECIMAL(10,2) NULL,
    FittingPrice DECIMAL(10,2) NULL,
    StandardFittingTime INTEGER NULL,    -- minutes (PSA duration)
    RequiresPassword BOOLEAN DEFAULT 0,  -- for PSA protected rates
    IsActive BOOLEAN DEFAULT 1,
    Notes TEXT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    UpdatedAt TEXT NULL
);

CREATE INDEX IX_Accessories_Name ON Accessories(Name);
CREATE INDEX IX_Accessories_Category ON Accessories(Category);

-- ============================================
-- PROTECTED RATES (PSA hourly rates – encrypted on demand)
-- ============================================
CREATE TABLE ProtectedRates (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AccessoryId INTEGER NOT NULL,
    ValidFrom TEXT NOT NULL,
    ValidTo TEXT NULL,
    HourlyRate DECIMAL(10,2) NOT NULL,
    Currency TEXT DEFAULT 'EUR',
    EncryptedData BLOB NULL,            -- for future encryption
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    FOREIGN KEY (AccessoryId) REFERENCES Accessories(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ProtectedRates_AccessoryId ON ProtectedRates(AccessoryId);

-- ============================================
-- WORK ORDERS (grouping of tasks per vehicle per day)
-- ============================================
CREATE TABLE WorkOrders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ClientId INTEGER NOT NULL,
    VehicleId INTEGER NOT NULL,
    OrderReference TEXT NULL UNIQUE,    -- e.g., "TW1357/VEOLIA"
    OrderType TEXT NOT NULL DEFAULT 'Direct_Fitting' CHECK(OrderType IN ('PSA_Contract', 'Direct_Fitting', 'Direct_Sale')),
    OrderDate TEXT NOT NULL,            -- "Date Assigned"
    PlannedDate TEXT NULL,
    CompletedDate TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Planned' CHECK(Status IN ('Planned', 'InProgress', 'Blocked', 'Done')),
    Notes TEXT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    UpdatedAt TEXT NULL,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE RESTRICT,
    FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id) ON DELETE RESTRICT
);

CREATE INDEX IX_WorkOrders_OrderDate ON WorkOrders(OrderDate);
CREATE INDEX IX_WorkOrders_ClientId ON WorkOrders(ClientId);
CREATE INDEX IX_WorkOrders_VehicleId ON WorkOrders(VehicleId);
CREATE INDEX IX_WorkOrders_Status ON WorkOrders(Status);

-- ============================================
-- WORK TASKS (each row from your daily work sheet)
-- ============================================
CREATE TABLE WorkTasks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkOrderId INTEGER NOT NULL,
    AccessoryId INTEGER NOT NULL,
    TaskType TEXT NOT NULL DEFAULT 'Fit' CHECK(TaskType IN ('Fit', 'Sell', 'Remove')),
    Quantity INTEGER NOT NULL DEFAULT 1 CHECK(Quantity > 0),
    UnitPrice DECIMAL(10,2) NULL,       -- captured at time of task
    FittingPrice DECIMAL(10,2) NULL,    -- captured at time of task
    EstimatedMinutes INTEGER NULL,      -- from catalog at creation time
    ActualMinutes INTEGER NULL,         -- filled later
    TaskStatus TEXT NOT NULL DEFAULT 'Planned' CHECK(TaskStatus IN ('Planned', 'InProgress', 'Blocked', 'Done')),
    Notes TEXT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    UpdatedAt TEXT NULL,
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE,
    FOREIGN KEY (AccessoryId) REFERENCES Accessories(Id) ON DELETE RESTRICT
);

CREATE INDEX IX_WorkTasks_WorkOrderId ON WorkTasks(WorkOrderId);
CREATE INDEX IX_WorkTasks_TaskStatus ON WorkTasks(TaskStatus);

-- ============================================
-- TRAVEL (optional, per work order)
-- ============================================
CREATE TABLE Travels (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkOrderId INTEGER NOT NULL,
    TravelDate TEXT NOT NULL,
    Destination TEXT NOT NULL,
    DistanceKm DECIMAL(6,1) NULL,
    TravelCost DECIMAL(10,2) NULL,
    Notes TEXT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE
);

-- ============================================
-- SOURCE DOCUMENTS (import tracking, deduplication)
-- ============================================
CREATE TABLE SourceDocuments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkOrderId INTEGER NOT NULL,
    DocumentType TEXT NOT NULL CHECK(DocumentType IN ('PSA_Plan', 'Client_Order', 'Invoice')),
    FilePath TEXT NOT NULL,
    FileHash TEXT NOT NULL UNIQUE,      -- SHA256 for duplicate prevention
    OriginalFilename TEXT NOT NULL,
    ImportedAt TEXT NOT NULL DEFAULT (DATETIME('now')),
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE
);

CREATE INDEX IX_SourceDocuments_FileHash ON SourceDocuments(FileHash);

-- ============================================
-- VIEW: Daily Work Summary (mirrors your Excel report)
-- ============================================
CREATE VIEW DailyWorkSummary AS
SELECT 
    wo.OrderDate,
    c.Name AS ClientName,
    v.ChassisNumber,
    v.Model,
    wo.OrderReference,
    a.Name AS AccessoryName,
    wt.TaskStatus,
    wt.Quantity,
    wt.ActualMinutes,
    wo.CompletedDate
FROM WorkOrders wo
JOIN Clients c ON wo.ClientId = c.Id
JOIN Vehicles v ON wo.VehicleId = v.Id
JOIN WorkTasks wt ON wo.Id = wt.WorkOrderId
JOIN Accessories a ON wt.AccessoryId = a.Id;