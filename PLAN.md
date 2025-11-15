# ViperKit — Scope & Plan v1.0 (M0)

**Owner:** Jeremy Tarkington  
**Codename:** ViperKit  
**Brand:** VENOMOUSVIPER (teal-on-dark cyber theme)

---

## 0. Branding & Theme

- Primary background: `#002E30` (dark teal)  
- Accent / highlight: `#00FFFA` (Viper teal)  
- Font style: clean, sans-serif (no goofy “gamer” fonts).  
- Logo: `assets/Logo.png` (snake head + VENOMOUSVIPER tag).

**Rules**

- App should look like a serious incident tool, not a game launcher.
- Logo + “ViperKit” branding visible on the main window / dashboard.
- Theme should be consistent across every tab (no random colors later).

---

## 1. Purpose (What ViperKit Is)

ViperKit is a **portable, offline-first incident response toolkit** for Windows.

Target user: MSP engineers or IT staff **without** a full-time security team.

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
- Not automatic malware removal or “one-click fix everything”.
- Not cloud-dependent (core features must work fully **offline**).
- Not “persistence-only” tooling.
- No giant folder tree until a milestone actually needs it.

---

## 3. Operating Modes

**Phase 1 (current project):**

- Windows portable app.
- Tech stack: .NET 8 + Avalonia UI.
- Packaging: Single self-contained EXE later via `dotnet publish`
- Runs locally, elevated, from a single folder.

**Phase 2 (future, separate track):**

- Optional Linux boot media with a curated GUI toolkit for dead-box / offline work.
- Not part of the current coding milestones.

---

## 4. UX & Navigation Contract

**Main tabs (top or left navigation):**

- **Dashboard**
- **Hunt**
- **Persist**
- **Sweep**
- **Remediate**
- **Cleanup**
- **Harden**
- **Case**
- **Help**

**Global concepts**

- **Evidence Cart**  
  Any finding (file, registry key, service, scheduled task, WMI item, etc.) can be added to an “Evidence” list with its type and context.

- **Preview / Undo**  
  Every destructive or configuration-changing action:
  - Shows exactly what will be done (commands, keys, files).
  - Requires an export/snapshot before applying.
  - Writes to an undo journal so the operator can roll back.

- **Trace View**  
  When something is hunted, ViperKit shows basic relationships, for example:
  - Task → runs → `C:\path\evil.exe`
  - `evil.exe` → loads → `weird.dll`
  - Service → binary path → non-system directory

- **Help View**  
  One concise page with:
  - Safety rules
  - “How to use each tab” in plain language
  - Where logs & reports are stored
  - Keyboard shortcuts (once we add them)

---

## 5. Data Model (Kept Small & Clear)

**Observable / IOC**

- Hash, path, filename
- URL, domain, IP
- Registry key/value, CLSID
- Service / task name
- Process ID / image

**Entity**

- File, Directory
- RegistryKey
- Service
- Scheduled Task
- WMI (filter/consumer/binding)
- COM object
- NetworkRule / Firewall entry
- User

**Relation**

- `launches`
- `loads`
- `persists-via`
- `drops`
- `connects-to`

**Evidence**

- List of Entities + Observables + Relations
- Timestamps
- Operator notes

**Journal**

- Ordered actions for undo/redo
- Written to log files under `.\logs`

---

## 6. Feature Specs per Tab (MVP = “Done means”)

### 6.1 Dashboard

**Goal:** Give the operator a starting point and status view.

MVP:

- ViperKit branding (logo + name + brief description).
- Quick explanation of each tab.
- Link/buttons to:
  - Open log folder
  - Open generated reports folder
  - View Help tab

“Done” = A clean landing view with branding and simple instructions, no hidden actions.

---

### 6.2 Hunt (IOC in → Findings out)

**Input types:**

- Hash, URL, domain, IP
- File path / filename
- Registry key / CLSID

**Offline search targets:**

- Filesystem:
  - Known user/system dirs, including:
    - `Users\<user>\AppData\Local`, `Roaming`, `Temp`
    - `ProgramData`
    - Startup folders
- Registry autoruns & adjacent hives:
  - `HKCU` / `HKLM\Software\Microsoft\Windows\CurrentVersion\Run*`
  - Winlogon, IFEO, AppCertDlls, LSA, Shell extensions / CLSID
- Scheduled tasks (including hidden)
- Services and drivers
- WMI (filters, consumers, bindings)
- Browser startup locations & extensions (Chrome/Edge, per profile)
- Hosts file / DNS cache for IP/domain

**Output:**

- Results grid with:
  - Type (file, reg, service, task, etc.)
  - Location (path / key)
  - Source (which collector found it)
  - Basic confidence or flags
- **Trace View** listing relationships.
- Button: **Add to Evidence**

“Done” = Search works offline, shows hits with context, builds simple trace edges, and can add selected items to Evidence.

---

### 6.3 Persist (Deep Persistence Map)

**Coverage (MVP):**

- IFEO debuggers
- AppCertDlls, Image Load
- Winlogon (Shell/Userinit/Ginacmd)
- LSA providers / SSP
- KnownDlls hijack checks
- COM hijacks (InprocServer32)
- Shell extensions
- WMI persistence (filters/consumers/bindings)
- Services/Drivers (Auto + suspicious paths)
- Scheduled Tasks
- Startup folders
- Run / RunOnce / RunServicesEx
- Explorer shell keys
- Script Host policies
- PATH / DLL search order pitfalls
- `netsh` helpers
- Proxy/WPAD hooks

