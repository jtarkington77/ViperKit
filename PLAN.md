# ViperKit — Scope & Plan v4.0

**Owner:** Jeremy Tarkington
**Codename:** ViperKit
**Brand:** VENOMOUSVIPER (teal-on-dark cyber theme)
**Last Updated:** 2024-11-24
**Release Status:** v1.0 - Ready for GitHub Release

---

## 0. Branding & Theme

- Primary background: `#0B1518` (dark teal)
- Accent / highlight: `#00FFFA` (Viper teal)
- Font style: clean, sans-serif (professional look)
- Logo: `Assets/logo.png` + `Assets/viperkit.ico` (snake head + VENOMOUSVIPER branding)

**Rules**
- App looks like a serious incident response tool
- Logo + "ViperKit" branding visible on main window
- Consistent theme across all tabs
- Professional color-coded severity indicators

---

## 1. Purpose (What ViperKit Is)

ViperKit is a **portable, offline-first incident response toolkit** for Windows.

**Target user:** MSP engineers, IT staff, and Tier 1/2 help desk technicians **without** a full-time security team or extensive cybersecurity experience.

ViperKit provides a **guided incident workflow**:

> "I think this box is compromised"
> → **Hunt** for the bad tool
> → **Persist** to see what keeps it alive
> → **Sweep** to see what landed with it
> → **Cleanup** to remove it safely
> → **Harden** to prevent reinfection
> → **Case Export** to document everything

---

## 2. Non-Goals (What ViperKit Is Not)

- Not an AV/EDR replacement
- Not automatic malware removal or "one-click fix everything"
- Not cloud-dependent (core features work fully **offline**)
- Not just a persistence scanner

---

## 3. Tech Stack

- **.NET 9** + **Avalonia UI 11.3**
- **QuestPDF** for professional PDF reports
- Cross-platform framework with Windows-focused features
- Single self-contained executable via `dotnet publish`
- Runs locally, elevated, fully portable
- No internet required for core features (VirusTotal optional)

---

## 4. Core Workflow

The "Case Focus" concept is central to ViperKit's workflow:

```
1. HUNT    → Find suspicious item (IOC, malware, RMM)
             → Set as "Case Focus" (captures path + timestamp)
             → Recent searches saved in history dropdown

2. PERSIST → Scan for persistence mechanisms
             → Items matching focus are highlighted (colored borders)
             → Add suspicious items to cleanup queue

3. SWEEP   → Scan recent file changes
             → Temporal clustering finds files created ±1-8h of focus target
             → "What else was installed at the same time?"
             → Add related items to focus

4. PERSIST → Re-run with expanded focus
             → Now see persistence for ALL suspicious items

5. CLEANUP → Remove identified threats safely
             → Confirmation dialog appears
             → Preview → Execute → Undo if needed

6. HARDEN  → Prevent reinfection (Planned)

7. CASE    → Export professional PDF report
             → Executive summary + critical next steps
             → Complete timeline for documentation
```

---

## 5. Milestone Status

### ✅ M0 — Plan & Wireframe: **COMPLETE**
- Approved scope + UX map + acceptance tests
- Branding colors and assets defined

### ✅ M1 — Dashboard: **COMPLETE**
- System snapshot (hostname, user, OS)
- Case ID and event tracking
- Case management (start new / load existing)
- Baseline capture and comparison
- Admin status detection with warning banner
- Scrollable content for multiple cases
- Status display

### ✅ M2 — Hunt MVP: **COMPLETE**
- IOC input with type selector (Auto, File/Path, Hash, Domain/URL, IP, Registry, Name/Keyword)
- File/Path: existence check, metadata, multi-hash calculation (MD5/SHA1/SHA256)
- Hash: type identification, optional disk scan
- Domain/URL: DNS lookup, HTTP probe
- IP: reverse DNS, ping test
- Registry: key enumeration, value display
- Name/Keyword: process search, install folder search, scoped file search
- Structured results list with severity levels
- Case focus integration with highlighting
- **Hunt history dropdown** - Last 10 searches saved and accessible
- Actions: open location, copy target, save results, set focus
- Full case event logging

