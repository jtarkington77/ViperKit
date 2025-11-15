# ViperKit

**ViperKit** is a portable, offline-first **incident response toolkit** for Windows, built for MSPs and IT teams that don’t have a full-time security staff.

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

## Status

> ## Current Status

- **UI Shell:** Avalonia UI with header branding, logo, and tab layout:
  - Dashboard, Hunt, Persist, Sweep, Remediate, Cleanup, Harden, Case, Help.
- **Hunt (M1 – early stub):**
  - Accepts IOC input with a type selector (Auto-detect, File/Path, Hash, Domain/URL, IP, Registry).
  - For **File / Path** IOCs:
    - Checks whether the file exists on disk.
    - Shows basic file metadata (size, created, modified).
  - All hunts are recorded to a simple journal at `logs/Hunt.log` in the app directory.
  - Other IOC types are currently echoed as “demo” and will be wired to real collectors in later milestones.




---

## Planned Tabs (Phase 1)

- **Dashboard** – Landing view, branding, quick instructions  
- **Hunt** – IOC in → findings out, Trace View, Evidence add  
- **Persist** – Deep persistence map (autoruns, WMI, COM, etc.)  
- **Sweep** – Recent-change radar (files, services, tasks, firewall, etc.)  
- **Remediate** – Playbooks with preview → apply → undo  
- **Cleanup** – Post-removal hygiene and resets  
- **Harden** – Standard/Strict security profiles with rollback  
- **Case** – Evidence cart, reports, artifact bundle  
- **Help** – Safety rules, shortcuts, folder locations  

Details and acceptance criteria for each tab live in `PLAN.md`.

---

## Repository Layout (M0)

> This will grow **only** when a milestone needs it. No random folders.

```text
ViperKit/
  PLAN.md          # Definitive scope & milestones
  README.md        # Public-facing overview
  assets/
    Logo.png       # VENOMOUSVIPER branding for ViperKit
```
