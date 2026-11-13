# 🎵 KK Audio Lab — Full-Stack Debugging Scenario Guide (Student Handout)

## 1. Scenario Briefing: The Pre-Release Incident

Welcome to the **KK Audio Lab** Application team! You have just joined the company today as a new **Full-Stack Software Development Intern**, excited to start your first day on the job.

We are preparing to launch our **KK Audio Lab Music Streaming Portal**—a full-stack application built with an **ASP.NET Core 10 Web API** backend (powered by **LiteDB** and **HTTP 206 byte-range audio streaming**) and a **Blazor WebAssembly** frontend.

However, as soon as you sit down at your desk, your Lead Developer rushes over with an urgent crisis: right before today's release, the previous intern pushed a broken commit to the `main` branch and left for vacation. With the live product demonstration happening in less than two hours, your Lead Developer is counting on you to step in and rescue the launch!

Currently, when the team tries to run the project:
1. **The solution fails to compile (`dotnet build` fails with 5 build errors).**
2. **Even after the build is fixed, 5 critical features are broken at runtime.**

Your mission as the new intern is to fix all **5 build errors** so the application compiles and launches, and then investigate and resolve all **5 runtime bug tickets** before the release deadline.

### ⚠️ Rules of Engagement
- **Do NOT modify core database models or DTO structures**: You do **not** need to change property types in `Track.cs` or `FrontendDtos.cs`. Every bug can be fixed cleanly at the source in **1–2 lines of code**.
- **Use Real Debugging Tools**: For Phase 2, there are **zero compiler errors**. You must launch the app and use your browser's **F12 Developer Tools** (**Console** and **Network** tabs) to diagnose what is failing between the Frontend and Backend.

---

## 2. System Architecture Quick Reference

| Component | Local URL | Key Files & Responsibilities |
| :--- | :--- | :--- |
| **Backend Web API** | `http://localhost:5100` | `Program.cs` (DI, CORS, JWT config)<br>`DbSeeder.cs` (Seeds default user & 5 MP3 tracks)<br>`TracksController.cs` (REST endpoints & audio streaming)<br>`TrackService.cs` (LiteDB CRUD & L1/L2 cache eviction)<br>`AuthService.cs` (BCrypt login & JWT generation) |
| **Frontend Blazor WASM** | `http://localhost:5200` | `Pages/Home.razor` (Music catalog & search/filter)<br>`Pages/Playlists.razor` (Student playlist manager)<br>`Pages/Auth.razor` (Student login & registration)<br>`Services/MusicApiClient.cs` (HTTP client to Backend) |

### Default Demo Student Credentials
- **Email**: `student@kkaudiolab.sg`
- **Password**: `Password123!` *(or click **🚀 One-Click Demo Sign In** on the `/auth` page)*
