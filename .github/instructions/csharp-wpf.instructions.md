---
description: "C# and WPF coding conventions for OrderBuddy Screen Manager v2. Use when writing or reviewing .cs, .xaml, or .csproj files in this project."
applyTo: ["**/*.cs", "**/*.xaml", "**/*.csproj"]
---

# C# / WPF / .NET 8 Coding Conventions

## Project structure

```
src/
  ScreenManager.App/          # WPF application (entry point, windows, views, DI)
    Views/                    # XAML views
    ViewModels/               # MVVM view models
    Windows/                  # KioskWindow, SettingsWindow, FallbackOverlay
    Converters/               # WPF value converters
    App.xaml / App.xaml.cs    # Application entry
  ScreenManager.Core/         # Platform-independent logic (no WPF refs)
    Config/                   # ConfigService, models, schema validation
    Display/                  # DisplayManager, stable-key, enumeration
    Health/                   # HealthMonitor, heartbeat, connectivity
    Recovery/                 # BackoffRetry, failure taxonomy
    Logging/                  # Serilog setup
    Security/                 # PIN hashing, DPAPI helpers
    Update/                   # Auto-update client
tests/
  ScreenManager.Tests/        # xUnit tests
```

## Naming

- **Namespaces:** `OrderBuddy.ScreenManager.Core.Config`, `OrderBuddy.ScreenManager.App.Views`, etc.
- **Interfaces:** `IConfigService`, `IDisplayManager`, `IHealthMonitor` — always prefix with `I`.
- **Private fields:** `_camelCase` with underscore prefix.
- **Constants:** `PascalCase` (not UPPER_SNAKE).
- **Async methods:** suffix with `Async` (e.g., `LoadConfigAsync`).
- **Events:** past tense for event names (`NavigationCompleted`, `ProcessFailed`).
- **XAML resources:** `PascalCaseKey`.

## Patterns

- **MVVM** for all settings/admin UI. Views bind to ViewModels. No code-behind logic beyond navigation/init.
- **Dependency Injection** via `Microsoft.Extensions.Hosting`. Register services in `App.xaml.cs` or a `Startup` class.
- **CancellationToken** on all async methods that may be cancelled (health checks, network probes, retries).
- **IDisposable / IAsyncDisposable** for WebView2 wrappers and timers.
- **Record types** for immutable config DTOs (e.g., `record ScreenConfig`).
- **System.Text.Json** source-generated serialization for config (no reflection).

## Error handling

- Never swallow exceptions silently. Always log with Serilog at appropriate level.
- Use `try/catch` at service boundaries, not deep in business logic.
- WebView2 event handlers (`ProcessFailed`, `NavigationCompleted`) must never throw — catch and log.
- Config corruption → fall back to last-known-good → safe defaults. Never crash the app.

## WebView2 specifics

- One `CoreWebView2Environment` shared across all kiosk windows (same user-data folder).
- `EnsureCoreWebView2Async()` must be awaited before accessing `CoreWebView2`.
- Subscribe to `ProcessFailed`, `NavigationCompleted`, `WebResourceResponseReceived` per window.
- Disable settings via `CoreWebView2Settings`: `AreDevToolsEnabled = false`, `AreDefaultContextMenusEnabled = false`, `IsStatusBarEnabled = false`, etc.
- Use `CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync()` for JS heartbeat injection.

## Logging

- Use Serilog structured logging: `Log.Information("Screen {ScreenId} loaded {Url}", id, url)`.
- Levels: `Verbose` debug noise, `Debug` dev detail, `Information` lifecycle events, `Warning` recoverable issues, `Error` failures, `Fatal` app-crash.
- Never log secrets (PIN, tokens). Redact URLs if they contain auth query params.

## Testing

- xUnit + FluentAssertions.
- Unit test Core services (config validation, backoff logic, display-key derivation).
- Integration test WebView2 only where practical (prefer manual verification for UI).
- Name tests: `MethodName_Scenario_ExpectedResult`.

## Performance

- Avoid allocations in hot paths (health-check timers, heartbeat handlers).
- Use `ValueTask` for frequently-completing async paths.
- Pool `HttpClient` instances (singleton or `IHttpClientFactory`).
- Minimize XAML visual tree for kiosk windows (just the WebView2 + fallback overlay).

## Do NOT

- Do NOT reference `ScreenManager.App` from `ScreenManager.Core` (one-way dependency).
- Do NOT use `Thread.Sleep` or `Task.Delay` for retry logic — use a proper backoff timer/service.
- Do NOT hard-code paths; use `Environment.GetFolderPath` / `%ProgramData%` constants.
- Do NOT use `dynamic` or reflection for config serialization — use source generators.
