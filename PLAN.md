# ViperKit — Scope & Plan v2.0

**Owner:** Jeremy Tarkington
**Codename:** ViperKit
**Brand:** VENOMOUSVIPER (teal-on-dark cyber theme)

---

## 0. Branding & Theme

- Primary background: `#0B1518` (dark teal)
- Accent / highlight: `#00FFFA` (Viper teal)
- Font style: clean, sans-serif (no goofy "gamer" fonts)
- Logo: `assets/Logo.png` (snake head + VENOMOUSVIPER tag)

**Rules**

- App should look like a serious incident tool, not a game launcher.
- Logo + "ViperKit" branding visible on the main window / dashboard.
- Theme should be consistent across every tab (no random colors later).

---

## 1. Purpose (What ViperKit Is)

ViperKit is a **portable, offline-first incident response toolkit** for Windows.

Target user: MSP engineers or IT staff **without** a full-time security team. Specifically designed for Tier 1/2 help desk technicians with little to no cybersecurity experience.

ViperKit helps an operator:

1. **Hunt** IOCs and suspicious artifacts.
2. Map deep **Persistence** mechanisms (not just Run keys).
3. **Sweep** for recent changes and potential droppers.
4. **Remediate** in a controlled, reversible way.
5. **Clean up** leftovers and reset broken hygiene.
6. **Harden** the endpoint with a reversible secure profile.
7. Bundle a **Case** report and evidence.
8. Access a simple **Help** view with rules and shortcuts.

---

## 2. Non-Goals (What ViperKit Is Not)

- Not an AV/EDR replacement.
- Not automatic malware removal or "one-click fix everything".
- Not cloud-dependent (core features must work fully **offline**).
- Not "persistence-only" tooling.

---

## 3. Tech Stack

- **.NET 8** + **Avalonia UI**
- Cross-platform framework, Windows-focused features
- Single self-contained executable via `dotnet publish`
- Runs locally, elevated, from a single folder
- No internet required for core features

---

## 4. Core Workflow

The "Case Focus" concept is central to ViperKit's workflow:

```
1. HUNT    → Find suspicious item (ScreenConnect, malware.exe, etc.)
             → Set as "Case Focus" (captures file path + timestamp)

2. PERSIST → Scan for persistence mechanisms
             → Items matching focus are highlighted
             → See if the suspicious item has autoruns, services, tasks

3. SWEEP   → Scan recent file changes
             → Temporal clustering finds files created ±1-8h of focus target
             → "What else was installed at the same time?"
             → Add related items to focus

4. PERSIST → Re-run with expanded focus
             → Now see persistence for ALL suspicious items

5. CLEANUP → Remove identified threats (Coming)

6. HARDEN  → Prevent reinfection (Coming)

7. CASE    → Export full case report with timeline
```

---

## 5. Milestone Status

### M0 — Plan & Wireframe: COMPLETE
- Approved scope + UX map + acceptance tests per tab
- Branding colors and assets defined

### M1 — Dashboard: COMPLETE
- System snapshot (hostname, user, OS)
- Case ID and event tracking
- Case export functionality
- Status display

### M2 — Hunt MVP: COMPLETE
- IOC input with type selector (Auto, File/Path, Hash, Domain/URL, IP, Registry, Name/Keyword)
- File/Path: existence check, metadata, hash calculation (MD5/SHA1/SHA256)
- Hash: type identification, optional disk scan
- Domain/URL: DNS lookup, HTTP probe
- IP: reverse DNS, ping test
- Registry: key enumeration, value display
- Name/Keyword: process search, install folder search, scoped file search
- Structured results list with severity levels
- Case focus integration
- Actions: open location, copy target, save results
- Full case event logging

### M3 — Persist MVP: COMPLETE
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
- Actions: investigate, add to case, add to focus, open location

### M4 — Sweep MVP: COMPLETE
- Lookback window: 24h, 3d, 7d, 30d
- Scan locations: all user profiles, ProgramData, Startup folders
- Services & drivers scan
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
- Actions: investigate (SHA256 + VirusTotal), add to case, add to focus, open location
- Copy/save results, case event logging

