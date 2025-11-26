# ViperKit v2.0 Blueprint

**Version:** 2.0.0
**Target Release:** Q2 2025
**Owner:** Jeremy Tarkington
**Status:** Planning → Development
**Last Updated:** 2024-11-25

---

## Executive Summary

ViperKit v2.0 expands forensic capabilities while maintaining the portable, point-in-time analysis design. The primary enhancement is a **categorized tab structure** that scales to support 15+ features while keeping the UI clean and intuitive.

**Key Changes:**
- 2-level tab hierarchy (Categories → Features)
- 4 new forensic capabilities (Network, Browser, Event Logs, Enhanced Export)
- Reusable UI components for consistency
- Improved architecture with Services layer

---

## UI/UX Design - Categorized Tab Groups

### Layout

```
┌─────────────────────────────────────────────────────────────┐
│ ViperKit v2.0                          [Case: Ticket-12345] │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────┬──────────┬──────────┬──────────┬──────────┐   │
│  │ OVERVIEW │ DISCOVER │ ANALYZE  │ RESPOND  │ REPORT   │   │
│  └──────────┴──────────┴──────────┴──────────┴──────────┘   │
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  [Category Sub-Tabs]                                │    │
│  │  Dashboard | Baseline | Demo                        │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                                                       │    │
│  │  [Main Content Area]                                 │    │
│  │                                                       │    │
│  │                                                       │    │
│  │                                                       │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                               │
│  [Status Bar]                          [Action Buttons]      │
└─────────────────────────────────────────────────────────────┘
```

### Category Structure

**1. OVERVIEW** - System & Case Management
   - **Dashboard** - System snapshot, case summary
   - **Baseline** - Capture/compare system state
   - **Demo** - Guided training walkthrough

**2. DISCOVER** - Find Threats
   - **Hunt** - IOC search (file, hash, domain, IP, registry)
   - **Sweep** - Time clustering for related artifacts
   - **Network** - ⭐ NEW: Active connections snapshot
   - **Browser** - ⭐ NEW: History, extensions, downloads

**3. ANALYZE** - Understand Persistence
   - **Persist** - Autorun locations, services, tasks, PowerShell history
   - **Event Logs** - ⭐ NEW: Security, System, Application logs

**4. RESPOND** - Take Action
   - **Cleanup** - Remove threats safely with undo
   - **Harden** - Apply security configurations

**5. REPORT** - Document & Export
   - **Case** - Timeline view and export
   - **Export** - ⭐ NEW: PDF, CSV, HTML, ZIP bundle
   - **Help** - In-app documentation

**Total:** 5 categories, 14 tabs

---

## Phase 1 Feature Scope (v2.0.0)

### 1. Network Snapshot Tab

**Category:** DISCOVER
**Priority:** HIGH
**Complexity:** Medium
**Dev Time:** 2-3 weeks

**User Story:**
As an MSP tech, I want to see active network connections when I run ViperKit so I can identify C2 beaconing or lateral movement.

**Features:**
- Single-button scan: "Scan Network State"
- Captures point-in-time snapshot:
  - Active TCP connections (with PIDs)
  - Active UDP connections (with PIDs)
  - Listening ports with owning processes
  - DNS cache entries
  - ARP cache
  - Hosts file content
- Process name/path mapping for each connection
- Focus integration: highlight connections from focus processes
- Severity scoring:
  - HIGH: Connections from known malware locations, suspicious ports
  - MEDIUM: Connections to non-standard ports, foreign IPs
  - LOW: Standard connections (HTTP, HTTPS, DNS)
- Export to case timeline
- Actions: Copy details, Add to focus, Add to case

**Data Captured:**
```
Connection Details:
- Protocol (TCP/UDP)
- Local Address:Port
- Remote Address:Port
- State (ESTABLISHED, LISTENING, etc.)
- Process ID
- Process Name
- Process Path
- Captured At (timestamp)
```

**UI Elements:**
- Scan button
- Results list (sortable columns)
- Summary panel (Total connections, HIGH/MEDIUM/LOW counts)
- Status text
- Export buttons (Copy, Save, Add to Case)

---

### 2. Browser Forensics Tab

