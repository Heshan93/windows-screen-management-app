# OrderBuddy Screen Manager v2

OrderBuddy Screen Manager v2 is a lightweight Windows kiosk manager for web-based POS surfaces (Till, Kiosk, Menu, KDS, Collection, Promo).

## Prerequisites

- .NET SDK 8.x
- Windows 10/11

## Build and run

```powershell
dotnet build OrderBuddy.ScreenManager.sln
dotnet run --project src/ScreenManager.App
dotnet test tests/ScreenManager.Tests
```

## Documentation

- [DESIGN.md](DESIGN.md)
- [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)
