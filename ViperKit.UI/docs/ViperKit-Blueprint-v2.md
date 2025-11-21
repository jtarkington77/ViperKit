ViperKit v1 – Incident Workflow Spec (Locked)
1. Purpose

ViperKit is not just a fancy search UI.

ViperKit is a guided incident workflow that walks a tech from:

“I think this box is compromised”
→ Hunt for the bad tool
→ Persist to see what keeps it alive
→ Sweep to see what landed with it
→ Cleanup to remove it safely
→ Harden to make this exact stunt harder next time
→ Case Export to document everything that happened.

Everything in the UI, logging, and exports should support that story.

2. High-Level Workflow

For a single endpoint:

Start Case

New case is created automatically when ViperKit opens.

System snapshot (host/user/OS) is recorded.

All actions are attached to this case.

Hunt – Find suspicious tools / binaries

Operator uses Hunt to locate suspicious binaries/processes/paths (e.g., rogue RMM, EasyHelp, pwsh7, etc.).

From Hunt results, operator can “Add to Case Targets”.

Persist – See how those targets stay alive

Operator runs a persistence scan.

Persist highlights any autoruns / services / tasks / Winlogon / IFEO / AppInit entries that are tied to Case Targets.

Operator can add persistence entries to the Case log.

Sweep – See what landed with them

Operator runs a Sweep over a lookback window (e.g., last 7 days).

Sweep groups file changes around each Case Target in time + folder.

Operator can promote additional suspicious files from Sweep into Case Targets.

Cleanup – Remove everything tied to the case

ViperKit shows a structured checklist of artifacts (files, services, tasks, keys) grouped by Case Target.

Operator uses ViperKit as the control center to investigate & remove them, marking items as “removed”.

All removals are logged to the Case.

Harden – Apply targeted defenses

Based on what was removed in this case, Harden suggests a small set of targeted controls (e.g., block rogue RMMs, block execution from temp).

Operator marks applied hardening steps; they are logged to the Case.

Case / Export – Single story of the incident

Case tab shows a chronological timeline of Hunt → Persist → Sweep → Cleanup → Harden events.

Export produces a readable report for ticketing / IR documentation.

3. Core Concepts
3.1 Case

A Case is a single incident investigation on one endpoint.

Tracked fields (conceptually):

Case ID (Host-YYYYMMDDHHMMSS)

Hostname, Username, OS description

Start / End time

Case Targets (see below)

Case Events (timeline of everything that happened)

3.2 Case Targets

Case Targets are the core of the workflow.

“These are the binaries / tools / components we believe are connected to this incident.”

Examples:

C:\ProgramData\ScreenConnect\ScreenConnect.ClientService.exe

C:\Program Files\EasyHelp\easyhelp.exe

C:\Program Files\PowerShell\7\pwsh.exe

Rules:

Targets are created by the operator (primarily from Hunt and Sweep).

Targets can be:

Exact file paths

Process image names

Tool names (if path not yet known)

All tabs use Case Targets to decide what to highlight, cluster, and log against.

(Implementation note: CaseManager should have a list like List<CaseTarget> in addition to the existing FocusTarget string.)

3.3 Case Events (Timeline)

Every meaningful action writes a CaseEvent:

Tab – which tab (Hunt, Persist, Sweep, Cleanup, Harden, Case, System)

Action – short verb phrase (“Target added”, “Persistence scan completed”, “Artifact removed”)

Severity – INFO / WARN / (later maybe ERROR)

Target – main object (path, name, service)

Details – human-readable sentence(s) with context

These populate both:

Dashboard high-level summary (last event, count)

Case tab detailed timeline

Exported text report

4. Tab-by-Tab Behavior
4.1 Dashboard

Role: Entry point overview.

Shows:

System Snapshot

Hostname, User, OS (from CaseManager.HostName, etc.)

Current Case Summary

Case ID

Number of Case Events

Last event summary

Status Line

High-level message (“Case initialising…”, “Last action: persistence scan completed – 5 hotspots, 2 CHECK entries.”)

