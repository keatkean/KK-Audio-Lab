using Frontend;
using Frontend.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// [EDUCATIONAL NOTE - Lab 8 S1 & S2]:
// Blazor WebAssembly .NET 10 Program Architecture:
// 1. WebAssemblyHostBuilder initializes the client-side WebAssembly execution environment
//    in the browser's JavaScript sandbox.
// 2. HttpClient BaseAddress: We configure the primary HttpClient to point directly to the
//    local ASP.NET Core 10 WebAPI server at http://localhost:5100.
// 3. AuthenticationStateProvider: Registering AuthStateService both as itself and as
//    AuthenticationStateProvider enables Blazor's cascading authorization state and <AuthorizeView>.
// 4. State Management: ClientCacheService and PlayerStateService are registered as Scoped
//    singletons across the lifetime of the browser tab.

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure Backend API HttpClient pointing to http://localhost:5100
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5100")
});

// Register Core Authorization Infrastructure
builder.Services.AddAuthorizationCore();

// Register Custom Authentication & Session Service
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateService>());

// Register Application Services
builder.Services.AddScoped<ClientCacheService>();
builder.Services.AddScoped<PlayerStateService>();
builder.Services.AddScoped<MusicApiClient>();

await builder.Build().RunAsync();
