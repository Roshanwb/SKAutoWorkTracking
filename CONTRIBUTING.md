# Contributing to SK Auto Work Tracking

## Development Setup
1. Install .NET 8 SDK
2. Clone repository
3. Run `dotnet restore`
4. Build with `dotnet build`

## Coding Standards
- Use nullable reference types (`#nullable enable`)
- Follow Microsoft .NET coding conventions
- All public members must have XML comments
- Use `CommunityToolkit.Mvvm` for ViewModels

## Pull Request Process
1. Create feature branch from `develop`
2. Write tests for new functionality
3. Ensure all tests pass
4. Update documentation
5. Submit PR against `develop`