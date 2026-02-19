# Development Guide

## Architecture Overview
- **SQLite** – Single‑file database, offline‑first
- **EF Core** – Data access, migrations
- **Repository + Unit of Work** – Abstraction over EF
- **WPF + MVVM** – UI with CommunityToolkit.Mvvm
- **ClosedXML / iText7** – Excel / PDF generation

## Key Decisions
- **No cloud sync** – manual Google Drive sharing
- **Batch validation at EOD** – no continuous entry
- **Password protected PSA rates** – simple encryption

## Testing
- xUnit for unit tests
- SQLite in‑memory for integration tests
- Cover business logic and validation rules