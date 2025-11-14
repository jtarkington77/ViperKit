# ViperKit Branding Guide

**Project:** ViperKit  
**Owner:** Jeremy Tarkington  
**Tagline:** VenomousViper Incident Toolkit

This doc defines how ViperKit should look and feel so every future UI change stays on-brand.

---

## 1. Logo & Identity

- Primary logo file: `assets/logo.png`
- Use the snake head + VENOMOUSVIPER wordmark as the **hero** on:
  - The Dashboard tab
  - Any splash/about screens
- App name to show in UI: **ViperKit**
  - Example lockup: `ViperKit — VENOMOUSVIPER Incident Toolkit`

Logo usage rules:

- Dark background only (no white backgrounds).
- Keep a small glow/soft shadow around the logo when possible.
- Do not stretch, squash, or recolor the snake outline.

---

## 2. Color Palette

Colors are sampled directly from `assets/logo.png`.

### Core

- **Background Deep Teal:** `#001517`  
  - Main window background, panel backgrounds.
- **Viper Teal (Accent/Glow):** ~`#00FFF8`  
  - Buttons, highlights, tab indicator, links.
  - Anything that should draw the operator’s eye.

### Supporting

- **Muted Teal:** values between `#00C7C0` and `#00E5D3`  
  - Secondary borders, subtle lines, icons.
- **Text**
  - Primary text: `#E5F8F8` (near-white teal)
  - Muted text / labels: `#8BA9A9`
  - Error/warning: can use a desaturated red or amber later, but
    background must stay dark teal (no bright white panels).

Rules:

- Never mix random rainbow colors.
- Everything should feel like **one coherent teal-on-dark cyber UI**.

---

## 3. Typography

Goal: professional IR tool, not a gamer overlay.

Preferred style (Windows-safe fallbacks):

- Primary: a clean sans-serif (e.g., **Segoe UI**, **Inter**, **Roboto** if bundled).
- Use:
  - **Title / headings:** slightly larger, semi-bold.
  - **Body text:** regular weight; high contrast against background.
  - **Code/paths/registry keys:** monospace (e.g., Consolas).

No cursive, decorative, or “esports” fonts.

---

## 4. Layout & Components

Overall feel:

- Flat + minimal with subtle glow, not heavy gradients.
- Plenty of breathing room between sections.
- Clear separation of “view” vs “actions”.

Components:

- **Tabs:** horizontal or vertical, but:
  - Active tab highlighted with Viper teal.
  - Inactive tabs in muted teal/gray.
- **Cards / panels:**
  - Slight border or drop shadow.
  - Rounded corners are OK but keep it subtle (not pill-shaped everywhere).
- **Buttons:**
  - Primary: Viper teal background on dark surface.
  - Hover: slightly brighter or stronger glow.
  - Disabled: muted gray/teal with no glow.

---

## 5. The “Viper” Vibe

The app should feel:

- Focused and **danger-aware** (you’re poking malware, not browsing memes).
- Calm, readable, no clutter.
- Like a custom operator console you’d want on a second monitor.

Do:

- Use subtle circuit/hex patterns in the background only if they don’t hurt readability.
- Keep icons simple and consistent line weight.

Don’t:

- Add animated snakes, flames, or meme graphics.
- Use neon on pure black with zero contrast (eye strain).

---

## 6. Branding in the Dashboard

The **Dashboard** tab must include:

- Logo (`assets/logo.png`) at the top or upper-left.
- Title: `ViperKit`
- Subtitle: `VENOMOUSVIPER Incident Toolkit`
- One short paragraph describing what ViperKit does (can reuse text from `README.md`).
- Quick links/buttons to:
  - Open logs folder
  - Open reports folder
  - Open Help tab

This is the first thing an operator sees, so it sets the tone for the whole toolkit.

---

## 7. File References

- Logo: `assets/logo.png`
- Branding spec (this file): `BRANDING.md`
- High-level plan & UX: `PLAN.md`
- Public-facing description: `README.md`

Any future UI work, mockup, or XAML layout should match these rules unless this document is intentionally updated.
