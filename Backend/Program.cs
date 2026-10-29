using System.Text;
using Backend.Data;
using Backend.Middleware;
using Backend.Services;
using Backend.Services.Interfaces;
using LiteDB;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// [EDUCATIONAL NOTE - Lab 9 S1]:
// ASP.NET Core 10 Minimal Hosting Architecture:
// WebApplication.CreateBuilder initializes the dependency injection container,
// configuration providers (appsettings.json, environment variables), and Kestrel web server.
var builder = WebApplication.CreateBuilder(args);

// Configure fixed local URL to http://localhost:5100
builder.WebHost.UseUrls("http://localhost:5100");

// ---------------------------------------------------------------------------
// 1. DATABASE REGISTRATION (LiteDB - Lab 2 S2)
// ---------------------------------------------------------------------------
// [EDUCATIONAL NOTE - Lab 2 S2]:
// In LiteDB, 'Connection=shared' mode allows multiple threads and external tools
// (e.g., LiteDB Studio) to access the BSON datafile concurrently without file locks.
// Registering ILiteDatabase as a Singleton ensures a single shared database engine instance.
var liteDbConnectionString = builder.Configuration.GetConnectionString("LiteDb")
    ?? "Filename=kkaudiolab.db;Connection=shared";

builder.Services.AddSingleton<ILiteDatabase>(sp => new LiteDatabase(liteDbConnectionString));

// ---------------------------------------------------------------------------
// 2. CACHING SERVICES (IMemoryCache & OutputCache - Lab 8 S2)
// ---------------------------------------------------------------------------
// [EDUCATIONAL NOTE - Lab 8 S2]:
// Multi-Tier Caching Architecture:
// 1. AddMemoryCache registers in-memory L1 cache to avoid BSON disk queries.
// 2. AddOutputCache registers ASP.NET Core 10 HTTP response cache with tag-based eviction.
builder.Services.AddMemoryCache();
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("CatalogCache", policy =>
    {
        policy.Tag("tracks");
        policy.Expire(TimeSpan.FromMinutes(10));
    });
});

// ---------------------------------------------------------------------------
// 3. CORS CONFIGURATION (Lab 8 S1)
// ---------------------------------------------------------------------------
// [EDUCATIONAL NOTE - Lab 8 S1]:
// Cross-Origin Resource Sharing (CORS):
// When the Blazor frontend runs on port 5200 and calls our API on port 5100, the browser blocks
// cross-origin audio streaming unless the server explicitly permits it.
// Crucially, we must expose 'Accept-Ranges', 'Content-Range', and 'Content-Length' so the browser's
// HTML5 <audio> engine can calculate the song's duration and enable timeline scrubbing.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Accept-Ranges", "Content-Range", "Content-Length", "ETag");
    });
});

// ---------------------------------------------------------------------------
// 4. JWT AUTHENTICATION (Lab 8 S1 & S2)
// ---------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "KkAudioLab_SuperSecretKey_ForPolytechnicLabCourse12345!";
var issuer = jwtSettings["Issuer"] ?? "KkAudioLab";
var audience = jwtSettings["Audience"] ?? "KkAudioLab_Audience";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// 5. APPLICATION SERVICES & HTTP CLIENTS (Lab 4, Lab 6, Lab 9)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITrackService, TrackService>();
builder.Services.AddScoped<IPlaylistService, PlaylistService>();

// Typed HttpClient for non-AI third party music metadata lookup (Lab 9 S2)
builder.Services.AddHttpClient<IMetadataDiscoveryService, ItunesMetadataService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.Add("User-Agent", "KkAudioLab-Backend/1.0");
});

builder.Services.AddControllers();

// Built-in .NET 10 OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// ---------------------------------------------------------------------------
// 6. DATABASE SEEDING & AUDIO SYNTHESIS (Lab 14 S1)
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ILiteDatabase>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    DbSeeder.Seed(db, config, logger);
}

// ---------------------------------------------------------------------------
// 7. HTTP REQUEST PIPELINE (Middleware Order - Lab 9 S1)
// ---------------------------------------------------------------------------
// [EDUCATIONAL NOTE - Lab 9 S1]:
// Middleware execution order is critical in ASP.NET Core:
// 1. Exception Handling (catches all downstream exceptions)
// 2. CORS (adds headers before auth blocks requests)
// 3. Output Cache (serves cached responses before hitting auth/controllers)
// 4. Authentication (decodes JWT)
// 5. Authorization (evaluates [Authorize] policies)
// 6. Endpoint Routing (Maps controllers)
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowBlazorFrontend");

app.UseOutputCache();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("""
    ╔══════════════════════════════════════════════════════════════════════════════╗
    ║                       🎵 KK AUDIO LAB WEBAPI IS ONLINE 🎵                     ║
    ╠══════════════════════════════════════════════════════════════════════════════╣
    ║  🚀 Health & Status Dashboard: http://localhost:5100/                         ║
    ║  📡 JSON Health Check:         http://localhost:5100/health                   ║
    ║  🎶 Music Tracks API:          http://localhost:5100/api/tracks               ║
    ║  🔑 Default Student Login:     student@kkaudiolab.sg / Password123!           ║
    ╚══════════════════════════════════════════════════════════════════════════════╝
    """);
    Console.ResetColor();
});

app.Run();
