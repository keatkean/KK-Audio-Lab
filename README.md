# KK Audio Lab — Local IIS Deployment Lab

Welcome to the **KK Audio Lab** deployment lab. In this lab, you will publish and host a full-stack **.NET 10** application—an **ASP.NET Core 10 Web API** backend and a **Blazor WebAssembly** frontend—on your local **Windows 11 IIS (Internet Information Services)** web server.

---

## Lab Guide

- **[Step-by-Step IIS Hosting Guide (Windows 11)](IIS_HOSTING_GUIDE.md)** — Follow this guide from Prerequisites through Verification.

---

## Application Architecture

| Component | Folder | Technology | Local IIS URL |
|---|---|---|---|
| **Backend API** | `Backend/` | ASP.NET Core 10 Web API, LiteDB (`kkaudiolab.db`), JWT Auth, HTTP 206 Audio Streaming | `http://localhost:5100` |
| **Frontend App** | `Frontend/` | Blazor WebAssembly (`.NET 10`) SPA | `http://localhost:5200` |

> **Note:** `Backend/Program.cs` is already configured for IIS hosting (`UseUrls` has been removed so IIS/ANCM manages the port binding). Keep ports `5100` (Backend) and `5200` (Frontend) on `localhost` so the existing CORS policy and API URLs work without code changes.

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

## Verification Checklist (Lab Completion)

Before submitting or demonstrating your lab, make sure **Visual Studio** and `run.bat` are closed, then test in your browser:

- [ ] `http://localhost:5100/` loads the **KK Audio Lab Health & Status Dashboard**
- [ ] `http://localhost:5100/health` and `http://localhost:5100/api/tracks` return JSON
- [ ] `http://localhost:5200/` loads the Blazor WebAssembly frontend
- [ ] Navigating to `http://localhost:5200/playlists` and pressing **F5** reloads cleanly (no `404` error)
- [ ] Signing in on `/auth` with `student@kkaudiolab.sg` / `Password123!`, playing a track, and scrubbing the audio progress bar works

---

## Quick Reference: Local Dev vs. IIS

- **Local Development (Visual Studio / CLI):** Run `run.bat` from the repo root to build `KKAudioLab.slnx` and start both apps with `dotnet run`.
- **Local IIS Production Simulation:** Stop any running `dotnet run` terminals first so ports `5100` and `5200` are free for IIS.