**Category:** DISCOVER
**Priority:** HIGH
**Complexity:** Medium-High
**Dev Time:** 3-4 weeks

**User Story:**
As a help desk tech, I want to extract browser history and extensions when investigating so I can find malicious redirects or credential-stealing add-ons.

**Features:**
- Browser selection: Chrome, Edge, Firefox
- Lookback period: 7/30/90 days
- Artifact types:
  - **History:** URLs visited, timestamps, visit counts
  - **Extensions:** Installed extensions with IDs, names, versions
  - **Downloads:** Downloaded files with paths and hashes
- Profile support: scans all user profiles on system
- Time clustering: integrate with Sweep focus timestamps
- Severity scoring:
  - HIGH: Extensions from unknown sources, downloads of executables
  - MEDIUM: Suspicious domains, excessive visit counts
  - LOW: Normal browsing activity
- Export to case timeline
- Actions: Copy URL, Add to focus, Add to case, Investigate (hash lookup)

**Data Captured:**
```
History Entry:
- Browser (Chrome/Edge/Firefox)
- Profile Name
- URL
- Title
- Visit Time
- Visit Count

Extension:
- Browser
- Profile Name
- Extension ID
- Extension Name
- Extension Version
- Install Time

Download:
- Browser
- Profile Name
- URL
- File Path
- File Hash (SHA256)
- Download Time
```

**UI Elements:**
- Browser selector (dropdown)
- Lookback period selector
- Scan button per browser
- Results list with filters (History/Extensions/Downloads)
- Summary panel (Total artifacts, suspicious count)
- Status text
- Export buttons

**Technical Notes:**
- SQLite database parsing for Chrome/Edge
- JSON file parsing for Firefox
- Must copy databases to temp before parsing (browsers lock files)
- Handle profile detection for all users

---

### 3. Event Log Parser Tab

**Category:** ANALYZE
**Priority:** MEDIUM
**Complexity:** Medium-High
**Dev Time:** 3-4 weeks

**User Story:**
As an incident responder, I want to extract suspicious events from Windows logs so I can find evidence of compromise.

**Features:**
- Log selection: Security, System, Application, PowerShell Operational
- Lookback period: 7/30/90 days
- Predefined query templates:
  - Logon Events (successful/failed)
  - Process Creation
  - Service Installation
  - Privilege Escalation
  - PowerShell Script Execution
  - Application Crashes
- Event filtering by Event ID
- Suspicious pattern detection:
  - Multiple logon failures
  - Encoded PowerShell commands
  - Service installation with suspicious paths
  - Privilege escalation attempts
- Severity scoring based on patterns
- Export to case timeline
- Actions: Copy event, Add to case, View full XML

**Data Captured:**
```
Event Entry:
- Log Name (Security/System/Application)
- Event ID
- Source
- Time Created
- Level (Information/Warning/Error)
- Message
- User
- Computer
- Category (Logon/Process Creation/etc.)
- Is Suspicious (bool)
- Detection Reason
```

**High-Value Event IDs:**
- **4624** - Successful logon
- **4625** - Failed logon
- **4688** - Process creation
- **4697** - Service installed
- **4720** - User account created
- **7045** - Service installed (System log)
- **4104** - PowerShell script block execution

**UI Elements:**
- Log selector (dropdown)
- Lookback period selector
- Quick filters (Logon/Process/Service/PowerShell)
- Scan button
- Results list with severity badges
- Summary panel (Total events, suspicious count)
- Status text
- Export buttons

**Technical Notes:**
- Use System.Diagnostics.Eventing.Reader namespace
- Requires SeSecurityPrivilege for Security log
- Parse Event XML for detailed data
- Handle large result sets (pagination)

---

### 4. Enhanced Export Formats

**Category:** REPORT
**Priority:** MEDIUM
**Complexity:** Low-Medium
**Dev Time:** 1-2 weeks

**User Story:**
As an MSP, I want to export findings to CSV/HTML so I can import into our ticketing system or share with clients.

**Features:**
- **CSV Export:**
  - All findings from all tabs in single CSV
  - Columns: Category, Type, Name, Path, Severity, Timestamp, Notes
  - Compatible with Excel/Google Sheets
  - Saved to Documents\ViperKit\Reports\

