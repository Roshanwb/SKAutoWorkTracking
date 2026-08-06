# SKAuto Work Tracking System – User Guide

![.net](https://img.shields.io/badge/.NET-C%23-007396?style=flat)
![Version](https://img.shields.io/badge/version-2.1.0-blue)

## Quick Start

1. **Launch the application** from the desktop shortcut.
2. **Log in** with your username and password.
3. **View today's work** – automatically loaded on the main dashboard.
4. **Create a work order** – click "New Work Order".
5. **Import data** – use the "Import" button or menu.
6. **Generate reports** – from the Reports window.

---

## Daily Workflow

### Morning (8:00–9:00)

- Open the application and check today's planned work.
- Import any new data (Excel/CSV, ParcCarrières, PDF folder, or Vehicles‑only).
- Print work sheets for mechanics (via Reports → PDF).

### During the Day

- Update work order status and billing status as progress is made.
- Add notes, travel records, and file attachments to work orders.
- Use the **Client Management** and **Vehicle Management** windows to maintain data.

### End of Day (16:00–17:00)

- Mark all work as "Done" (or appropriate status).
- Generate daily reports (Excel/PDF) with filters.
- Optionally upload reports to Google Drive.
- The application automatically creates a backup on exit (if a user is logged in).

---

## Key Features

### 1. Work Order Management

- Create work orders for PSA and direct clients.
- Add multiple tasks (accessories) per work order.
- Track status: Planned → In Progress → Blocked → Done.
- Track **billing status**: Done, To Do, Pending.
- Add travel records (destination, distance, cost).
- Attach one or more files (invoices, PDFs, images) to any work order – stored on Google Drive.
- Edit or delete work orders directly from the main grid.

> **Understanding Work Orders & Tasks**  
> - A Work Order represents **all work for one vehicle on one date**.  
> - All tasks under the same work order share that date.  
> - If the same vehicle returns on another day, create a new work order.  
> - This ensures accurate daily scheduling and reporting.

---

### 2. Importing Data

The system offers **four import methods**:

| Method | When to Use |
|--------|-------------|
| **Excel / CSV** | Standard ParcCarrières files (or similar). All vehicles and clients are imported; work orders are only created for rows with `Etat = "Prêt"` and within the selected date range. |
| **ParcCarrières CSV** | Dedicated parser for the exact ParcCarrières format – same logic as above. |
| **PDF Folder** | Select a folder of PDF work orders. The built‑in Python extractor reads the data; work orders are created with status = Planned. |
| **Import Vehicles Only** | Available in the Vehicle Management window. Imports VIN, Model, and Client; creates clients and vehicles; skips existing VINs; ignores date and Etat. |

> 💡 **Tip:** Use the **"Dry Run"** checkbox to preview what will be imported without making changes.

---

### 3. Reporting

Generate **Excel** or **PDF** reports with powerful filters.

**Report Types:**
- **Work Orders** – full list with tasks, travel, and totals.
- **Tasks** – all accessories grouped by type, with usage and total price.
- **Clients** – list of all clients with contact details.
- **Vehicles** – list of all vehicles with associated client.

**Filters (for Work Orders & Tasks):**
- Date range, task type, work status, specific accessory.
- **Multi‑select client filter** – dynamically populated from orders in the date range.
- **Order Type** – choose from PSA Sur Site, PSA Exterieur, Direct Sur Site, Direct Exterieur, or All.
- **Group by Week** – combines orders by week.
- **Group by Task Type** – for the Tasks report.
- **Show Price / Show Time** – toggle visibility of Amount and Time columns.
- **Summary Only** – hides task‑level details, shows totals only.

> 💡 **Tip:** PDF reports use your custom colors and fonts (configured in Settings). The time column shows `–` for tasks without an estimated duration.

---

### 4. Data Management

#### Clients
- Add, edit, delete clients.
- **Merge duplicate clients** – select multiple clients in Client Management and click "Merge". All vehicles are reassigned to the master client.

#### Vehicles
- Search, add, edit, delete vehicles.
- **Import Vehicles** directly from CSV using the dedicated button.

#### Accessories (Tasks)
- Define tasks with part number, price, time, and task type (Fit, Sell, Remove, Preparation, Travel, Other).
- **Admin‑only PSA rate** (€/h) – editable in the Accessory Management window; auto‑saved.
- **Per‑task time/price override** – in the Work Order detail, adjust the time (▲/▼ buttons) and the price is recalculated from the PSA rate; you can also manually override the price.

---

### 5. Backup & Restore

Protect your data with **multiple backup options**:

| Type | Description |
|------|-------------|
| **Local Backup** | One‑click copy of the `.db` file. Automatic backup on exit (if logged in). Keeps only the latest N backups (configurable). |
| **Export to ZIP (CSV)** | Exports all tables as CSV files in a ZIP archive – portable and restorable on any machine. |
| **Google Drive Sync** | Connect your Drive account to upload backups and restore from Drive backups. Auto‑authenticates on startup if configured. |

**Restore features:**
- Choose a local `.db`, a ZIP file, or a Drive backup.
- Conflict resolution: **Skip** or **Overwrite** existing records.
- **Dry Run** mode to preview changes.
- **Progress bar** and **busy cursor** during long operations.

---

### 6. User & Security

- **Roles:** Admin (full access) and User (limited – no price visibility, no admin menus).
- **Login:** Secure login with BCrypt password hashing.
- **Forgot Password:** Click "Forgot Password?" – a verification code is sent to your registered email.
- **Change Password:** From the main menu (File → Change Password).
- **Admin Only:** Manage users, configure Google Drive, set report styles, bulk price updates.

---

### 7. System Tray

- When logged in, closing the main window **minimizes the app to the system tray** instead of exiting.
- Right‑click the tray icon to **Show** (restore) or **Exit** (creates a backup and shuts down).
- This allows the app to run in the background for quick access.

---

## Troubleshooting

### Common Errors

| Error | Cause / Fix |
|-------|-------------|
| `SQLite Error 19: UNIQUE constraint failed` | Duplicate client/vehicle/accessory. Use "Overwrite" conflict resolution or skip duplicates. |
| `Foreign key constraint failed` | Missing client or vehicle. Import clients and vehicles first. |
| `Email not configured` | Admin must set up SMTP settings in `AppConfig.json` for password reset. |
| `Chassis not found` | Check that the VIN format is correct (17 characters). |

### Data Recovery

- Daily backups: `%AppData%\SKAuto\Backups`.
- Manual backup: use the Backup window.
- Export all data as CSV/ZIP for portability.

---

## Support

For technical issues: **roshanwb@gmail.com**

**Version:** 2.1.0  
**Last Updated:** 06/08/2026