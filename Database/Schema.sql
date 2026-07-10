-- Enable FK enforcement
PRAGMA foreign_keys = ON;

-- ============================================
-- USERS (for authentication and role management)
-- ============================================
CREATE TABLE Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash TEXT NOT NULL,
    Role TEXT NOT NULL DEFAULT 'User' CHECK(Role IN ('Admin', 'User')),
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NULL
);

CREATE INDEX IX_Users_Username ON Users(Username);
CREATE INDEX IX_Users_Role ON Users(Role);

-- Insert default admin user (password: admin)
INSERT INTO Users (Username, PasswordHash, Role, IsActive) 
VALUES ('admin', 'admin', 'Admin', 1);

-- ============================================
-- CLIENTS (master client list)
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
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NULL,
    FOREIGN KEY (ParentClientId) REFERENCES Clients(Id) ON DELETE SET NULL
);

CREATE INDEX IX_Clients_Name ON Clients(Name);
CREATE INDEX IX_Clients_ParentClientId ON Clients(ParentClientId);

-- ============================================
-- VEHICLES (now with ClientId – one client per vehicle)
-- ============================================
CREATE TABLE Vehicles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChassisNumber TEXT NOT NULL COLLATE NOCASE UNIQUE,
    ClientId INTEGER NOT NULL,                     -- each vehicle belongs to one client
    Make TEXT NULL,
    Model TEXT NULL,
    Year INTEGER NULL,
    Registration TEXT NULL,
    Notes TEXT NULL,
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NULL,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE RESTRICT
);

CREATE INDEX IX_Vehicles_ChassisNumber ON Vehicles(ChassisNumber);
CREATE INDEX IX_Vehicles_Model ON Vehicles(Model);
CREATE INDEX IX_Vehicles_ClientId ON Vehicles(ClientId);

-- ============================================
-- ACCESSORIES CATALOG (with category and single price)
-- ============================================
CREATE TABLE Accessories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PartNumber TEXT NULL UNIQUE,
    Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
    Description TEXT NULL,
    Category TEXT NULL,                  -- e.g., 'Fitting', 'Selling', 'Preparation', 'Déplacement'
    Price DECIMAL(10,2) NULL,            -- single price (instead of separate Price/FittingPrice)
    Time INTEGER NULL,     -- minutes (PSA duration)
    Price DECIMAL(10,2) NULL,    -- optional, if needed
    RequiresPassword BOOLEAN DEFAULT 0,
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NULL
);

CREATE INDEX IX_Accessories_Name ON Accessories(Name);
CREATE INDEX IX_Accessories_Category ON Accessories(Category);



-- ============================================
-- PROTECTED RATES (keep if still needed)
-- ============================================
CREATE TABLE ProtectedRates (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AccessoryId INTEGER NOT NULL,
    ValidFrom TEXT NOT NULL,
    ValidTo TEXT NULL,
    HourlyRate DECIMAL(10,2) NOT NULL,
    Currency TEXT DEFAULT 'EUR',
    EncryptedData BLOB NULL,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (AccessoryId) REFERENCES Accessories(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ProtectedRates_AccessoryId ON ProtectedRates(AccessoryId);

-- ============================================
-- WORK ORDERS (no longer have ClientId – client is determined via vehicle)
-- ============================================
CREATE TABLE WorkOrders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    VehicleId INTEGER NOT NULL,
    OrderReference TEXT NULL UNIQUE,
    OrderType TEXT NOT NULL DEFAULT 'Direct_Contract' CHECK(OrderType IN ('PSA_Contract', 'Direct_Contract')),
    OrderDate TEXT NOT NULL,
    PlannedDate TEXT NULL,
    CompletedDate TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Planned' CHECK(Status IN ('Planned', 'InProgress', 'Blocked', 'Done')),
    Notes TEXT NULL,
    TotalAmount DECIMAL(10,2) NULL,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NULL,
    FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id) ON DELETE RESTRICT
);

CREATE INDEX IX_WorkOrders_OrderDate ON WorkOrders(OrderDate);
CREATE INDEX IX_WorkOrders_VehicleId ON WorkOrders(VehicleId);
CREATE INDEX IX_WorkOrders_Status ON WorkOrders(Status);

-- ============================================
-- WORK TASKS (simplified pricing: only Price column)
-- ============================================
CREATE TABLE WorkTasks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkOrderId INTEGER NOT NULL,
    AccessoryId INTEGER NOT NULL,
    TaskType TEXT NOT NULL DEFAULT 'Fit' CHECK(TaskType IN ('Fit', 'Sell', 'Remove', 'Preparation', 'Déplacement')),
    Quantity INTEGER NOT NULL DEFAULT 1 CHECK(Quantity > 0),
    Price DECIMAL(10,2) NULL,             -- single price at task level
    EstimatedMinutes INTEGER NULL,
    ActualMinutes INTEGER NULL,
    TaskStatus TEXT NOT NULL DEFAULT 'Planned' CHECK(TaskStatus IN ('Planned', 'InProgress', 'Blocked', 'Done')),
    Notes TEXT NULL,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
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
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
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
    FileHash TEXT NOT NULL UNIQUE,
    OriginalFilename TEXT NOT NULL,
    ImportedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE
);

CREATE INDEX IX_SourceDocuments_FileHash ON SourceDocuments(FileHash);

-- ============================================
-- VIEW: Daily Work Summary (updated to get client from vehicle)
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
JOIN Vehicles v ON wo.VehicleId = v.Id
JOIN Clients c ON v.ClientId = c.Id
JOIN WorkTasks wt ON wo.Id = wt.WorkOrderId
JOIN Accessories a ON wt.AccessoryId = a.Id;