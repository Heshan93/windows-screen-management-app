# OrderBuddy Screen Manager v2 — Runbook / User Manual

> **Audience:** Store managers, IT support, deployment staff.
> **What it does:** Runs web-based POS apps (Till, KDS, Menu, Kiosk, Collection, Promo) full-screen across multiple monitors, locked-down and self-recovering. Works unattended — just plug in the screens and go.

---

## 1. Does it auto-detect screens?

**Yes.** The app automatically detects every monitor connected to the PC on startup, and continuously watches for hot-plug changes (screens added/removed while running). Each monitor is identified by a stable key based on its device path, position, and resolution — so screens keep their assignment even after a reboot or re-plug.

---

## 2. System requirements

| Requirement | Minimum |
|---|---|
| OS | Windows 10 (build 1903+) or Windows 11, 64-bit |
| Runtime | WebView2 Evergreen Runtime (auto-installed by the deploy script) |
| RAM | 4 GB (8 GB recommended for 3+ screens) |
| Screens | 1–N monitors (tested up to 4) |
| Network | Internet access for web apps; works offline with branded fallback |
| Account | Local administrator for install; standard user for daily operation |

---

## 3. Installation

### Easiest way — one double-click (recommended)

1. Copy the **deploy folder** (`artifacts\deploy\`) to the target PC — for example onto the Desktop or into `C:\Temp\`. It contains:
   - `Install-OrderBuddy.bat`
   - `ScreenManager.Installer.msi`
   - `install.ps1`
   - `RUNBOOK.md`
2. **Double-click `Install-OrderBuddy.bat`.**
3. Click **Yes** on the Windows "Do you want to allow this app to make changes?" (UAC) prompt.

That's it. The installer will automatically:
- Install the **WebView2 runtime** if it is missing,
- Install the app to `C:\Program Files\OrderBuddy\ScreenManager\`,
- Register auto-start, and
- Launch the app — the **Setup Wizard** appears on screen.

No command line, no PowerShell knowledge required.

### Alternative — run the PowerShell script directly

From an **elevated** PowerShell prompt (Run as Administrator):

```powershell
powershell -ExecutionPolicy Bypass -File "C:\path\to\install.ps1"
```

This does the same thing as the batch file (WebView2 check → MSI install → launch).

### Alternative — Manual MSI install

```powershell
# Elevated prompt
msiexec /i "ScreenManager.Installer.msi" /qn /norestart
```

| Flag | Meaning |
|---|---|
| `/qn` | Fully silent (no UI) |
| `/qb` | Basic UI (progress bar) |
| `/norestart` | Don't reboot after install |

> After a manual MSI install the app starts on next sign-in (or start it now from
> `C:\Program Files\OrderBuddy\ScreenManager\ScreenManager.App.exe`).

---

## 4. Running the app from the terminal (development)

> Use this during development or to test changes without installing via MSI.
> You need the **.NET 8 SDK** installed and a copy of the source code.

### Quick start — run directly

Open a terminal in the repo root (`v2\`) and run:

```powershell
dotnet run --project src/ScreenManager.App/ScreenManager.App.csproj
```

The app starts exactly as it would when installed. On first run it launches the Setup Wizard.

### Build only (no run)

```powershell
# Debug build (fast, includes debug symbols)
dotnet build src/ScreenManager.App/ScreenManager.App.csproj

# Release build (optimised, what gets shipped)
dotnet build src/ScreenManager.App/ScreenManager.App.csproj -c Release
```

A clean build should report `0 Warning(s)` and `0 Error(s)`.

### Run the tests

```powershell
dotnet test tests/ScreenManager.Tests/ScreenManager.Tests.csproj
```

All tests should show `Passed! - Failed: 0`.

### Run tests with detail (see each test name)

```powershell
dotnet test tests/ScreenManager.Tests/ScreenManager.Tests.csproj --logger "console;verbosity=normal"
```

### Build the whole solution at once

```powershell
dotnet build OrderBuddy.ScreenManager.sln
```

---

## 5. Building and packaging a release

> Use this to produce the MSI installer and the ready-to-ship deploy folder.
> Run from the repo root (`v2\`) in an **elevated PowerShell** prompt.

### Step 1 — Build and package (unsigned)

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-release.ps1
```

This will:
1. Publish a self-contained single-file `ScreenManager.App.exe` for Windows x64
2. Build `ScreenManager.Installer.msi`
3. Assemble `artifacts\deploy\` — the folder you copy to the target PC

Output locations:

| File | Path |
|---|---|
| Compiled exe | `artifacts\publish\win-x64\ScreenManager.App.exe` |
| MSI installer | `src\ScreenManager.Packaging\ScreenManager.Installer\bin\x64\Release\ScreenManager.Installer.msi` |
| **Deploy folder** | `artifacts\deploy\` ← **copy this to the target PC** |

### Step 2 — Install on the target PC

Copy the entire `artifacts\deploy\` folder to the target PC, then double-click `Install-OrderBuddy.bat`.

### Optional — sign the MSI with a certificate

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-release.ps1 `
    -CertificateThumbprint "YOUR_CERT_THUMBPRINT_HERE" `
    -TimeStampUrl "http://timestamp.digicert.com"
```

Replace `YOUR_CERT_THUMBPRINT_HERE` with the SHA1 thumbprint of your code-signing certificate (visible in the Windows Certificate Manager). `signtool.exe` (from the Windows SDK) must be in your PATH.

### Optional — redirect build output to another drive

If your `C:` drive is low on space (publish output is ~180 MB):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-release.ps1 -BuildRoot "E:\OrderBuddyBuild"
```

### Validate the MSI before shipping

```powershell
# Elevated prompt — installs, checks, then uninstalls automatically
powershell -ExecutionPolicy Bypass -File scripts\validate-installer.ps1
```

---

## 6. First launch — Setup Wizard

On first run (no `config.json` exists), the app launches a **6-step setup wizard**:

| Step | What you do |
|---|---|
| 1. Welcome | Read the intro, click Next |
| 2. Device info | Enter a device name and location (e.g. "Counter 1", "Main Floor") |
| 3. Assign displays | The wizard shows all detected monitors. It auto-assigns the first 3 as Till, KDS, Menu |
| 4. URLs & options | Enter the actual web app URLs for each screen. Set rotation/zoom if needed |
| 5. Review | Check everything looks right |
| 6. Lock | Click Finish — settings are saved and the kiosk locks |

After the wizard, each screen shows its assigned web app full-screen.

---

## 7. Accessing admin settings (after lockdown)

The app is locked down — you can't close it, alt-tab, or access the desktop normally. To change settings:

### Method 1: Touch screen (no keyboard)
**Tap 5 times in the top-right corner** (140×140 pixel zone) **within 3 seconds** → PIN pad appears.

### Method 2: Keyboard
Press **`Ctrl + Shift + F12`** → PIN pad appears.

### Default PIN: **`1234`**

> **Change the PIN** immediately after first setup via the Admin Settings → General tab.

> **Lockout protection:** Wrong PIN attempts trigger exponential backoff (2s, 4s, 8s... up to 5 minutes).

---

## 8. Admin Settings panel

After PIN unlock, the settings window opens with these tabs:

### Displays tab
- **Display Wall** — Visual minimap showing your physical monitor layout with live status dots:
  - 🟢 Green = Online
  - 🔵 Blue = Loading
  - 🟡 Gold = Recovering
  - 🔴 Red = Offline/Failed
- Click a monitor tile to edit its settings
- **Identify Screens** button — flashes a big number on each physical screen for 3 seconds

### Per-screen settings

| Setting | What it does |
|---|---|
| **Enabled** | Turn this screen on/off |
| **Name** | Friendly label (e.g. "Till 1", "Kitchen Display") |
| **App Type** | Preset: Till, KDS, Menu, Kiosk, Collection, Promo, Custom |
| **URL** | The web app address to load |
| **Test URL** | Checks if the URL is reachable before saving |
| **Full Screen** | Edge-to-edge (default: on) |
| **Rotation** | 0°, 90°, 180°, 270° — for portrait screens |
| **Zoom** | 25%–500% — enlarge/shrink the page |
| **Reload Interval** | Auto-refresh every N seconds (0 = never) |
| **Startup Delay** | Wait N seconds before loading (stagger launches) |
| **Allow Popups** | Let the web app open new windows |
| **Auto-grant Permissions** | Auto-allow camera/mic/location prompts |
| **Allow Insecure Certs** | Accept self-signed SSL (for local dev servers) |
| **Allowed Domains** | Restrict navigation to listed domains only |
| **Fallback Mode** | What to show when offline: Branded / Image / Custom HTML |

### General tab

| Setting | What it does |
|---|---|
| **Auto-start on Login** | Start the app automatically when Windows starts |
| **Lock System Keys** | Block Win key and Ctrl+Esc (prevents desktop access) |
| **Admin PIN** | Change the admin unlock PIN |
| **Nightly Maintenance** | Auto-reload all screens at a set time (default: 03:30) |
| **Log Level** | Verbosity: Verbose / Debug / Information / Warning / Error |

### Advanced tab
- Retry backoff timing, offline timeout, cache settings.

### Diagnostics tab

| Action | What it does |
|---|---|
| **Refresh** | Shows live per-screen health state, uptime, memory usage |
| **Connectivity Test** | Tests internet + selected screen's web app URL |
| **Restart All Screens** | Reload every screen immediately |
| **Export Support Bundle** | Creates a ZIP on the Desktop with logs + redacted config for troubleshooting |
| **Open Logs Folder** | Opens the log directory in Explorer |

### About tab
- App version, build info.

---

## 9. Adding a new screen

1. **Plug in the monitor.** The app detects it automatically within ~1 second.
2. Open admin settings (5 corner taps or `Ctrl+Shift+F12`, enter PIN).
3. Go to **Displays** tab — the new monitor appears in the Display Wall.
4. Click the new monitor tile → set **Enabled = true**, choose **App Type**, enter the **URL**.
5. Click **Save**. The screen loads immediately.

---

## 10. Removing a screen

Just **unplug the monitor**. The app:
- Closes that screen's window
- Keeps its saved assignment in config
- Other screens are unaffected
- If you plug the same monitor back in, it restores automatically

---

## 11. What happens when things go wrong

### Web app is down / no internet
The screen shows a **branded fallback overlay** with:
- Device name and screen name
- The specific cause (internet down vs. server down vs. captive portal)
- Last successful connection time
- Next retry countdown

The app retries automatically with increasing delays (5s → 10s → 30s → 60s cap). When the connection recovers, the web app reloads automatically.

### App crashes
The Scheduled Task restarts the app automatically (up to 3 retries, 1-minute intervals). All screens restore from saved config.

### Browser tab freezes
A JavaScript heartbeat monitors each screen. If a page freezes, the app detects it and reloads that screen.

### Power loss / hard reboot
The app auto-starts on login, reads saved config, and restores all screens to their assigned URLs.

---

## 12. File locations

| What | Where |
|---|---|
| App executable | `C:\Program Files\OrderBuddy\ScreenManager\ScreenManager.App.exe` |
| Configuration | `C:\ProgramData\OrderBuddy\ScreenManager\config.json` |
| Config backup | `C:\ProgramData\OrderBuddy\ScreenManager\config.bak` |
| Log files | `C:\ProgramData\OrderBuddy\ScreenManager\logs\screen-manager-YYYYMMDD.log` |
| WebView2 cache | `C:\ProgramData\OrderBuddy\ScreenManager\webview2\` |
| Screen status | `C:\ProgramData\OrderBuddy\ScreenManager\screen-status.json` |

Log files rotate daily, keep 14 days, max 10 MB each.

---

## 13. Uninstalling

From an elevated PowerShell:

```powershell
msiexec /x "ScreenManager.Installer.msi" /qn /norestart
```

This removes:
- App files from Program Files
- Auto-start registry key
- Scheduled task

It does **not** remove config or logs in ProgramData (so you can reinstall without losing settings).

---

## 14. Upgrading

Just install the new MSI over the old one:

```powershell
msiexec /i "ScreenManager.Installer.msi" /qn /norestart
```

The MSI uses major-upgrade — it removes the old version and installs the new one. Your `config.json` in ProgramData is preserved. Schema migration handles any config format changes automatically.

---

## 15. Troubleshooting

| Problem | Solution |
|---|---|
| App doesn't start after reboot | Check Task Scheduler for `OrderBuddy.ScreenManager.v2` task. Also check `HKLM\...\Run` key. |
| Screen shows wrong web app | Open admin → Displays → click the screen tile → check URL and DisplayKey binding |
| Can't open admin panel | Try both methods: 5 corner taps AND Ctrl+Shift+F12. If locked out, wait for backoff timer. |
| Forgot PIN | Edit `config.json` manually — clear the `AdminPinHash` field. Next launch resets to `1234`. |
| Screen stuck on fallback | Check internet. Run Diagnostics → Connectivity Test. Check if the web app URL is reachable from the device. |
| High memory usage | The memory watchdog + nightly reload handles this. Set `ORDERBUDDY_MEMORY_WATCHDOG_MB` env var if needed. |
| Need to exit the app | Admin panel → close from settings, or kill `ScreenManager.App.exe` from Task Manager (the scheduled task will restart it — disable the task first if you want it to stay closed). |
| Export logs for support | Admin → Diagnostics → Export Support Bundle → ZIP saved to Desktop |

---

## 16. Quick reference card

```
─── INSTALL / DEPLOY ─────────────────────────────────────────────────────────
INSTALL (easy):    Double-click  artifacts\deploy\Install-OrderBuddy.bat
INSTALL (silent):  msiexec /i "ScreenManager.Installer.msi" /qn /norestart
UPGRADE:           msiexec /i "ScreenManager.Installer.msi" /qn /norestart
UNINSTALL:         msiexec /x "ScreenManager.Installer.msi" /qn /norestart

─── DEVELOPMENT ──────────────────────────────────────────────────────────────
RUN (dev):         dotnet run --project src/ScreenManager.App/ScreenManager.App.csproj
BUILD (debug):     dotnet build src/ScreenManager.App/ScreenManager.App.csproj
BUILD (release):   dotnet build src/ScreenManager.App/ScreenManager.App.csproj -c Release
RUN TESTS:         dotnet test tests/ScreenManager.Tests/ScreenManager.Tests.csproj
BUILD SOLUTION:    dotnet build OrderBuddy.ScreenManager.sln

─── PACKAGING ────────────────────────────────────────────────────────────────
PACKAGE (unsigned): powershell -ExecutionPolicy Bypass -File scripts\package-release.ps1
PACKAGE (signed):   powershell -ExecutionPolicy Bypass -File scripts\package-release.ps1 `
                        -CertificateThumbprint "THUMBPRINT" `
                        -TimeStampUrl "http://timestamp.digicert.com"
VALIDATE MSI:       powershell -ExecutionPolicy Bypass -File scripts\validate-installer.ps1

─── ADMIN / RUNTIME ──────────────────────────────────────────────────────────
OPEN ADMIN:    Ctrl+Shift+F12  OR  5 taps top-right corner
DEFAULT PIN:   1234
RESET PIN:     Clear "AdminPinHash" in config.json → restarts as 1234

─── FILE PATHS ───────────────────────────────────────────────────────────────
CONFIG:        C:\ProgramData\OrderBuddy\ScreenManager\config.json
LOGS:          C:\ProgramData\OrderBuddy\ScreenManager\logs\
WEBVIEW CACHE: C:\ProgramData\OrderBuddy\ScreenManager\webview2\
```
