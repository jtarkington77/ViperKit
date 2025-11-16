# ViperKit – Incident Response Portable Kit (Blueprint)

**Goal:**  
A GUI-based, portable incident response kit for non-cyber help desk techs to clean an infected workstation properly:

- Find indicators of compromise (IOC).
- Find and remove persistence.
- Sweep for recent changes in risky locations.
- Clean artifacts safely (with quarantine).
- Harden the system after remediation.
- Auto-build an exportable case file showing what was found, what was done, and the final clean state.

Everything is designed to be **clickable, filterable, and readable** for Tier 1 / Tier 2 techs.

---

## Core Tabs & Workflow

Recommended run order (NOT forced in UI):

1. Dashboard  
2. Hunt  
3. Persist  
4. Sweep  
5. Cleanup  
6. Harden  
7. Case  
8. Help  

The Dashboard will clearly show this order and explain why, but the user can jump around as needed.

---

## 1. Dashboard (Start Here)

**Purpose:** Entry point + context + case control.

### Responsibilities

- Show host snapshot:
  - Hostname
  - Logged-in user
  - OS + build
  - Uptime
- “Start Case” button:
  - Creates a case ID (timestamp + host)
  - Creates a case folder (e.g. `Cases\{CaseId}\`)
  - Creates a case log file (JSON + human-readable text)
- Show current case status:
  - Not started / In progress / Completed
- Quick navigation buttons:
  - Open **Hunt**
  - Open **Persist**
  - Open **Sweep**
  - Open **Cleanup**
  - Open **Harden**
  - Open **Case**
- Instructions:
  - “Recommended order” list: Dashboard → Hunt → Persist → Sweep → Cleanup → Harden → Case
  - Short explanation of each tab.

### Notes

- Dashboard is the “home base”.
- Case logging framework is initialized here and used by all other tabs.

---

## 2. Hunt (IOC Discovery)

**Purpose:** Given a known bad thing (hash, file, domain, etc.), find where it exists on the system.

### Inputs

- IOC textbox: paste:
  - File path (full or partial)
  - Hash (MD5/SHA1/SHA256)
  - Domain / URL
  - IP address
  - Registry key or value name
- IOC type dropdown:
  - Auto-detect
  - File path
  - Hash
  - Domain/URL
  - IP address
  - Registry
- Optional scope folder:
  - Limit file scans to a folder tree (for speed / targeted hunts)

### Surfaces to search

- Filesystem:
  - Whole drive or scoped folder
  - Matching names, hashes, or paths
- Registry:
  - HKLM + HKCU core hives
  - Autorun keys
  - IFEO
  - Services keys
- Services & Drivers:
  - Service name, ImagePath
- Scheduled Tasks:
  - Task names, actions (paths, arguments)
- Hosts file, browser-related locations, etc. (future expansion)

### Output

Grid/table:

- Category (File / Registry / Service / Task / Other)
- Name (filename, service name, task name, reg value)
- Path / Location (full path or registry path)
- Details (hash, command line, arguments)
- Severity (LOW / MEDIUM / HIGH)
- Reason (why it’s flagged)
- Actions:
  - Open location (File Explorer / regedit)
  - Add to Case evidence
  - Send to Cleanup (mark for removal/quarantine)

### Case Logging

Every hunt:
- Logs the IOC searched.
- Logs hits found (category + path + severity).
- If user adds something to evidence or cleanup, that action is added to the case log.

---

## 3. Persist (Persistence Map)

**Purpose:** Map all common persistence mechanisms, not just Run keys and Startup. This is where we find how the malware comes back.

### Surfaces

- Registry Autoruns:
  - Run / RunOnce (HKLM, HKCU)
  - RunServices / RunServicesOnce
- Winlogon:
  - Shell
  - Userinit
  - GINAs / Authentication packages
- IFEO:
  - `Image File Execution Options` Debugger hijacks
- Services:
  - Auto / System / Manual
  - Third-party services
- Drivers:
  - Boot/system/non-MS drivers
- Scheduled Tasks:
  - All tasks from `C:\Windows\System32\Tasks`
  - Registered tasks from Task Scheduler
- Startup folders:
  - Current user
  - All users
- WMI event consumers (future phase).
- PowerShell profiles:
  - All-host, all-user profiles
- Browser extensions (Chrome/Edge/Firefox) (future phase).
- Office startup macros (Word/Excel/Outlook) (future phase).

### Output

Grid/table:

- Category (Reg:Run, Service, Driver, Task, Startup, Winlogon, etc.)
- Name (value name, service name, task name)
- Path / Command (exe path / script / argument)
- Publisher / Company (if available)
- Severity:
  - HIGH: non-Microsoft from user-writable path, missing binary, suspicious locations
  - MEDIUM: unknown publisher in autorun/persistence areas
  - LOW: Microsoft or common legit entries
- Reason:
  - Human-readable explanation for severity.
- Actions:
  - Open location
  - View properties
  - Quarantine / disable (moves file to Quarantine and disables the autorun)
  - Add to Case evidence

### Case Logging

- Every persistence scan writes:
  - Count per category
  - High/medium findings
- Every change (disable, quarantine) is logged with:
  - Before state
  - After state

---

## 4. Sweep (Recent Change Radar)

**Purpose:** Show recent changes in risky areas that might represent installers, droppers, or new persistence artifacts.

### File-based sweep

Look in:

- Desktop (all users)
- Downloads (all users)
- AppData (Local, Roaming, LocalLow)
- Temp folders
- ProgramData
- Startup folders
- System32\Tasks (task definition files)
- Other high-risk locations (to be defined as we refine logic).

Filter by time window:

- 24 hours
- 3 days
- 7 days
- 30 days

Filter by extension / type:

- Executables: `.exe`, `.com`, `.scr`
- Scripts: `.ps1`, `.js`, `.jse`, `.vbs`, `.bat`, `.cmd`
- Drivers: `.sys`
- DLLs
- Archives/installers: `.zip`, `.7z`, `.rar`, `.iso`, `.msi`

### Registry & services sweep

- New or recently modified services/drivers.
- New Run / RunOnce entries.
- New scheduled tasks.
- New IFEO entries.

### Output

Grid/table:

- Category: File / Service / Driver / Task / Registry
- Severity:
  - HIGH: new EXE/script/driver in Desktop/Downloads/Startup or very recent in AppData/Temp.
  - MEDIUM: older but suspicious in AppData/Temp, DLLs in those paths, non-MS services/drivers changed recently.
  - LOW: everything else that’s still worth seeing.
- Name: filename, service name, task name, etc.
- Path / Location
- Modified time (and/or created time)
- Reason: human explanation:
  - e.g. `user-writable location, executable file, modified within last 4h`.
- Actions:
  - Open location
  - Add to Case evidence
  - Send to Cleanup

### Case Logging

- Records the window used and the counts.
- Stores the list of MED/HIGH items (not all LOW noise).
- If user marks items as interesting or adds to evidence, that’s logged.

---

## 5. Cleanup (Post-removal Hygiene)

**Purpose:** Safely remove bad stuff and clean up leftover junk, with quarantine instead of hard delete.

### Actions

- Quarantine selected files:
  - Move to `Cases\{CaseId}\Quarantine\`
  - Record original path + hash
- Disable / remove persistence:
  - Remove autorun registry entries (with backup in case file).
  - Disable or delete scheduled tasks (export before delete).
  - Disable or delete services (record original config).
- Temp/Cache cleanup:
  - Option to clear Temp folders.
  - Cleanup browser cache/profile leftovers (future).
- Browser extension removal (future).
- Orphaned services/tasks cleanup where pointers are broken.

### Case Logging

- Every cleanup action:
  - Who/what: type (file/registry/service/task)
  - From where: original path/registry key
  - Action: quarantined / disabled / removed
  - Timestamp

---

## 6. Harden (Aftercare)

**Purpose:** Lock down the host after infection so it’s less likely to get hit again.

### Profiles

1. Standard:
   - Ensure Defender/AV is enabled.
   - Ensure firewall is on.
   - Disable Office macros by default.
   - Block Office from running macros in external files (where applicable).
2. Strict:
   - All Standard steps.
   - Disable PowerShell v2, logging configuration.
   - Tighten RDP settings.
   - Disable unsigned startup items.
   - Additional hardening where possible.

### Output

- Shows which hardening actions were applied.
- Allows toggling certain actions on/off before applying.

### Case Logging

- Lists every applied setting change and its before/after where feasible.

---

## 7. Case (Evidence & Reporting)

**Purpose:** Single place to view everything done and export a professional incident report.

### Contains

- General:
  - Case ID
  - Hostname
  - User
  - OS
  - Time opened / time closed
- Findings:
  - IOC hits from Hunt
  - Persistence entries that were flagged and/or changed
  - Sweep findings that were promoted to evidence
- Actions:
  - Files quarantined (with hashes)
  - Services/tasks/registry entries changed or removed
  - Cleanup/hygiene steps run
  - Harden profile applied + settings
- Before/After:
  - Summary of persistence map before cleanup vs after cleanup.
  - Summary of sweep before/after if a second sweep is run as validation.

### Export

- Export options:
  - HTML
  - PDF (later)
  - Zip case directory (logs + evidence)
- Export content is **curated**:
  - No raw dumps of every scan line
  - Clear narrative:
    - “What happened?”
    - “What was found?”
    - “What was done?”
    - “What is the current state?”

---

## 8. Help (Guidance & Safety)

**Purpose:** Built-in documentation aimed at Tier 1/2 techs.

### Includes

- Tab-overview:
  - What each tab does
  - When to use it
- “How to read this screen” examples:
  - Sample persistence entry annotated
  - Sample sweep entry annotated
- Safety rules:
  - Always quarantine first, never hard delete.
  - When to escalate to a security team.
- Where logs and case files are stored.

---

## UI Design Principles

- Dark theme with ViperKit branding (logo + accent color).
- Clean, modern layout; **no raw walls of text**.
- Each tab uses clear sections and grids/tables.
- Columns are sortable (at least by Severity, Name, Category, Modified).
- Filters:
  - Severity filters
  - Search bar per tab
- One-click actions:
  - Open location
  - Add to case
  - Quarantine/remove
- No prior cybersecurity experience required to navigate.

---

## Non-Goals

- This is not an EDR/AV replacement.
- This is not fully autonomous malware removal.
- This is not intended to process or manage *servers* or domain-wide remediation.
- This is not a forensics suite; it’s a practical help-desk-grade IR kit with guardrails.
