-- =============================================
-- Migration: Add BillingStatus and Attachment fields
-- Date: 2026-07-31
-- Author: SKAuto Team
-- =============================================

-- 1. Add BillingStatus to WorkOrder (default = 0 -> ToDo)
-- Values: 0 = ToDo, 1 = Done, 2 = Pending
ALTER TABLE WorkOrders ADD COLUMN BillingStatus INTEGER NOT NULL DEFAULT 0;

-- 2. Add Google Drive fields to SourceDocuments
ALTER TABLE SourceDocuments ADD COLUMN GoogleDriveFileId TEXT;
ALTER TABLE SourceDocuments ADD COLUMN FileSize INTEGER;
ALTER TABLE SourceDocuments ADD COLUMN UploadDate TEXT;
ALTER TABLE SourceDocuments ADD COLUMN ContentType TEXT;

-- 3. Optional: Create an index on GoogleDriveFileId for faster lookups
CREATE INDEX IF NOT EXISTS IX_SourceDocuments_GoogleDriveFileId ON SourceDocuments(GoogleDriveFileId);

-- 4. Optional: Create an index on WorkOrderId + UploadDate for sorting
CREATE INDEX IF NOT EXISTS IX_SourceDocuments_WorkOrderId_UploadDate ON SourceDocuments(WorkOrderId, UploadDate DESC);

-- 5. Update existing rows (if any) with default values for new fields
-- No existing rows need updating because the new columns are nullable or have defaults.
-- For WorkOrder, BillingStatus defaults to 0 (ToDo), which is correct.

-- 6. Verify the changes
SELECT name FROM pragma_table_info('WorkOrders') WHERE name = 'BillingStatus';
SELECT name FROM pragma_table_info('SourceDocuments') WHERE name = 'GoogleDriveFileId';
SELECT name FROM pragma_table_info('SourceDocuments') WHERE name = 'FileSize';
SELECT name FROM pragma_table_info('SourceDocuments') WHERE name = 'UploadDate';
SELECT name FROM pragma_table_info('SourceDocuments') WHERE name = 'ContentType';