# ViperKit

**ViperKit** is a portable, offline-first **incident response toolkit** for Windows, built for MSPs and IT teams that don't have a full-time security staff.

It helps an operator:

- **Hunt** IOCs and suspicious artifacts
- Map deep **Persistence** mechanisms
- **Sweep** for recent changes and potential droppers
- **Remediate** in a controlled, reversible way
- **Clean up** leftovers and reset broken hygiene
- **Harden** the endpoint with reversible secure profiles
- Bundle a **Case** report with evidence and logs

Branding and behavior are defined in [`PLAN.md`](./PLAN.md).

---

## Tech Stack

- **.NET 8** + **Avalonia UI** (cross-platform, but Windows-focused)
- Single portable executable
- Runs elevated for full access to system artifacts
- Offline-first — no internet required for core features

---

## Current Status

### Dashboard - Complete
- System snapshot (hostname, user, OS)
- Case ID and event tracking
- Case export functionality
- Quick navigation to tabs

### Hunt - Complete
- **IOC Types:** File/Path, Hash, Domain/URL, IP, Registry, Name/Keyword
- **File/Path:** Checks existence, shows metadata, calculates hashes (MD5/SHA1/SHA256)
- **Hash:** Identifies hash type, optional disk scan for matches
- **Domain/URL:** DNS lookup, HTTP probe
- **IP:** Reverse DNS, ping test
- **Registry:** Opens key, shows values
- **Name/Keyword:** Searches processes, Program Files, ProgramData, scoped file search
- Structured results list with severity levels
- Set case focus for cross-tab filtering
- Open location, copy target, save results
- Full case event logging

### Persist - Complete
- **Registry:** Run/RunOnce (HKCU/HKLM + Wow6432Node)
- **Winlogon:** Shell/Userinit hijacks
- **IFEO:** Debugger hijacks
- **AppInit_DLLs**
- **Startup folders** (all users + current user)
- **Services & Drivers** (auto-start only)
- **Scheduled Tasks**
- **PowerShell profiles**
- **Risk assessment:** OK / NOTE / CHECK with color badges
- **High-signal detection:** IFEO, Winlogon, AppInit, PS profiles flagged
- **MITRE ATT&CK technique mapping**
- **Publisher extraction** from executables
- **Summary panel** with triage counts
- **Filters:** Severity, location type, text search, focus highlighting
- **Actions:** Investigate, add to case, add to focus, open location

### Sweep - Complete
- **Lookback window:** 24 hours, 3 days, 7 days, 30 days
- **Scan locations:** All user profiles (Desktop, Downloads, AppData, Temp), ProgramData, Startup folders
- **Services & drivers scan**
- **File type filtering:** exe, dll, scripts, installers, archives
- **Severity levels:** HIGH / MEDIUM / LOW based on location + type + age
- **Summary panel** with triage counts
- **Severity color badges**
- **Focus integration:**
  - Focus term matching (pink border)
  - Temporal clustering with configurable window (orange border) — finds files installed same time as focus target
  - Folder clustering (blue border)
  - "Cluster hits only" filter
  - Focus targets display with timestamps
- **Actions:** Investigate (SHA256 + VirusTotal), add to case, add to focus, open location
- Copy/save results, case event logging

### Cleanup - Not Started

### Harden - Not Started

### Case - Partial
- Events logged throughout workflow
- Export working

### Help - Not Started

---

## Workflow

The intended workflow for a help desk tech responding to an incident:

```
1. HUNT    → Find the bad thing (IOC, tool name, suspicious file)
             Set it as "case focus"

2. PERSIST → Check if the bad thing has persistence
             (services, autoruns, tasks, etc.)

3. SWEEP   → Find OTHER things installed at the same time
             Temporal clustering shows related artifacts
             Add suspicious items to focus

4. PERSIST → Re-run to check persistence for ALL focus items

5. CLEANUP → (Coming) Remove the bad stuff safely

6. HARDEN  → (Coming) Prevent reinfection

7. CASE    → Export case report with all findings
```

---

## Repository Layout

```text
ViperKit/
  ViperKit.UI/           # Main Avalonia UI application
    Views/               # XAML views and code-behind
    Models/              # Data models (HuntResult, PersistItem, SweepEntry, etc.)
    docs/                # Blueprint documents
  assets/
    Logo.png             # VENOMOUSVIPER branding
  PLAN.md                # Scope & milestones
  README.md              # This file
  BRANDING.md            # Brand guidelines
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

## License

Proprietary — VENOMOUSVIPER / Jeremy Tarkington
