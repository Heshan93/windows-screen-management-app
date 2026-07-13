# OrderBuddy Screen Manager v2

## What is this project?
A lightweight .NET 8 WPF + WebView2 kiosk application that manages web-based POS applications (Till, Kiosk, Menu, KDS, Collection, Promo) across multiple Windows displays. Designed for unattended production POS environments.

## Key docs
- `DESIGN.md` — Full architecture, UI/UX, technology decisions, edge cases, validation checklist.
- `DEVELOPMENT_PLAN.md` — 13 phases (P0–P12), 96 trackable tasks with acceptance criteria and dependencies.

## Technology stack
- .NET 8 LTS, C# 12, WPF (MVVM), WebView2 (Evergreen)
- Serilog (logging), System.Text.Json (config), WiX v4 (installer), Velopack (updates)
- Config stored in `%ProgramData%\OrderBuddy\ScreenManager\`

## Build & run
```powershell
dotnet build src/ScreenManager.App
dotnet run --project src/ScreenManager.App
dotnet test tests/ScreenManager.Tests
dotnet publish src/ScreenManager.App -c Release -r win-x64 --self-contained
```

## Important rules
- This is a **clean-slate v2** — do NOT copy patterns from `../screen_runner/` (v1, Python/Qt).
- Follow DESIGN.md architecture decisions. Do not deviate without updating the doc.
- Track every task via DEVELOPMENT_PLAN.md checkboxes and progress dashboard.
- Use the `screen-manager-v2` agent (in `.github/agents/`) for development sessions.