### ✅ M3 — Persist MVP: **COMPLETE**
- Registry Run/RunOnce (HKCU/HKLM + Wow6432Node)
- Winlogon Shell/Userinit hijacks
- IFEO Debugger hijacks
- AppInit_DLLs
- Startup folders (all users + current user)
- Services & Drivers (auto-start only)
- Scheduled Tasks
- PowerShell profiles
- Risk assessment (OK/NOTE/CHECK) with color badges
- High-signal detection for suspicious locations
- MITRE ATT&CK technique mapping
- Publisher extraction from executables
- Summary panel with triage counts
- Filters: severity, location type, text search
- Focus highlighting with colored borders
- Actions: investigate, add to case, add to focus, add to cleanup

### ✅ M4 — Sweep MVP: **COMPLETE**
- Lookback windows: 24h, 3d, 7d, 30d
- Scan locations: all user profiles, ProgramData, Startup folders, Services
- File type filtering (exe, dll, scripts, installers, archives)
- Severity levels (HIGH/MEDIUM/LOW) based on location + type + age
- Summary panel with triage counts
- Severity color badges
- Focus integration:
  - Focus term matching (pink border)
  - Temporal clustering ±1h to ±8h configurable (orange border)
  - Folder clustering (blue border)
  - "Cluster hits only" filter
  - Focus targets display with timestamps
- Actions: investigate (SHA256 + VirusTotal), add to case, add to focus, add to cleanup
- Copy/save results, case event logging

### ✅ M5 — Cleanup MVP: **COMPLETE**
- Queue items from Persist and Sweep tabs
- **Confirmation dialogs** for all destructive actions
- Execute all pending / execute selected
- Quarantine files (move to safe location)
- Disable services (stop + set Start=4)
- Disable scheduled tasks (schtasks /Disable)
- Backup and delete registry keys (reg export + delete)
- Full undo capability:
  - Restore quarantined files
  - Re-enable services
  - Restore registry keys from backup
- Journal-based tracking with JSON persistence
- Stats display (total, pending, completed, failed)
- Open quarantine folder
- Remove from queue / clear queue

### ⏳ M6 — Harden MVP: **PLANNED**
- Standard/Strict security profiles
- Defender preference toggles
- Script engine restrictions
- RDP/NLA configuration checks
- Rollback capability
- Integration with case findings

### ✅ M7 — Case MVP: **COMPLETE**
- Events logged throughout workflow
- Focus targets tracking
- Chronological timeline view
- **Professional PDF report generation** with:
  - Executive summary with risk breakdown (HIGH/MEDIUM/LOW counts)
  - **CRITICAL NEXT STEPS** section (password resets, monitoring, patching, etc.)
  - Scans performed with totals
  - Key findings (filtered by severity)
  - Remediation actions taken
  - Hardening applied (when implemented)
  - Baseline information
  - Timeline of key events
- Text file export
- JSON event logs
- Auto-save case data

### ✅ M8 — Help: **COMPLETE**
- **Searchable help system** with real-time filtering
- Safety rules (prominently displayed)
- Quick start guide
- Tab usage instructions (all 7 tabs documented)
- Tips & best practices
- FAQ section
- File locations reference
- Keyboard shortcuts (coming)
- Collapsible sections for easy navigation
- Version info with GitHub link

### ⏳ M9 — Demo Mode: **PLANNED**
- Guided walkthrough for training and POC
- Create harmless test artifacts
- Step-by-step guide through full workflow
- Auto-cleanup at demo end

### ⏳ M10 — PowerShell History: **PLANNED**
- Scan PSReadLine history files
- Windows PowerShell 5.1 + PowerShell 7 support
- Risk scoring (HIGH/MEDIUM/LOW)
- Base64 decoding for encoded commands
- Pattern matching for suspicious commands

---

## 6. Quality of Life Features (v1.0)

### ✅ **Admin Status Detection**
- Automatically detects if running as Administrator
- Shows prominent warning banner if NOT admin
- Explains required privileges

### ✅ **Hunt History**
- Remembers last 10 IOC searches
- Dropdown for quick re-search
- Persists between sessions