- **HTML Report:**
  - Styled version of PDF report
  - Same structure (summary, findings, timeline)
  - Embeds CSS for self-contained file
  - Printable via browser

- **Markdown Export:**
  - Text-based format for wikis/documentation
  - Same structure as PDF
  - Good for GitHub/Confluence

- **Forensics Bundle (ZIP):**
  - Password-protected ZIP containing:
    - Case JSON
    - PDF report
    - CSV export
    - All scan results
    - Cleanup journal
    - Harden journal
    - Registry exports
  - SHA256 manifest file
  - Saved to Documents\ViperKit\Bundles\

**UI Changes:**
- Add "Export" button menu on Case tab:
  - Export PDF (existing)
  - Export CSV (new)
  - Export HTML (new)
  - Export Markdown (new)
  - Export Forensics Bundle (new)
- Progress indicator for large exports
- Success/failure notification

**Technical Notes:**
- CSV: Use StringBuilder for performance
- HTML: Embed CSS inline for portability
- ZIP: Use System.IO.Compression with AES encryption
- SHA256: Use System.Security.Cryptography

---

## Architecture Changes

### New File Structure

```
ViperKit.UI/
├── Views/
│   ├── MainWindow.axaml           # Updated: 2-level tab hierarchy
│   ├── MainWindow.axaml.cs        # Updated: category navigation
│   │
│   ├── MainWindow.Network.cs      # ⭐ NEW
│   ├── MainWindow.Browser.cs      # ⭐ NEW
│   ├── MainWindow.EventLogs.cs    # ⭐ NEW
│   │
│   └── Components/                # ⭐ NEW: Reusable UI
│       ├── ResultsListView.axaml
│       ├── SeverityBadge.axaml
│       └── ScanProgressBar.axaml
│
├── Models/
│   ├── NetworkConnection.cs       # ⭐ NEW
│   ├── BrowserArtifact.cs         # ⭐ NEW
│   ├── EventLogEntry.cs           # ⭐ NEW
│   │
│   └── (existing models...)
│
├── Services/                      # ⭐ NEW: Business logic layer
│   ├── PdfReportGenerator.cs     # Existing
│   ├── CsvExporter.cs            # ⭐ NEW
│   ├── HtmlReportGenerator.cs    # ⭐ NEW
│   ├── MarkdownExporter.cs       # ⭐ NEW
│   ├── NetworkScanner.cs         # ⭐ NEW
│   ├── BrowserParser.cs          # ⭐ NEW
│   └── EventLogParser.cs         # ⭐ NEW
│
└── Utilities/                     # ⭐ NEW: Shared helpers
    ├── SqliteHelper.cs
    ├── ProcessHelper.cs
    └── ExportHelper.cs
```

### Design Patterns

**Services Layer:**
- Heavy logic extraction from Views
- Testable, reusable business logic
- Static classes for stateless operations

**Component Pattern:**
- Reusable XAML UserControls
- Consistent UX across tabs
- Reduce code duplication

**Data Models:**
- Plain C# classes (POCOs)
- Immutable where possible
- Clear property names

---

## Data Models (New)

### NetworkConnection.cs
```csharp
namespace ViperKit.UI.Models;

public class NetworkConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Protocol { get; set; } = string.Empty; // TCP/UDP
    public string LocalAddress { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public string State { get; set; } = string.Empty; // ESTABLISHED, LISTENING, etc.
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ProcessPath { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public string Severity { get; set; } = "LOW"; // HIGH/MEDIUM/LOW
    public bool MatchesFocus { get; set; }
    public string Notes { get; set; } = string.Empty;
}
```

### BrowserArtifact.cs
```csharp
namespace ViperKit.UI.Models;

public class BrowserArtifact
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Browser { get; set; } = string.Empty; // Chrome, Edge, Firefox
    public string ProfileName { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = string.Empty; // History, Extension, Download

    // History fields
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime VisitTime { get; set; }
    public int VisitCount { get; set; }

    // Extension fields
    public string ExtensionId { get; set; } = string.Empty;
    public string ExtensionName { get; set; } = string.Empty;
    public string ExtensionVersion { get; set; } = string.Empty;

    // Download fields
    public string DownloadPath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;

    // Common fields
    public DateTime Timestamp { get; set; }
    public string Severity { get; set; } = "LOW";
    public bool MatchesFocus { get; set; }
    public bool IsTimeCluster { get; set; }
}
```

