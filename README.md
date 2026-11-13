# KK Audio Lab — Local IIS Deployment Lab

## Scenario: Your First Deployment Ticket as a DevOps / Software Intern

You have just joined **KK Audio Lab**, a digital music streaming and audio studio company, as a **Junior Software / DevOps Intern**.

The engineering team recently completed the new **.NET 10** platform (**ASP.NET Core 10 Web API** backend + **Blazor WebAssembly** frontend), which currently only runs in developer mode via Visual Studio or `run.bat`. Before the team pushes the release to the company's Windows Server staging environment, your Tech Lead has assigned you a deployment ticket:

> **Ticket #DEPLOY-101:** *"Stand up a production-like local IIS environment on your Windows 11 workstation, deploy both the API and WebAssembly frontend without running Visual Studio, and verify that login, SPA page refreshes, 30 MB track uploads, and audio streaming all work reliably."*

---

## Lab Guide

- **[Step-by-Step IIS Hosting Guide (Windows 11)](IIS_HOSTING_GUIDE.md)** — Your runbook from Prerequisites through Verification and Troubleshooting.

---

## Application Architecture

| Component | Folder | Technology | Local IIS URL |
|---|---|---|---|
| **Backend API** | `Backend/` | ASP.NET Core 10 Web API, LiteDB (`kkaudiolab.db`), JWT Auth, HTTP 206 Audio Streaming | `http://localhost:5100` |
| **Frontend App** | `Frontend/` | Blazor WebAssembly (`.NET 10`) SPA | `http://localhost:5200` |

> **Note:** The codebase is already configured for IIS hosting (`UseUrls` removed from `Backend/Program.cs`, dynamic API host resolution on port `5100` in `Frontend/`, and port-`5200` CORS support in `Backend/`). Both `http://localhost:5200` and LAN IP access (`http://<YOUR-IP>:5200`) work automatically once ports `5100` and `5200` are allowed in Windows Firewall.

---

## What You Will Practice in This Lab

1. **Prepare Windows 11 IIS:** Enable IIS features, then install **IIS URL Rewrite Module 2.1** and the **.NET 10 Hosting Bundle (x64)**.
2. **Publish Release Builds:** Publish both `Backend` and `Frontend` projects into `bin\Release\net10.0\publish\` (or `browser-wasm\publish`) and deploy them under `C:\inetpub\wwwroot\`.
3. **Host the ASP.NET Core Web API:**
   - Configure an **Unmanaged (`No Managed Code`)** Application Pool with **Disable Overlapped Recycle = `True`** for LiteDB safety.
   - Grant **Modify** permissions to `IIS AppPool\KKAudioLabApiPool` so LiteDB (`kkaudiolab.db`) and `AudioLibrary` uploads work.
   - Configure `web.config` `<requestLimits maxAllowedContentLength="31457280" />` for 30 MB audio uploads.
4. **Host the Blazor WebAssembly SPA:**
   - Point the IIS site's Physical Path to the `wwwroot` subfolder (`C:\inetpub\wwwroot\kkaudiolab-web\wwwroot`).
   - Add `web.config` inside `wwwroot\` with WebAssembly MIME types and a **URL Rewrite** SPA fallback rule to `index.html`.
5. **Verify the Deployment:** Confirm all routes, authentication, and audio playback/scrubbing work directly from IIS (with Visual Studio / `dotnet run` closed).

---

## Verification Checklist (Ticket Sign-Off)

Before signing off on **Ticket #DEPLOY-101** and demonstrating to your Tech Lead, make sure **Visual Studio** and `run.bat` are closed, then verify in your browser:

- [ ] `http://localhost:5100/` loads the **KK Audio Lab Health & Status Dashboard**
- [ ] `http://localhost:5100/health` and `http://localhost:5100/api/tracks` return JSON
- [ ] `http://localhost:5200/` loads the Blazor WebAssembly frontend
- [ ] Navigating to `http://localhost:5200/playlists` and pressing **F5** reloads cleanly (no `404` error)
- [ ] Signing in on `/auth` with `student@kkaudiolab.sg` / `Password123!`, playing a track, and scrubbing the audio progress bar works
- [ ] *(Bonus / LAN Test)* Opening `http://<YOUR-LAPTOP-IP>:5200` from a phone on the same Wi-Fi works after opening ports `5100` & `5200` in Windows Defender Firewall

---

## Quick Reference: Local Dev vs. IIS

- **Local Development (Visual Studio / CLI):** Run `run.bat` from the repo root to build `KKAudioLab.slnx` and start both apps with `dotnet run`.
- **Local IIS Production Simulation:** Stop any running `dotnet run` terminals first so ports `5100` and `5200` are free for IIS.
