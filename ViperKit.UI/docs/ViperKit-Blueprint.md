# ViperKit — Master Blueprint v3.0

**Owner:** Jeremy Tarkington
**Codename:** ViperKit
**Brand:** VENOMOUSVIPER

---

## 1. Purpose

ViperKit is a **portable, offline-first incident response toolkit** for Windows.

**Target User:** MSP engineers, IT staff, and Tier 1/2 help desk technicians **without** a full-time security team or extensive cybersecurity experience.

ViperKit is a **guided incident workflow** that walks a tech from:

> "I think this box is compromised"
> → Hunt for the bad tool
> → Persist to see what keeps it alive
> → Sweep to see what landed with it
> → Cleanup to remove it safely
> → Harden to prevent reinfection
> → Case Export to document everything

---

## 2. What ViperKit Is NOT

- Not an AV/EDR replacement
- Not automatic malware removal or "one-click fix everything"
- Not cloud-dependent (core features work fully offline)
- Not just a persistence scanner

---

## 3. Core Concept: Case Focus

**Case Focus** is a global list of targets for the current investigation. Think of it as tags that follow you across all tabs.

### Examples of Focus Items
```
ConnectWiseControl.Client.exe
ScreenConnect Client
LogMeIn
powershell.exe
C:\Program Files\EasyHelp\agent.exe
```

### How Focus Works

1. **Focus is additive** — Items get added from any tab, never overwritten
2. **Focus carries across tabs** — Set it in Hunt, see highlights in Persist and Sweep
3. **Focus includes timestamps** — When focus target is a file path, its install time is captured for temporal clustering

### Adding to Focus
- **Hunt:** Find suspicious item → "Add to focus"
- **Persist:** See suspicious persistence → "Add to focus"
- **Sweep:** See related artifact → "Add to focus"

---

## 4. The Workflow

