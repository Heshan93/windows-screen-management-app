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

### Step 1 — Open an **elevated** PowerShell prompt

Right-click PowerShell → **Run as Administrator**.

### Step 2 — Run the install script

```powershell
powershell -ExecutionPolicy Bypass -File "C:\path\to\scripts\install.ps1"
```

This script does two things:
1. **Checks for WebView2 runtime** — if missing, downloads and installs it silently.
2. **Installs the MSI** — copies the app to `C:\Program Files\OrderBuddy\ScreenManager\` and registers auto-start.

### Step 3 — Reboot or log off/on

The app will start automatically on next login.

### Alternative: Manual MSI install

```powershell
# Elevated prompt
msiexec /i "ScreenManager.Installer.msi" /qn /norestart
```

| Flag | Meaning |
|---|---|
| `/qn` | Fully silent (no UI) |
| `/qb` | Basic UI (progress bar) |
| `/norestart` | Don't reboot after install |

---

## 4. First launch — Setup Wizard

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

## 5. Accessing admin settings (after lockdown)

The app is locked down — you can't close it, alt-tab, or access the desktop normally. To change settings:

### Method 1: Touch screen (no keyboard)
**Tap 5 times in the top-right corner** (140×140 pixel zone) **within 3 seconds** → PIN pad appears.

### Method 2: Keyboard
Press **`Ctrl + Shift + F12`** → PIN pad appears.

### Default PIN: **`1234`**

> **Change the PIN** immediately after first setup via the Admin Settings → General tab.

> **Lockout protection:** Wrong PIN attempts trigger exponential backoff (2s, 4s, 8s... up to 5 minutes).

---

## 6. Admin Settings panel

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

## 7. Adding a new screen

1. **Plug in the monitor.** The app detects it automatically within ~1 second.
2. Open admin settings (5 corner taps or `Ctrl+Shift+F12`, enter PIN).
3. Go to **Displays** tab — the new monitor appears in the Display Wall.
4. Click the new monitor tile → set **Enabled = true**, choose **App Type**, enter the **URL**.
5. Click **Save**. The screen loads immediately.

---

## 8. Removing a screen

Just **unplug the monitor**. The app:
- Closes that screen's window
- Keeps its saved assignment in config
- Other screens are unaffected
- If you plug the same monitor back in, it restores automatically

---

## 9. What happens when things go wrong

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

## 10. File locations

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

## 11. Uninstalling

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

## 12. Upgrading

Just install the new MSI over the old one:

```powershell
msiexec /i "ScreenManager.Installer.msi" /qn /norestart
```

The MSI uses major-upgrade — it removes the old version and installs the new one. Your `config.json` in ProgramData is preserved. Schema migration handles any config format changes automatically.

---

## 13. Troubleshooting

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

## 14. Quick reference card

```
INSTALL:    powershell -ExecutionPolicy Bypass -File scripts\install.ps1
            (elevated prompt required)

OPEN ADMIN: Ctrl+Shift+F12  or  5 taps top-right corner
DEFAULT PIN: 1234

CONFIG:     C:\ProgramData\OrderBuddy\ScreenManager\config.json
LOGS:       C:\ProgramData\OrderBuddy\ScreenManager\logs\

UNINSTALL:  msiexec /x "ScreenManager.Installer.msi" /qn /norestart
UPGRADE:    msiexec /i "ScreenManager.Installer.msi" /qn /norestart

RESET PIN:  Clear "AdminPinHash" in config.json → restarts as 1234
```
