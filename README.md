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

> **Current milestone: M0 – Plan & Wireframe**

- Scope, UX, and data model are defined in `PLAN.md`.
- No production code yet – only planning and branding assets.
- Next milestone: **M1 – Hunt tab MVP**.

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
