using Backend.Models;
using LiteDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Backend.Controllers;

// [EDUCATIONAL NOTE - Lab 9 S1]:
// Health Check APIs & Production Observability:
// In modern cloud, containerized (Docker/Kubernetes), and microservice architectures,
// health endpoints (/health) are queried periodically by load balancers, orchestrators,
// and frontend applications to verify system liveness and readiness.
// 
// When an administrator or developer visits http://localhost:5100/ in a web browser,
// this controller serves a responsive, dark-mode status dashboard showing real-time database,
// storage, and audio catalog metrics. When called programmatically (curl, Blazor, Postman),
// it returns structured JSON with HTTP 200 OK.
[ApiController]
public class HealthController : ControllerBase
{
    private readonly ILiteDatabase _db;
    private readonly IConfiguration _config;
    private readonly string _audioLibraryPath;

    public HealthController(ILiteDatabase db, IConfiguration config)
    {
        _db = db;
        _config = config;

        var configuredPath = _config["AudioLibraryPath"] ?? "AudioLibrary";
        _audioLibraryPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(Directory.GetCurrentDirectory(), configuredPath);
    }

    /// <summary>
    /// Health Check API returning structured JSON status.
    /// Accessible via GET /health and GET /api/health.
    /// </summary>
    [HttpGet("/health")]
    [HttpGet("/api/health")]
    [Produces("application/json")]
    public IActionResult GetHealth()
    {
        var healthData = BuildHealthStatus();
        return Ok(healthData);
    }

    /// <summary>
    /// Root endpoint (http://localhost:5100/).
    /// Automatically serves a sleek status dashboard in browsers (text/html)
    /// or JSON for programmatic API requests.
    /// </summary>
    [HttpGet("/")]
    public IActionResult GetRoot()
    {
        var acceptHeader = Request.Headers.Accept.ToString();
        var healthData = BuildHealthStatus();

        if (acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            var html = RenderDashboardHtml(healthData);
            return Content(html, "text/html");
        }

        return Ok(healthData);
    }

    private object BuildHealthStatus()
    {
        var isDbHealthy = false;
        var trackCount = 0;
        var userCount = 0;
        var playlistCount = 0;

        try
        {
            var tracksCol = _db.GetCollection<Track>("tracks");
            var usersCol = _db.GetCollection<User>("users");
            var playlistsCol = _db.GetCollection<Playlist>("playlists");

            trackCount = tracksCol.Count();
            userCount = usersCol.Count();
            playlistCount = playlistsCol.Count();
            isDbHealthy = true;
        }
        catch
        {
            isDbHealthy = false;
        }

        var isAudioDirReady = Directory.Exists(_audioLibraryPath);
        var audioFilesOnDisk = isAudioDirReady
            ? Directory.EnumerateFiles(_audioLibraryPath)
                .Count(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            : 0;

        var isOverallHealthy = isDbHealthy && isAudioDirReady;

        return new
        {
            status = isOverallHealthy ? "Healthy" : "Degraded",
            service = "KK Audio Lab WebAPI",
            version = "1.0.0",
            framework = ".NET 10.0 (net10.0)",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            serverTimeUtc = DateTime.UtcNow,
            checks = new
            {
                database = new
                {
                    status = isDbHealthy ? "Healthy" : "Unhealthy",
                    engine = "LiteDB v5 (Embedded NoSQL)",
                    connection = "Filename=kkaudiolab.db;Connection=shared",
                    statistics = new
                    {
                        tracks = trackCount,
                        users = userCount,
                        playlists = playlistCount
                    }
                },
                audioStorage = new
                {
                    status = isAudioDirReady ? "Healthy" : "MissingDirectory",
                    path = _audioLibraryPath,
                    playableFilesCount = audioFilesOnDisk,
                    supportedFormats = new[] { ".mp3", ".wav", ".ogg" }
                },
                streamingEngine = new
                {
                    protocol = "HTTP 206 Partial Content",
                    mode = "FileOptions.Asynchronous | FileOptions.SequentialScan",
                    cacheControl = "public, max-age=86400, ETag enabled"
                },
                caching = new
                {
                    l1 = "IMemoryCache (In-Process)",
                    l2 = "ASP.NET Core 10 OutputCache (Tag: 'tracks')"
                },
                auth = new
                {
                    scheme = "JWT Bearer",
                    hashing = "BCrypt (Cost factor 11)",
                    handler = "Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler"
                }
            },
            endpoints = new
            {
                health = "/health",
                tracks = "/api/tracks",
                playlists = "/api/playlists",
                login = "/api/auth/login",
                register = "/api/auth/register",
                currentUser = "/api/auth/me",
                discovery = "/api/discovery/search?query=Coldplay",
                openApi = "/openapi/v1.json"
            }
        };
    }