### EventLogEntry.cs
```csharp
namespace ViperKit.UI.Models;

public class EventLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string LogName { get; set; } = string.Empty; // Security, System, Application
    public int EventId { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime TimeCreated { get; set; }
    public string Level { get; set; } = string.Empty; // Information, Warning, Error
    public string Message { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Computer { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Logon, Process Creation, etc.
    public string Severity { get; set; } = "LOW";
    public bool IsSuspicious { get; set; }
    public string DetectionReason { get; set; } = string.Empty;
}
```

---

## Implementation Timeline

### Week 1-2: Foundation
- [ ] Refactor MainWindow.axaml for 2-level tabs
- [ ] Create reusable components (ResultsListView, SeverityBadge)
- [ ] Set up Services and Utilities folders
- [ ] Create data model files (stubs)

### Week 3-4: Network Tab
- [ ] Create NetworkConnection.cs model
- [ ] Implement NetworkScanner.cs service
- [ ] Build MainWindow.Network.cs partial class
- [ ] Add Network tab UI to XAML
- [ ] Test focus integration
- [ ] Add to case timeline

### Week 5-7: Browser Tab
- [ ] Create BrowserArtifact.cs model
- [ ] Implement BrowserParser.cs service
  - [ ] Chrome SQLite parsing
  - [ ] Edge SQLite parsing
  - [ ] Firefox JSON parsing
- [ ] Build MainWindow.Browser.cs partial class
- [ ] Add Browser tab UI to XAML
- [ ] Test time clustering integration
- [ ] Add to case timeline

### Week 8-10: Event Logs Tab
- [ ] Create EventLogEntry.cs model
- [ ] Implement EventLogParser.cs service
- [ ] Build MainWindow.EventLogs.cs partial class
- [ ] Add Event Logs tab UI to XAML
- [ ] Implement suspicious pattern detection
- [ ] Test performance with large logs
- [ ] Add to case timeline

### Week 11-12: Enhanced Export
- [ ] Implement CsvExporter.cs
- [ ] Implement HtmlReportGenerator.cs
- [ ] Implement MarkdownExporter.cs
- [ ] Implement ZIP bundle creation
- [ ] Update Case/Export tab UI
- [ ] Test export formats

### Week 13: Integration & Testing
- [ ] End-to-end workflow testing
- [ ] Focus integration testing across all new tabs
- [ ] Performance testing with large datasets
- [ ] Memory leak testing
- [ ] Bug fixes

### Week 14-15: Alpha Release
- [ ] Internal testing
- [ ] UI/UX validation
- [ ] Documentation updates
- [ ] Create changelog

---

## XAML Structure (Skeleton)