```
┌─────────────────────────────────────────────────────────────────────┐
│  1. HUNT     Find the bad thing (IOC, tool name, file)              │
│              → Set as "Case Focus"                                  │
│                                                                     │
│  2. PERSIST  Check if the bad thing has persistence                 │
│              → Services, autoruns, tasks highlighted if focus match │
│                                                                     │
│  3. SWEEP    Find OTHER things installed at the same time           │
│              → Temporal clustering (±1-8h of focus target install)  │
│              → Add related items to focus                           │
│                                                                     │
│  4. PERSIST  Re-run with expanded focus                             │
│              → Now see persistence for ALL suspicious items         │
│                                                                     │
│  5. CLEANUP  Remove identified threats safely                       │
│              → Preview → Export → Apply → Undo                      │
│                                                                     │
│  6. HARDEN   Prevent reinfection                                    │
│              → Apply security controls based on what was found      │
│                                                                     │
│  7. CASE     Export full case report with timeline                  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 5. Tab Specifications

### 5.1 Dashboard — COMPLETE

**Role:** Entry point overview and case status.

**Features:**
- System snapshot (hostname, user, OS)
- Case ID and event count
- Last event summary
- Case export button
- Status messages

**Behavior:**
- Dashboard reflects the workflow, it doesn't control it
- Updates automatically as events are logged

---

### 5.2 Hunt — COMPLETE

**Role:** Find suspicious tools, binaries, and artifacts.

**IOC Types:**
| Type | What It Does |
|------|--------------|
| File/Path | Check existence, show metadata, calculate hashes (MD5/SHA1/SHA256) |
| Hash | Identify hash type, optional disk scan for matches |
| Domain/URL | DNS lookup, HTTP probe |
| IP | Reverse DNS, ping test |
| Registry | Open key, enumerate values |
| Name/Keyword | Search processes, Program Files, ProgramData, scoped file search |

**Actions:**
- Run Hunt
- Set as Case Focus (adds target to focus list)
- Open Location
- Copy Target
- Save Results

**Case Logging:**
- All hunts logged with IOC, type, and results
- Focus changes logged

---

### 5.3 Persist — COMPLETE

**Role:** Map all persistence mechanisms and highlight focus matches.

**Persistence Locations Scanned:**
| Location | Details |
|----------|---------|
| Registry Run/RunOnce | HKCU + HKLM + Wow6432Node variants |
| Winlogon | Shell, Userinit hijacks |
| IFEO | Debugger hijacks |
| AppInit_DLLs | DLL injection points |
| Startup Folders | All users + current user |
| Services & Drivers | Auto-start only |
| Scheduled Tasks | All tasks with enabled triggers |
| PowerShell Profiles | All profile locations |

**Risk Assessment:**
| Level | Meaning | Color |
|-------|---------|-------|
| CHECK | Needs investigation | Red |
| NOTE | Informational (stale entry, etc.) | Amber |
| OK | Normal/expected | Green |

**High-Signal Detection:**
- IFEO entries (always suspicious)
- Winlogon modifications
- AppInit_DLLs
- PowerShell profiles
- Suspicious paths in autoruns

**Features:**
- Summary panel with triage counts
- Severity color badges
- Focus highlighting (pink border)
- MITRE ATT&CK mapping
- Publisher extraction
- Multiple filters (severity, location, text search)

**Actions:**
- Run Persistence Scan
- Add to Focus
- Add to Case
- Investigate (open location/regedit)
- Copy/Save Results

---

### 5.4 Sweep — COMPLETE

**Role:** Find what else was installed around the same time as focus targets.

**Scan Locations:**
- All user profiles (Desktop, Downloads, AppData, Temp)
- ProgramData
- Startup folders
- Services & drivers

**File Types:**
- Executables (.exe, .dll, .com, .scr, .sys)
- Scripts (.ps1, .vbs, .js, .bat, .cmd)
- Installers (.msi)
- Archives (.zip, .7z, .rar, .iso)

**Severity Levels:**
| Level | Criteria |
|-------|----------|
| HIGH | EXE/script/driver in Desktop/Downloads/Startup, or very recent (<4h) in AppData/Temp |
| MEDIUM | Older EXE/script/driver in AppData/Temp, or DLLs there |
| LOW | Everything else (still logged) |

**Clustering (Key Feature):**

When focus targets are set, Sweep performs clustering:

| Cluster Type | Border Color | Meaning |
|--------------|--------------|---------|
| Focus Match | Pink | Item name/path contains focus target term |
| Time Cluster | Orange | Item modified within ±1-8h of focus target's timestamp |
| Folder Cluster | Blue | Item is in same directory tree as focus target |

**Cluster Window:** Configurable: ±1h, ±2h, ±4h, ±8h

**Features:**
- Summary panel with counts
- Severity color badges
- Focus targets display with timestamps
- "Cluster hits only" filter
- Investigate button (SHA256 hash + VirusTotal lookup)

**Actions:**
- Run Sweep
- Add to Focus
- Add to Case
- Investigate (hash + VirusTotal)
- Open Location
- Copy/Save Results

---

### 5.5 Cleanup — NOT STARTED

**Role:** Safely remove identified threats with undo capability.

**Planned Features:**
- Grouped by Case Target (ScreenConnect, EasyHelp, etc.)
- Artifact types: Services, Tasks, Files/Folders, Registry keys
- Preview → Export → Apply → Undo workflow
- Manual-assist mode (v1) — ViperKit guides, operator removes

**Planned Actions:**
| Action | Export First | Undo Capable |
|--------|--------------|--------------|
| Delete scheduled task | XML export | Yes |
| Stop/disable service | Config export | Yes |
| Quarantine file | Copy to quarantine folder | Yes |
| Remove registry value | Key export | Yes |

---

### 5.6 Harden — NOT STARTED

**Role:** Apply targeted defenses based on what was found.

**Planned Recommendations:**
- For rogue RMMs: AppLocker/SRP rules, firewall restrictions
- For scripts in Temp: Block execution from user-writable locations
- For PowerShell abuse: Enable Script Block Logging

**Planned Features:**
- Standard/Strict profiles
- Rollback capability
- Integration with Case Targets

---

### 5.7 Case — PARTIAL

**Role:** Timeline of everything that happened, exportable report.

**Current Features:**
- Event logging from all tabs
- Text file export

**Planned Features:**
- Chronological timeline view
- Case Targets list
- HTML/Markdown report generation
- Artifacts ZIP bundle
- Operator notes

---

### 5.8 Help — NOT STARTED

**Role:** Quick reference for operators.

**Planned Content:**
- Safety rules ("Preview everything, export before applying")
- Tab-by-tab usage guide
- Log/report locations
- Keyboard shortcuts

---

## 6. Example Workflow: Rogue RMM Cleanup

**Scenario:** Attacker installed ScreenConnect, EasyHelp, and PowerShell 7. ScreenConnect keeps coming back after "uninstall."

### Step 1: Hunt
```
Search: "ScreenConnect"
Find: ScreenConnect.ClientService.exe
Action: Add to Focus