### M5 — Cleanup MVP: NOT STARTED
- Disable/delete scheduled tasks
- Stop/disable services
- Quarantine files
- Remove autorun registry values
- Preview → Export → Apply → Undo flow

### M6 — Harden MVP: NOT STARTED
- Standard/Strict security profiles
- Defender preference toggles
- Script engine restrictions
- RDP/NLA configuration checks
- Rollback capability

### M7 — Case MVP: PARTIAL
- Events logged throughout workflow
- Case export to text file working
- HTML/Markdown report generation (not started)
- Artifacts ZIP bundle (not started)

### M8 — Help: NOT STARTED
- Safety rules
- Tab usage instructions
- Log/report locations
- Keyboard shortcuts

---

## 6. Feature Specs per Tab

### 6.1 Dashboard — COMPLETE

- ViperKit branding (logo + name)
- System snapshot (hostname, user, OS)
- Case summary (ID, event count, last event)
- Case export button
- Status messages

### 6.2 Hunt — COMPLETE

**Input types:**
- Hash (MD5/SHA1/SHA256)
- URL, domain, IP
- File path / filename
- Registry key
- Name/keyword search

**Search targets:**
- Filesystem (user dirs, AppData, ProgramData, Temp)
- Running processes
- Program Files / ProgramData folders
- Registry keys
- Network (DNS, HTTP probes, ping)

**Output:**
- Structured results list with category, severity, summary
- Actions: open location, copy target, set focus
- Case event logging

### 6.3 Persist — COMPLETE

**Coverage:**
- IFEO debuggers
- Winlogon (Shell/Userinit)
- AppInit_DLLs
- Services/Drivers (auto-start)
- Scheduled Tasks
- Startup folders
- Run/RunOnce keys
- PowerShell profiles

**Features:**
- Risk assessment (OK/NOTE/CHECK)
- High-signal flagging
- MITRE ATT&CK mapping
- Publisher extraction
- Summary panel with counts
- Multiple filter options
- Focus highlighting

### 6.4 Sweep — COMPLETE

**Features:**
- Configurable lookback window
- Multi-location scanning
- Severity-based classification
- Focus integration with temporal clustering
- Configurable cluster window (±1h to ±8h)
- VirusTotal integration via Investigate button
- Summary panel with counts

### 6.5 Cleanup — NOT STARTED

**Planned:**
- Task removal with XML export
- Service stop/disable with config export
- File quarantine to `.\quarantine`
- Registry value removal with key export
- Preview → Apply → Undo flow

### 6.6 Harden — NOT STARTED

**Planned:**
- Standard/Strict profiles
- Defender settings
- Script engine restrictions
- RDP hardening
- Rollback capability

### 6.7 Case — PARTIAL

**Current:**
- Event logging from all tabs
- Text file export

**Planned:**
- HTML report generation
- Markdown report generation
- Artifacts ZIP bundle
- Evidence table with notes

### 6.8 Help — NOT STARTED

**Planned:**
- Safety rules
- Tab usage guide
- Log locations
- Keyboard shortcuts

---

## 7. Safety & Rollback

- Preview-first and "require export before changes" **ON by default**
- Every destructive action writes to an undo journal
- Logs stored under `.\logs` in the ViperKit folder
- Case events track all significant actions

---

## 8. Packaging & Footprint

- Single portable directory
- Writes only to:
  - `.\logs`
  - `.\quarantine` (if used)
  - `.\exports` or `.\reports` (for outputs)
- Must run elevated for many features
- No internet required for core features
- VirusTotal lookups are optional and user-initiated

---

## 9. Next Steps

1. **Cleanup tab** — Implement safe removal workflow
2. **Harden tab** — Security profile application
3. **Case tab** — Full report generation
4. **Help tab** — User documentation
5. **Quick Hunt buttons** — Pre-built IOC searches for common threats
