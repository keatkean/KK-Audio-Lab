# KK Audio Lab

A music streaming and audio library studio built with **.NET 10**:
- **Backend** (`Backend/`): ASP.NET Core 10 Web API with LiteDB storage (`kkaudiolab.db`), JWT authentication, and HTTP 206 byte-range audio streaming (`http://localhost:5100`).
- **Frontend** (`Frontend/`): Blazor WebAssembly `.NET 10` client (`http://localhost:5200`).

---

## Documentation

- **[IIS Hosting Guide (Windows 11)](IIS_HOSTING_GUIDE.md)** — Complete step-by-step guide to prerequisites, IIS setup, publishing, `web.config`, permissions, and troubleshooting.

---

## Quick Start (Local Development)

Run `run.bat` from the repository root to clean, build `KKAudioLab.slnx`, and launch both services:
- **Backend API:** `http://localhost:5100` (Health dashboard at `/`, JSON health check at `/health`)
- **Frontend App:** `http://localhost:5200`
- **Default Student Login:** `student@kkaudiolab.sg` / `Password123!`