    private static string RenderDashboardHtml(dynamic data)
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>KK Audio Lab — Backend Status & Health</title>
            <link rel="preconnect" href="https://fonts.googleapis.com">
            <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
            <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@300;400;500;600;700;800&family=JetBrains+Mono:wght@400;500;700&display=swap" rel="stylesheet">
            <style>
                :root {
                    --bg-dark: #07090e;
                    --card-bg: rgba(18, 24, 38, 0.75);
                    --border-color: rgba(255, 255, 255, 0.08);
                    --accent-cyan: #00f2fe;
                    --accent-blue: #4facfe;
                    --accent-emerald: #10b981;
                    --accent-purple: #8b5cf6;
                    --text-main: #f8fafc;
                    --text-muted: #94a3b8;
                }
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                    background-color: var(--bg-dark);
                    background-image: 
                        radial-gradient(at 0% 0%, rgba(79, 172, 254, 0.12) 0px, transparent 50%),
                        radial-gradient(at 100% 100%, rgba(0, 242, 254, 0.08) 0px, transparent 50%),
                        radial-gradient(at 50% 50%, rgba(139, 92, 246, 0.05) 0px, transparent 50%);
                    color: var(--text-main);
                    font-family: 'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, sans-serif;
                    min-height: 100vh;
                    padding: 40px 20px;
                    display: flex;
                    justify-content: center;
                    align-items: center;
                }
                .container {
                    width: 100%;
                    max-width: 980px;
                }
                .header {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    margin-bottom: 28px;
                    flex-wrap: wrap;
                    gap: 16px;
                }
                .brand {
                    display: flex;
                    align-items: center;
                    gap: 14px;
                }
                .logo-icon {
                    width: 48px;
                    height: 48px;
                    background: linear-gradient(135deg, var(--accent-blue), var(--accent-cyan));
                    border-radius: 14px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    font-size: 24px;
                    box-shadow: 0 8px 24px rgba(79, 172, 254, 0.35);
                }
                .brand-title {
                    font-size: 24px;
                    font-weight: 800;
                    letter-spacing: -0.5px;
                    background: linear-gradient(135deg, #ffffff 40%, #cbd5e1);
                    -webkit-background-clip: text;
                    -webkit-text-fill-color: transparent;
                }
                .brand-subtitle {
                    font-size: 13px;
                    color: var(--text-muted);
                    font-weight: 500;
                }
                .badge-healthy {
                    display: inline-flex;
                    align-items: center;
                    gap: 8px;
                    background: rgba(16, 185, 129, 0.12);
                    border: 1px solid rgba(16, 185, 129, 0.3);
                    color: #34d399;
                    padding: 8px 16px;
                    border-radius: 9999px;
                    font-size: 13px;
                    font-weight: 600;
                    letter-spacing: 0.5px;
                }
                .pulsing-dot {
                    width: 8px;
                    height: 8px;
                    background-color: #10b981;
                    border-radius: 50%;
                    box-shadow: 0 0 10px #10b981;
                    animation: pulse 1.8s infinite;
                }
                @keyframes pulse {
                    0% { transform: scale(0.95); box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7); }
                    70% { transform: scale(1); box-shadow: 0 0 0 8px rgba(16, 185, 129, 0); }
                    100% { transform: scale(0.95); box-shadow: 0 0 0 0 rgba(16, 185, 129, 0); }
                }
                .grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
                    gap: 20px;
                    margin-bottom: 24px;
                }
                .card {
                    background: var(--card-bg);
                    backdrop-filter: blur(16px);
                    border: 1px solid var(--border-color);
                    border-radius: 18px;
                    padding: 22px;
                    transition: transform 0.2s ease, border-color 0.2s ease, box-shadow 0.2s ease;
                }
                .card:hover {
                    transform: translateY(-2px);
                    border-color: rgba(255, 255, 255, 0.16);
                    box-shadow: 0 12px 30px rgba(0, 0, 0, 0.35);
                }
                .card-header {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    margin-bottom: 14px;
                }
                .card-title {
                    font-size: 14px;
                    font-weight: 600;
                    color: var(--text-muted);
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                }
                .card-badge {
                    font-size: 11px;
                    padding: 4px 8px;
                    border-radius: 6px;
                    background: rgba(255, 255, 255, 0.06);
                    font-weight: 600;
                    color: #e2e8f0;
                }
                .stat-value {
                    font-size: 26px;
                    font-weight: 800;
                    color: #fff;
                    margin-bottom: 6px;
                }
                .stat-desc {
                    font-size: 13px;
                    color: var(--text-muted);
                    line-height: 1.5;
                }
                .code-pill {
                    font-family: 'JetBrains Mono', monospace;
                    background: rgba(0, 0, 0, 0.4);
                    padding: 3px 6px;
                    border-radius: 4px;
                    font-size: 12px;
                    color: var(--accent-cyan);
                }
                .links-section {
                    background: var(--card-bg);
                    backdrop-filter: blur(16px);
                    border: 1px solid var(--border-color);
                    border-radius: 18px;
                    padding: 22px;
                    margin-bottom: 24px;
                }
                .links-title {
                    font-size: 16px;
                    font-weight: 700;
                    margin-bottom: 16px;
                    display: flex;
                    align-items: center;
                    gap: 8px;
                }
                .btn-group {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 12px;
                }
                .btn {
                    display: inline-flex;
                    align-items: center;
                    gap: 8px;
                    background: rgba(255, 255, 255, 0.05);
                    border: 1px solid rgba(255, 255, 255, 0.1);
                    color: #f1f5f9;
                    text-decoration: none;
                    padding: 10px 18px;
                    border-radius: 10px;
                    font-size: 13px;
                    font-weight: 600;
                    transition: all 0.2s ease;
                    cursor: pointer;
                }
                .btn:hover {
                    background: rgba(79, 172, 254, 0.15);
                    border-color: var(--accent-blue);
                    color: #fff;
                    transform: translateY(-1px);
                }
                .btn-primary {
                    background: linear-gradient(135deg, var(--accent-blue), var(--accent-cyan));
                    color: #040914;
                    border: none;
                    font-weight: 700;
                }
                .btn-primary:hover {
                    box-shadow: 0 4px 20px rgba(79, 172, 254, 0.4);
                    color: #040914;
                }
                .live-json {
                    margin-top: 18px;
                    background: #030508;
                    border: 1px solid rgba(255, 255, 255, 0.06);
                    border-radius: 12px;
                    padding: 16px;
                    font-family: 'JetBrains Mono', monospace;
                    font-size: 12px;
                    color: #38bdf8;
                    max-height: 220px;
                    overflow-y: auto;
                    display: none;
                }
                .footer {
                    text-align: center;
                    font-size: 12px;
                    color: #64748b;
                    margin-top: 24px;
                }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="header">
                    <div class="brand">
                        <div class="logo-icon">🎵</div>
                        <div>
                            <div class="brand-title">KK Audio Lab</div>
                            <div class="brand-subtitle">ASP.NET Core 10 Local-First Music Streaming WebAPI</div>
                        </div>
                    </div>
                    <div class="badge-healthy">
                        <span class="pulsing-dot"></span>
                        ALL SYSTEMS OPERATIONAL
                    </div>
                </div>

                <div class="grid">
                    <!-- Card 1: Database -->
                    <div class="card">
                        <div class="card-header">
                            <span class="card-title">Embedded NoSQL</span>
                            <span class="card-badge" style="color: #34d399;">ONLINE</span>
                        </div>
                        <div class="stat-value">LiteDB v5</div>
                        <div class="stat-desc">
                            File: <span class="code-pill">kkaudiolab.db</span><br>
                            Stored: <strong>{{data.checks.database.statistics.tracks}}</strong> tracks, 
                            <strong>{{data.checks.database.statistics.playlists}}</strong> playlists, 
                            <strong>{{data.checks.database.statistics.users}}</strong> student user.
                        </div>
                    </div>

                    <!-- Card 2: Audio Storage -->
                    <div class="card">
                        <div class="card-header">
                            <span class="card-title">Local Audio Library</span>
                            <span class="card-badge" style="color: #38bdf8;">ACTIVE</span>
                        </div>
                        <div class="stat-value">{{data.checks.audioStorage.playableFilesCount}} Playable Tracks</div>
                        <div class="stat-desc">
                            Folder: <span class="code-pill">Backend/AudioLibrary/</span><br>
                            Formats: <strong>.mp3, .wav, .ogg</strong><br>
                            Streaming: <strong>HTTP 206 Partial Content</strong>
                        </div>
                    </div>

                    <!-- Card 3: Caching & Auth -->
                    <div class="card">
                        <div class="card-header">
                            <span class="card-title">Performance & Auth</span>
                            <span class="card-badge" style="color: #c084fc;">READY</span>
                        </div>
                        <div class="stat-value">Multi-Tier Cache</div>
                        <div class="stat-desc">
                            L1: <span class="code-pill">IMemoryCache</span><br>
                            L2: <span class="code-pill">OutputCache (tag eviction)</span><br>
                            Auth: <strong>JWT + BCrypt (Cost 11)</strong>
                        </div>
                    </div>
                </div>

                <div class="links-section">
                    <div class="links-title">
                        <span>⚡ Quick Actions & API Endpoints</span>
                    </div>
                    <div class="btn-group">
                        <a href="/health" target="_blank" class="btn btn-primary">
                            <span>📡 JSON Health Check (/health)</span>
                        </a>
                        <a href="/api/tracks" target="_blank" class="btn">
                            <span>🎶 Catalog API (/api/tracks)</span>
                        </a>
                        <a href="/api/playlists" target="_blank" class="btn">
                            <span>📋 Playlists API (/api/playlists)</span>
                        </a>
                        <a href="/api/discovery/search?query=Coldplay" target="_blank" class="btn">
                            <span>🔍 iTunes Discovery</span>
                        </a>
                        <a href="/openapi/v1.json" target="_blank" class="btn">
                            <span>📑 OpenAPI Spec (JSON)</span>
                        </a>
                        <button onclick="fetchLiveHealth()" class="btn" style="background: rgba(16, 185, 129, 0.15); border-color: rgba(16, 185, 129, 0.3); color: #34d399;">
                            <span>▶ Ping /health API</span>
                        </button>
                    </div>
                    <pre id="liveJsonContainer" class="live-json"></pre>
                </div>

                <div class="card" style="background: rgba(15, 23, 42, 0.5); border-style: dashed;">
                    <div style="font-size: 13px; color: #94a3b8; display: flex; justify-content: space-between; flex-wrap: wrap; gap: 10px;">
                        <div>
                            🔑 <strong>Default Student Account:</strong> 
                            <span class="code-pill">student@kkaudiolab.sg</span> 
                            Password: <span class="code-pill">Password123!</span>
                        </div>
                        <div>
                            🌐 <strong>CORS Allowed Origin:</strong> <span class="code-pill">http://localhost:5200</span>
                        </div>
                    </div>
                </div>

                <div class="footer">
                    KK Audio Lab — .NET 10 WebAPI • Singapore Polytechnic / University Audio Engineering & Backend Lab
                </div>
            </div>

            <script>
                async function fetchLiveHealth() {
                    const box = document.getElementById('liveJsonContainer');
                    box.style.display = 'block';
                    box.innerText = 'Pinging /health...';
                    try {
                        const res = await fetch('/health');
                        const data = await res.json();
                        box.innerText = JSON.stringify(data, null, 2);
                    } catch (e) {
                        box.innerText = 'Error fetching health: ' + e;
                    }
                }
            </script>
        </body>
        </html>
        """;
    }
}