**Heuristics:**

- Non-system directories for binaries
- Alternate Data Streams
- Invalid or missing signatures where applicable

**Actions:**

- Open location
- Copy command / path
- Add to Evidence

“Done” = One click enumerates persistence surface, tags suspicious entries, and supports Evidence collection.

---

### 6.4 Sweep (Recent Change Radar)

**MVP:**

- Time window slider (default: 7 days)
- Looks for:
  - Newly created/modified files in user-writable dirs
  - New services / tasks
  - New users / groups
  - New firewall rules
  - New browser extensions
  - MSI installs / uninstalls (summary)
  - Prefetch for new binaries

**Output:**

- Sorted list with timestamps and artifact type
- Add to Evidence
- Export to CSV

“Done” = Operator can see what changed recently and export a timeline.

---

### 6.5 Remediate (Surgical, Reversible)

**Playbooks (MVP):**

- Disable/delete scheduled task
  - Export task XML first
- Stop/disable service
  - Export config first if feasible
- Quarantine file
  - Copy to `.\quarantine`
  - Unblock if needed
  - Optionally replace with inert stub
  - Kill process tree
- Remove autorun registry value
  - Export key first

Each playbook flow: **Preview → Export → Apply → Undo**

“Done” = Every action shows exact changes, forces an export snapshot, and can be rolled back from the journal.

---

### 6.6 Cleanup (Post-Removal Hygiene)

**MVP:**

- Reset proxy/WPAD to default
- Winsock reset (preview only; operator chooses when to run)
- Remove orphaned autoruns
- Purge droppers in known `Temp`/stash directories
- Reset scheduled task cache entries for removed tasks

“Done” = Preview list, Apply button, and undo where applicable. No silent destructive behavior.

---

### 6.7 Harden (Quick Secure Profiles, Reversible)

**Profiles:**

- **Standard**
- **Strict**

**Possible controls (MVP):**

- Defender preference toggles (where present)
- Block Office macros from internet
- Disable risky script engine autoruns for standard users
- Fix common RDP / NLA misconfigs (no blind blanket “off”)
- Enable SmartScreen where applicable
- Basic SRP/AppLocker template for common dropper dirs

“Done” = Applying a profile shows the planned GPO/registry changes and creates a rollback plan.

---

### 6.8 Case (Report & Bundle)

**MVP:**

- Live Evidence table with:
  - Type, path, source, relation notes
  - Operator notes
- Generate:
  - HTML report
  - Markdown report
  - ZIP bundle with:
    - Logs
    - Exports
    - Snapshots

“Done” = One button produces a clean report and artifacts ZIP.

---

### 6.9 Help

**MVP Content:**

- Short list of safety rules (e.g., “Preview everything; always export before applying.”)
- Where logs live
- Where reports are stored
- One-liner description of each tab
- Note: ViperKit is a manual assistant, not an automatic malware remover.

“Done” = One page, fast to read, no scrolling novel.

---

## 7. Safety & Rollback

- Preview-first and “require export before changes” **ON by default**.
- Every action writes to an undo journal.
- Logs stored under `.\logs` in the ViperKit folder.
- Optional:
  - Prompt to create a System Restore Point when supported (Windows client SKUs).
  - Strict mode may require explicit operator confirmation.

---

## 8. Packaging & Footprint

- Single portable directory.
- Writes only to:
  - `.\logs`
  - `.\quarantine` (if used)
  - `.\exports` or `.\reports` (for outputs)
- Must run elevated for many features, but:
  - Clearly shows current privilege status.
  - Refuses to run dangerous actions without elevation.
- No internet required for core features.
- Any later online lookups (VT, URL intel, etc.) are behind a clear toggle and **never block** offline flows.

---

## 9. Milestones & Acceptance Criteria

**M0 — Plan & Wireframe (THIS DOCUMENT)**

- ✅ Approved scope + UX map + acceptance tests per tab.
- ✅ Branding colors and assets defined.
- Only repo files allowed at this stage:
  - `PLAN.md`
  - `README.md` (skeleton)
  - `assets/Logo.png`
  - `BRANDING.md` (optional at M0)

**M1 — Hunt MVP**

- Hunt tab UI file.
- Hunt module (PowerShell).
- Evidence integration.
- Logging in place.
- No destructive actions.

**M2 — Persist MVP**

- Deep autoruns & persistence map.
- Suspicious heuristics.
- Evidence integration.

**M3 — Sweep MVP**

- Time-window diff across artifacts.
- CSV export.

**M4 — Remediate MVP**

- Four safe playbooks (task, service, file quarantine, autorun removal).
- Export → apply → undo flow.

**M5 — Cleanup MVP**

- Hygiene tasks with preview/undo.

**M6 — Harden MVP**

- Two profiles (Standard/Strict) with rollback.

**M7 — Case MVP**

- Report generation (HTML/MD) + artifacts ZIP.

---

## 10. Milestone Deliverables (Per PR)

Every milestone PR must contain exactly:

1. The UI definition for that tab only.
2. One PowerShell module implementing that tab’s logic.
3. Updated `CHANGELOG.md`.
4. Updated `README.md` section for that feature.

No extra folders or scripts until a milestone needs them.

---
