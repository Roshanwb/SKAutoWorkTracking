# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] – 2026-08-06

### Added
- **Billing status** (Done/ToDo/Pending) for work orders with color‑coded grid column and update buttons (#68)
- **File attachments** for work orders stored on Google Drive – folder structure `SKAuto Attachments/{WorkOrderId}/` (#69)
- **PSA rate management** (€/h) in `AppConfig`; admin‑editable in Accessory Management (auto‑save) (#67)
- **Per‑task time (minutes) and price override** in WorkOrder detail, with ▲/▼ buttons for 5‑min increments (#67)
- **OrderType enum** updated with four new types and friendly display names:
  - PSA Sur Site, PSA Exterieur, Direct Sur Site, Direct Exterieur (#70)
- **Enhanced Reports** with:
  - Multi‑select client filter (dynamic from date range) with Select All / Deselect All (#71)
  - OrderType filter with friendly names (#71)
  - Time column (`EstimatedMinutes`) with dash (`–`) for zero/null values (#71)
  - Show Price / Show Time checkboxes to control column visibility (#71)
  - Progress indication and wait cursor during generation (#71)
  - Darker alternating row colors and `GridSplitter` for resizable layout (#71)
  - Localised empty report with "No records found" message (#71)
- **HelpView** fully localised in English and French
- **`GoToTodayCommand`** for the Refresh button – resets date picker to today
- **Role‑based access control**: Users cannot access Import, Update Price, Users, Administration

### Fixed
- CSV export/import now preserves foreign key names and original dates
- Vehicle import no longer filters by `Etat` – all VINs are imported
- SQLite foreign key constraint failures during restore
- XAML encoding errors (special characters moved to resources)
- Attachment upload/download with progress bar and busy indicator; prevents closing during operation
- Multiple `MessageBox` and `OpenFileDialog` ambiguity issues
- Reports with no data now generate a proper report with headers and localised "No records" message
- Report language now follows application UI culture (French/English)
- Stale lock detection and crash recovery in cloud sync (#66)

### Changed
- Reports UI redesigned with `GridSplitter` and consistent dropdown widths (140px)
- Refresh button now goes to today’s date instead of reloading current day
- Alternating row colors darkened for better readability (Excel: `#E8E8E8`, PDF: darker grey)
- System tray behaviour – backup on final exit only (not on minimise)

### Security
- BCrypt password hashing (already present)
- Role‑based access control enforced for admin features

---

## [2.0.0] – 2026-07-09

### Added
- Full UI localisation (English/French) with runtime switching
- System tray support – minimise to tray, exit from tray
- Dedicated "Import Vehicles" feature
- Progress bars and wait cursors during long operations
- UI Themes (Light, Dark, Blue) with runtime switching
- Google Drive backup restore from ZIP
- Comprehensive theme resources for all UI controls

### Fixed
- CSV export now includes foreign key names (ClientName, VehicleChassis, AccessoryName)
- Restore preserves original dates
- Vehicle import no longer filters by Etat
- SQLite foreign key constraint failures resolved
- XAML encoding errors fixed (special characters in resources)
- Build errors with missing quotes around `{loc:Translate}`

### Changed
- Backup created only on final exit (not on minimise)
- Language set in Settings saves to AppConfig
- Report colors and fonts customisable
- All UI strings moved to resource files

### Security
- BCrypt password hashing
- Role‑based access control (Admin/User)