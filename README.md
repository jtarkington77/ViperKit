# ViperKit

**ViperKit** is a portable, offline-first **incident response toolkit** for Windows, built for MSPs and IT teams that don't have a full-time security staff.

**Target Users:** Tier 1/2 help desk technicians and MSP engineers with limited cybersecurity experience who need a guided workflow to investigate and remediate compromised endpoints.

---

## What ViperKit Does

ViperKit provides a **guided incident workflow** that walks a tech from:

> "I think this box is compromised"
> → **Hunt** for the bad tool
> → **Persist** to see what keeps it alive
> → **Sweep** to see what landed with it
> → **Cleanup** to remove it safely
> → **Harden** to prevent reinfection
> → **Case Export** to document everything

---

## Tech Stack

- **.NET 9** + **Avalonia UI** (cross-platform framework, Windows-focused features)
- Single portable executable
- Runs elevated for full access to system artifacts
- **Offline-first** — no internet required for core features
- Optional VirusTotal integration for hash lookups

---

## Current Status

| Tab | Status | Description |
|-----|--------|-------------|
| **Dashboard** | Complete | System snapshot, case management, demo mode (planned) |
| **Hunt** | Complete | IOC searching with 6 input types |
| **Persist** | Complete | Deep persistence mapping with risk assessment |
| **Sweep** | Complete | Recent file radar with temporal clustering |
| **Cleanup** | Complete | Safe removal with undo capability |
| **Harden** | Not Started | Security profile application |
| **Case** | Partial | Event timeline, text export (HTML/ZIP planned) |
| **Help** | Not Started | In-app documentation |

---

## Features

### Dashboard
- System snapshot (hostname, user, OS)
- Case ID and event tracking
- Case export functionality
- **Demo Mode** (planned) — guided walkthrough with test artifacts

### Hunt
- **IOC Types:** File/Path, Hash, Domain/URL, IP, Registry, Name/Keyword
- **File/Path:** Checks existence, shows metadata, calculates hashes (MD5/SHA1/SHA256)
- **Hash:** Identifies hash type, optional disk scan for matches
- **Domain/URL:** DNS lookup, HTTP probe
- **IP:** Reverse DNS, ping test
- **Registry:** Opens key, shows values
- **Name/Keyword:** Searches processes, Program Files, ProgramData, scoped file search
- Structured results list with severity levels
- Set case focus for cross-tab filtering

### Persist
- **Registry:** Run/RunOnce (HKCU/HKLM + Wow6432Node)
- **Winlogon:** Shell/Userinit hijacks
- **IFEO:** Debugger hijacks (Image File Execution Options)
- **AppInit_DLLs**
- **Startup folders** (all users + current user)
- **Services & Drivers** (auto-start only)
- **Scheduled Tasks**
- **PowerShell profiles**
- **PowerShell History Analysis** (planned) — scan command history for suspicious activity
- **Risk assessment:** OK / NOTE / CHECK with color badges
- **High-signal detection:** IFEO, Winlogon, AppInit, PS profiles flagged
- **MITRE ATT&CK technique mapping**
- **Publisher extraction** from executables
- Focus highlighting with colored borders

### Sweep
- **Lookback window:** 24 hours, 3 days, 7 days, 30 days
- **Scan locations:** All user profiles, ProgramData, Startup folders, Services
- **File types:** Executables, DLLs, scripts, installers, archives
- **Severity levels:** HIGH / MEDIUM / LOW based on location + type + age
- **Focus integration:**
  - Focus term matching (pink border)
  - Temporal clustering ±1-8h configurable (orange border)
  - Folder clustering (blue border)
- **Investigate:** SHA256 hash + VirusTotal lookup

### Cleanup
- Queue items from Persist and Sweep tabs
- **Safe removal actions:**
  - Quarantine files (move to safe location)
  - Disable services (stop + set to disabled)
  - Disable scheduled tasks
  - Backup and delete registry keys
- **Full undo capability** — restore quarantined files, re-enable services
- Journal-based tracking for audit trail
- Preview before apply workflow

### Case
- Chronological event timeline from all tabs
- Focus targets tracking
- Text file export
- HTML report generation (planned)
- Artifacts ZIP bundle (planned)

---

## Core Concept: Case Focus

**Case Focus** is a global list of targets that follows you across all tabs. When you find something suspicious, add it to focus and it will be highlighted everywhere.

```
Examples:
- ConnectWiseControl.Client.exe
- ScreenConnect Client (service name)
- C:\Program Files\EasyHelp\agent.exe
- powershell.exe
```

**How it works:**
1. **Hunt** — Find suspicious item → "Add to focus"
2. **Persist** — See highlighted persistence entries matching focus
3. **Sweep** — See temporal clustering (files created ±1-8h of focus target)
4. **Cleanup** — Queue focus-related items for removal

---

## Workflow Example: Rogue RMM Cleanup

**Scenario:** Attacker installed ScreenConnect that keeps coming back after uninstall.

```
1. HUNT    → Search "ScreenConnect", find the executable
             → Set as case focus (captures path + timestamp)

2. PERSIST → Run scan, see highlighted:
             - ScreenConnect service (focus match)
             - Scheduled task pointing to ScreenConnect

3. SWEEP   → See files clustered around install time:
             - ScreenConnect.Setup.msi (TIME CLUSTER)
             - helper.ps1 in AppData (TIME CLUSTER)
             → Add installer and script to focus

4. PERSIST → Re-run, now see persistence for ALL focus items

5. CLEANUP → Queue all items, preview, execute
             - Service disabled
             - Task disabled
             - Files quarantined

6. CASE    → Export report for ticket documentation
```

---

## Repository Layout

```text
ViperKit/
├── ViperKit.UI/           # Main Avalonia UI application
│   ├── Views/             # XAML views and code-behind
│   │   ├── MainWindow.axaml
│   │   ├── MainWindow.Hunt.cs
│   │   ├── MainWindow.Persist.cs
│   │   ├── MainWindow.Sweep.cs
│   │   ├── MainWindow.Cleanup.cs
│   │   └── Sweep.Services.cs
│   ├── Models/            # Data models
│   │   ├── CaseManager.cs
│   │   ├── HuntResult.cs
│   │   ├── PersistItem.cs
│   │   ├── SweepEntry.cs
│   │   ├── CleanupItem.cs
│   │   └── CleanupJournal.cs
│   └── docs/              # Blueprint documents
├── assets/
│   └── Logo.png           # VENOMOUSVIPER branding
├── PLAN.md                # Scope & milestones
├── README.md              # This file
└── BRANDING.md            # Brand guidelines
```

---

## Building

```bash
cd ViperKit.UI
dotnet build
dotnet run
```

For a portable release:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Planned Features

### Demo Mode (Dashboard)
- Guided walkthrough for training and proof-of-concept
- Creates harmless test artifacts (files, registry keys, scheduled task)
- Step-by-step guide through Hunt → Persist → Sweep → Cleanup
- Full cleanup at demo end

### PowerShell History Analysis (Persist)
- Scans PSReadLine history for Windows PowerShell and PowerShell 7
- All user profiles when running elevated
- Risk scoring: HIGH (download+execute, encoded commands), MEDIUM (policy changes), LOW (other)
- Base64 decoding for encoded commands

### Enhanced Case Export
- HTML report with timeline visualization
- Markdown export for documentation
- Artifacts ZIP bundle with quarantined files

---

## License

Proprietary — VENOMOUSVIPER / Jeremy Tarkington
