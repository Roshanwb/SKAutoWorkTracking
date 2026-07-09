# Changelog

## [2.0.0] – 2026-07-09

### Added
- Full UI localization (English/French) with runtime switching
- System tray support – minimize to tray, exit from tray
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
- Backup created only on final exit (not on minimize)
- Language set in Settings saves to AppConfig
- Report colors and fonts customizable
- All UI strings moved to resource files

### Security
- BCrypt password hashing
- Role-based access control (Admin/User)