### ✅ **Confirmation Dialogs**
- Execute All cleanup - requires confirmation
- Execute Selected cleanup - shows item details
- Warns about irreversible actions
- Reminds to export case report

### ✅ **Scrollable Interface**
- Dashboard scrolls for multiple cases
- All tabs support vertical scrolling
- Fixed-height lists with internal scrolling

### ✅ **Professional PDF Reports**
- QuestPDF-powered generation
- Executive summary with statistics
- Critical manual recommendations
- Color-coded findings
- Complete timeline

---

## 7. Current Feature Status

| Tab | Status | Key Features |
|-----|--------|-------------|
| **Dashboard** | ✅ Complete | System snapshot, case management, baseline, admin detection, scrollable |
| **Hunt** | ✅ Complete | 6 IOC types, focus setting, history dropdown, structured results |
| **Persist** | ✅ Complete | 8 persistence locations, risk assessment, MITRE mapping, focus highlighting |
| **Sweep** | ✅ Complete | Time clustering, severity classification, VirusTotal integration |
| **Cleanup** | ✅ Complete | Safe removal, undo capability, confirmation dialogs, journal tracking |
| **Harden** | ⏳ Planned | Security profiles, rollback capability |
| **Case** | ✅ Complete | Timeline, PDF/text export, focus tracking, auto-save |
| **Help** | ✅ Complete | Searchable docs, safety rules, FAQ, tips |

---

## 8. Safety & Rollback

- **Preview-first workflow** - See what will be changed before applying
- **Confirmation dialogs** for all destructive actions
- **Undo journal** - Every action tracked for rollback
- **Quarantine system** - Files moved, not deleted
- **Registry backups** - .reg files created before deletion
- **Service rollback** - Original state recorded
- **Case export** - Always export before remediation

**File Locations:**
- Logs: `C:\ProgramData\ViperKit\Cases\{CaseId}\`
- Quarantine: `Documents\ViperKit\Quarantine\{CaseId}\`
- Reports: `Documents\ViperKit\Reports\`

---

## 9. Packaging & Deployment

### Self-Contained Exe
- Single portable executable (no .NET install required)
- Run from any location
- Writes to documented paths only
- Must run elevated (Administrator)

### Internet Requirements
- **Core features**: None (fully offline)
- **Optional features**: VirusTotal hash lookups (user-initiated)

---

## 10. Next Development Priorities

### High Priority
1. ✅ **PDF Report Generation** - COMPLETE
2. ✅ **Help Tab** - COMPLETE
3. ✅ **Confirmation Dialogs** - COMPLETE
4. ⏳ **PowerShell History Analysis** - High forensic value
5. ⏳ **Harden Tab** - Security profile application

### Medium Priority
6. Demo Mode - Training and POC capability
7. HTML/Markdown report exports
8. Artifacts ZIP bundle

### Lower Priority
9. Enhanced baseline monitoring
10. Network connection tracking
11. Browser artifact analysis

---

## 11. Release Checklist (v1.0)

- ✅ All core tabs functional (Dashboard, Hunt, Persist, Sweep, Cleanup, Case, Help)
- ✅ PDF report generation working
- ✅ Confirmation dialogs for destructive actions
- ✅ Admin detection and warnings
- ✅ Hunt history tracking
- ✅ Comprehensive help documentation
- ✅ Professional README for GitHub
- ⏳ Icon file (viperkit.ico)
- ⏳ Screenshots for README
- ⏳ Build portable exe
- ⏳ Create GitHub release v1.0
- ⏳ Upload exe to releases

---

## 12. Known Limitations (v1.0)

1. **Windows only** - No Linux/Mac support (by design)
2. **No Harden tab** - Planned for v1.1
3. **No PowerShell history** - Planned for v1.1
4. **No Demo Mode** - Planned for future release
5. **Basic HTML export** - Enhanced reports planned

---

## Version History

See [CHANGELOG.md](CHANGELOG.md) for detailed version history.

**v1.0** (2024-11-24) - Initial Release
- Complete Hunt, Persist, Sweep, Cleanup, Case, Help tabs
- Professional PDF report generation
- Confirmation dialogs and safety features
- Admin detection
- Hunt history tracking