Dashboard does not control the workflow; it reflects it.

4.2 Hunt – “Find the bad stuff”
Operator Flow

Enter IOC in HuntIocInput:

file path, exe name, hash, domain, IP, registry key, etc.

Choose type (Auto / File path / Hash / Domain/URL / IP / Registry).

Click Run Hunt.

Behaviour

Performs hunts using existing logic (files/processes/registry/etc.).

Results populate:

HuntResultsText (raw text / log style).

HuntResultsList (structured HuntResult objects).

New Required Feature

Add to Case Targets (button or context action on HuntResultsList item):

When triggered on a result:

Extract a canonical identifier for the thing:

Preferable: full file path if present.

Otherwise: process image path / name.

Call a CaseManager method (conceptually):

CaseManager.AddTarget(identifier, sourceTab: "Hunt");


Write a CaseEvent:

Tab: Hunt

Action: "Target added"

Target: the identifier

Details: maybe include Hunt category, severity, and brief reason.

Optionally:

Update the Case tab to show the current list of Case Targets (read-only for now).

Update UI status text: “Target added to case: <path>”.

4.3 Persist – “What keeps it alive?”
Operator Flow

After adding 1+ Case Targets in Hunt, click Persist.

Click Run persistence scan.

Review:

Entries that match Case Targets.

Other CHECK entries / hotspots.

For suspicious entries:

Investigate (open Regedit / Explorer / Task Scheduler / Services).

Optionally Add selected to Case (log it).

Behaviour

Persist already does:

Run keys (HKCU/HKLM + Wow6432Node)

Winlogon Shell/Userinit

IFEO Debugger

AppInit_DLLs

Startup folders

Services and Drivers

Scheduled Tasks

Tie-in with Case Targets:

For each PersistItem:

If its Path, RegistryPath, or Name matches or contains any Case Target identifier:

Mark it as related to that target (internally).

Visually emphasize it:

e.g., badge: “Target: ScreenConnect.ClientService.exe”

or grouping/headline in the UI (future refinement).

Case Events to log:

After scan:

Tab="Persist", Action="Persistence scan completed", Target="Entries: X, CHECK: Y, Hotspots: Z".

When user clicks Add selected to case:

Tab="Persist", Action="Persistence entry added to case", Target=item.Name or Path, Details includes LocationType, Source, and related CaseTarget if known.

When user opens an entry (Investigate):

Tab="Persist", Action="Investigated persistence entry", Target=item.Name or Path, Details includes Via: Regedit/Explorer/Services/Task Scheduler.

Persist should not delete or change anything yet – it’s for mapping, not removal.

4.4 Sweep – “What else landed when this got installed?”
Operator Flow

Click Sweep.

Pick a lookback window (e.g., 7 days).

Click Run Sweep.

For each Case Target:

Look at entries grouped by time + folder proximity.

Promote other suspicious items to Case Targets.

Behaviour

Sweep identifies recent changes (files, programs, etc.) over a time range.

Case Targets integration:

For each Case Target:

Determine:

Target path (if path-type target).

Parent folder (e.g., C:\ProgramData\ScreenConnect).

File timestamps (if accessible).

Create one or more clusters:

“Cluster around [TargetName] in [Folder]:”

Items in the same folder tree.

Items created/modified within ±N hours/days of the target.

These clusters appear logically in the UI (even if underlying UI remains a flat list for now).

User actions:

Click on suspicious SweepEntry and Add to Case Targets (similar to Hunt).

That loops back through the workflow: newly added target will show up in future Persist scans and Sweep clusters.

Case Events:

After sweep:

Tab="Sweep", Action="Sweep completed", Target="Entries: X", Details mention lookback & how many entries touched CaseTargets.

When a sweep item is promoted to a target:

Tab="Sweep", Action="Target added from sweep", Target=path/name, Details mention cluster info (folder, time range, related target if any).

4.5 Cleanup – “Walk the machine to clean state”
Operator Flow

Go to Cleanup after mapping everything in Persist & Sweep.