### MainWindow.axaml (Updated)
```xml
<Window>
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/> <!-- Header -->
      <RowDefinition Height="*"/>    <!-- Content -->
      <RowDefinition Height="Auto"/> <!-- Status Bar -->
    </Grid.RowDefinitions>

    <!-- Header -->
    <Border Grid.Row="0" Background="#0B1518" Padding="12">
      <StackPanel Orientation="Horizontal">
        <Image Source="avares://ViperKit.UI/Assets/logo.png" Height="32"/>
        <TextBlock Text="ViperKit v2.0" FontSize="18" Margin="12,0"/>
        <TextBlock x:Name="CaseIdText" Text="[No Case]" Margin="12,0"/>
      </StackPanel>
    </Border>

    <!-- Main Content - 2-Level Tabs -->
    <TabControl Grid.Row="1" x:Name="CategoryTabControl">

      <!-- OVERVIEW Category -->
      <TabItem Header="OVERVIEW">
        <TabControl>
          <TabItem Header="Dashboard"><!-- Existing --></TabItem>
          <TabItem Header="Baseline"><!-- Existing --></TabItem>
          <TabItem Header="Demo"><!-- Existing --></TabItem>
        </TabControl>
      </TabItem>

      <!-- DISCOVER Category -->
      <TabItem Header="DISCOVER">
        <TabControl>
          <TabItem Header="Hunt"><!-- Existing --></TabItem>
          <TabItem Header="Sweep"><!-- Existing --></TabItem>
          <TabItem Header="Network"><!-- NEW --></TabItem>
          <TabItem Header="Browser"><!-- NEW --></TabItem>
        </TabControl>
      </TabItem>

      <!-- ANALYZE Category -->
      <TabItem Header="ANALYZE">
        <TabControl>
          <TabItem Header="Persist"><!-- Existing --></TabItem>
          <TabItem Header="Event Logs"><!-- NEW --></TabItem>
        </TabControl>
      </TabItem>

      <!-- RESPOND Category -->
      <TabItem Header="RESPOND">
        <TabControl>
          <TabItem Header="Cleanup"><!-- Existing --></TabItem>
          <TabItem Header="Harden"><!-- Existing --></TabItem>
        </TabControl>
      </TabItem>

      <!-- REPORT Category -->
      <TabItem Header="REPORT">
        <TabControl>
          <TabItem Header="Case"><!-- Existing --></TabItem>
          <TabItem Header="Export"><!-- Enhanced --></TabItem>
          <TabItem Header="Help"><!-- Existing --></TabItem>
        </TabControl>
      </TabItem>

    </TabControl>

    <!-- Status Bar -->
    <Border Grid.Row="2" Background="#0A1214" Padding="8">
      <TextBlock x:Name="GlobalStatusText" Text="Ready"/>
    </Border>

  </Grid>
</Window>
```

---

## Migration Notes

### v1.0 → v2.0 Compatibility

**Case Files:**
- v1.0 case.json fully compatible
- New fields (network, browser, events) are optional
- Old cases load without errors

**UI Changes:**
- Tabs reorganized into categories
- All v1.0 features still accessible
- No functionality removed

**First Launch:**
- Show "What's New in v2.0" dialog
- Highlight new DISCOVER and ANALYZE categories
- Offer quick tour (skip button)

---

## Testing Strategy

### Unit Tests
- NetworkScanner.cs - connection parsing
- BrowserParser.cs - SQLite/JSON parsing
- EventLogParser.cs - event XML parsing
- CsvExporter.cs - CSV formatting

### Integration Tests
- Focus highlighting across new tabs
- Case timeline integration
- Export format validation

### Performance Tests
- Large network connection lists (1000+ connections)
- Browser history parsing (100,000+ entries)
- Event log parsing (50,000+ events)
- Memory usage monitoring

### User Acceptance Tests
- First-time user flow (no prior v1.0 experience)
- Existing user migration (v1.0 → v2.0)
- Workflow testing (Hunt → Network → Browser → Export)

---

## Success Metrics

**v2.0 Targets:**
- 5 categories, 14 total tabs
- <10 second scan time for all new tabs
- <1 MB avg case file size (with new data)
- 90% user satisfaction with UI redesign
- <5% bug regression rate
- 100% v1.0 case compatibility

---

## Release Checklist

### Alpha (Week 14-15)
- [ ] UI redesign implemented
- [ ] Network tab functional
- [ ] Internal testing complete
- [ ] Screenshot mockups created

### Beta (Week 16-20)
- [ ] All Phase 1 features complete
- [ ] Browser tab functional
- [ ] Event Logs tab functional
- [ ] Enhanced export functional
- [ ] Beta .exe published
- [ ] Bug tracker set up

### v2.0.0 Release (Week 21-22)
- [ ] All bugs fixed
- [ ] Documentation updated (README, PLAN, CHANGELOG)
- [ ] Screenshots captured
- [ ] Video demo recorded
- [ ] Blog post written
- [ ] Production .exe published
- [ ] GitHub release created

---

## Next Steps

**Ready to Begin Phase 1 Development:**

1. **Week 1-2 Tasks:**
   - Refactor MainWindow.axaml for categorized tabs
   - Create reusable component library
   - Set up Services folder structure

2. **Immediate Actions:**
   - Create feature branch: `feature/v2.0-categorized-tabs`
   - Update .gitignore for new folders
   - Install any needed NuGet packages

**Shall we begin with Week 1-2 foundation work?**