Search: "EasyHelp"
Find: C:\Program Files\EasyHelp\easyhelp.exe
Action: Add to Focus

Search: "PowerShell 7"
Find: C:\Program Files\PowerShell\7\pwsh.exe
Action: Add to Focus
```

### Step 2: Persist
```
Run: Persistence scan
See:
  - ScreenConnect service (highlighted - focus match)
  - Scheduled task pointing to EasyHelp (highlighted)
  - Run key for PowerShell 7 (highlighted)
Action: Add suspicious entries to case log
```

### Step 3: Sweep
```
Settings: 7-day lookback, ±2h cluster window
Run: Sweep scan
See:
  - ScreenConnect.Setup.msi (TIME CLUSTER - installed same time)
  - helper.ps1 in AppData (TIME CLUSTER)
  - EasyHelp DLLs (FOLDER CLUSTER)
Action: Add installer and script to focus
```

### Step 4: Persist (Second Pass)
```
Run: Persistence scan again
See: Additional persistence for newly-added focus items
Result: Complete map of all persistence tied to the infection
```

### Step 5: Cleanup (Coming)
```
See: Grouped checklist by target
  - ScreenConnect: 2 services, 1 task, 3 files
  - EasyHelp: 1 service, 1 folder
  - PowerShell 7: 1 run key
Action: Remove each, mark as removed
```

### Step 6: Harden (Coming)
```
See: Recommendations based on findings
  - Block known RMM paths in AppLocker
  - Block execution in %TEMP%
  - Enable Script Block Logging
Action: Apply controls, mark as applied
```

### Step 7: Case Export
```
Action: Export case report
Result: Complete narrative suitable for ticket/IR documentation
```

---

## 7. Technical Implementation

### Case Manager API
```csharp
CaseManager.StartNewCase()              // Create new case with system snapshot
CaseManager.SetFocusTarget(target, tab) // Add to focus list (appends, doesn't overwrite)
CaseManager.GetFocusTargets()           // Get all focus items
CaseManager.ClearFocus(tab)             // Clear all focus items
CaseManager.AddEvent(tab, action, severity, target, details) // Log event
CaseManager.ExportToFile()              // Generate case report
```

### Data Models

**HuntResult:** Category, Target, Severity, Summary, Details

**PersistItem:** Name, Path, RegistryPath, Source, LocationType, Risk, Reason, MitreTechnique, Publisher, IsFocusHit, RiskBackground, FocusBorderBrush

**SweepEntry:** Category, Severity, Path, Name, Source, Reason, Modified, IsFocusHit, IsTimeCluster, IsFolderCluster, ClusterTarget, SeverityBackground, ClusterBorderBrush

**CaseEvent:** Timestamp, Tab, Action, Severity, Target, Details

---

## 8. UI Design Principles

1. **Summary panels first** — Show triage counts before the wall of data
2. **Color badges** — Instant visual severity recognition
3. **Focus highlighting** — Colored borders for focus/cluster matches
4. **Filter, don't hide** — When filters yield zero, show all with status message
5. **Actions at the bottom** — Consistent button placement across tabs
6. **Case logging everywhere** — Every significant action recorded
7. **Dark theme** — Professional look with VENOMOUSVIPER branding

---

## 9. Next Development Steps

1. **Cleanup tab** — Safe removal workflow with undo
2. **Harden tab** — Security profile application
3. **Case tab** — Full timeline view and report generation
4. **Help tab** — User documentation
5. **Quick Hunt buttons** — Pre-built searches for common threats (RMMs, RATs, etc.)