See a checklist grouped by Case Target:

ScreenConnect:

Services

Scheduled tasks

Files/Folders

Registry autoruns

EasyHelp:

…

PowerShell 7:

…

For each item:

Click actions to open appropriate tools (Explorer, Regedit, Services, Task Scheduler).

Manually remove them.

Mark item as “Removed” in ViperKit.

(v1 is manual-assist — no automated deletion yet.)

Behaviour

Internally, Cleanup reads from:

Persist items related to Case Targets.

Sweep items related to Case Targets.

(Later) any additional forensic results.

Each removal action results in:

Tab="Cleanup", Action="Artifact removed", Target=path/service/key, Details include:

Type (Service / File / Task / Autorun)

Which Case Target it was grouped under.

If the operator decides something is benign:

Optionally “Mark as benign”:

Tab="Cleanup", Action="Artifact marked benign", Target=....

4.6 Harden – “Close the door they walked through”
Operator Flow

After Cleanup, operator opens Harden.

Harden shows recommended controls based on:

What Case Targets were involved (e.g., rogue RMM tools, scripts in Temp, etc.).

What persistence mechanisms were used.

Examples:

For rogue RMM tools:

Suggest:

AppLocker / SRP rules disallowing known RMM paths or exe names.

Tighten inbound firewall + remote access policy.

For activity in %TEMP% / %Downloads%:

Suggest:

Block execution from user temp/downloads via SRP/AppLocker.

Enable Script Block Logging.

Operator then:

Applies some of these outside ViperKit (via GPO, RMM, etc.).

Marks them as “Applied” in the Harden tab.

Each applied hardening action logs:

Tab="Harden", Action="Recommendation applied", Target="Block execution in %TEMP%", Details="Applied via SRP policy / GPO".

4.7 Case – “One linear story + export”
Operator Flow

At any time, operator can open Case to see:

Case metadata (ID, host, user, started time).

Case focus string (optional single-text filter).

Full event list in chronological order.

At the end, operator:

Hits Export case report.

Gets a text file suitable for:

Ticket notes

Incident report

Knowledge base entry

Behaviour

Case tab is a read-only timeline of:

System / Case start

Targets added (from Hunt/Sweep)

Persistence scans + notable entries

Sweep results/cluster actions

Cleanup removals

Harden recommendations applied

Any errors significant to the investigation

Export format (v1 text):

Header with case metadata.

Summary counts (targets, persistence entries, artifacts removed, controls applied).

Ordered events with timestamps and tabs.

5. Example: Rogue RMM + Helpers

Scenario:

Attacker installs:

ScreenConnect (rogue)

EasyHelp (rogue)

PowerShell 7

A week later, ScreenConnect shows back up after you “uninstall” it the first time.

How ViperKit should be used:

Hunt

Search for “ScreenConnect”.

Find ScreenConnect.ClientService.exe, “Add to Case Targets”.

Search for “EasyHelp” and “PowerShell 7” → Add those too.

Persist

Run persistence scan.

See:

ScreenConnect services and scheduled tasks tied to its exe path.

Any weird autorun keys pointing at EasyHelp.

Add relevant entries to the case log and investigate them.

Sweep

Run 7-day sweep.

See clusters:

Around C:\ProgramData\ScreenConnect\... with ScreenConnect.Setup.msi and maybe a PowerShell script.

Around C:\Program Files\EasyHelp\... with DLLs and helper exes.

Promote suspicious installer/loader scripts to Case Targets if needed.

Cleanup

Walk through the grouped artifacts (services, tasks, files, keys) for:

ScreenConnect

EasyHelp

PowerShell 7

Remove them, mark them as removed.

Harden

Apply:

Block known rogue RMMs in AppLocker/SRP.

Restrict execution in %TEMP% and %Downloads%.

Mark controls as applied.

Case Export

Export the case file and attach to:

The client’s ticket.

Internal IR records.

It reads like a clear narrative: what was found, how it persisted, what else was present, what was removed, and how the system was hardened.