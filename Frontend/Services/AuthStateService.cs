using System.Security.Claims;
using System.Text.Json;
using Frontend.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Frontend.Services;

// [EDUCATIONAL NOTE - Lab 8 S1 & S2]:
// Blazor WebAssembly Authentication Architecture:
// 1. AuthenticationStateProvider is the core extensibility point in Blazor for auth.
// 2. We store the JSON Web Token (JWT) in the browser's localStorage using IJSRuntime.
// 3. To build the ClaimsPrincipal, we parse the JWT's base64url-encoded payload
//    without needing third-party libraries.
// 4. Calling NotifyAuthenticationStateChanged notifies all <AuthorizeView> and
//    <CascadingAuthenticationState> components in the render tree to re-evaluate user roles.

public class AuthStateService : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private const string TokenKey = "kk_audio_jwt";
    private const string UserKey = "kk_audio_user";
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public AuthStateService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
            if (string.IsNullOrWhiteSpace(token))
            {
                return new AuthenticationState(Anonymous);
            }

            var claims = ParseClaimsFromJwt(token);
            // Check expiration claim ("exp") if present
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim != null && long.TryParse(expClaim.Value, out var expSeconds))
            {
                var expDate = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                if (expDate <= DateTimeOffset.UtcNow)
                {
                    // Token expired: clean up
                    await LogoutAsync();
                    return new AuthenticationState(Anonymous);
                }
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);
            return new AuthenticationState(user);
        }
        catch
        {
            return new AuthenticationState(Anonymous);
        }
    }

    /// <summary>
    /// Authenticates the user in client state after receiving AuthResponseDto from the API.
    /// </summary>
    public async Task LoginAsync(AuthResponseDto response)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, response.Token);
        var json = JsonSerializer.Serialize(response);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", UserKey, json);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Clears student session tokens from localStorage and notifies Blazor state change.
    /// </summary>
    public async Task LogoutAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserKey);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    /// <summary>
    /// Retrieves the current JWT string to attach to HTTP Authorization headers.
    /// </summary>
    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves cached student user info from localStorage.
    /// </summary>
    public async Task<AuthResponseDto?> GetSavedUserAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", UserKey);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<AuthResponseDto>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decodes a JWT payload without external libraries by padding and deserializing base64 JSON.
    /// </summary>
    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        var parts = jwt.Split('.');
        if (parts.Length < 2) return claims;

        var payload = parts[1];
        // Base64Url padding
        payload = payload.Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var jsonBytes = Convert.FromBase64String(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs != null)
        {
            foreach (var kvp in keyValuePairs)
            {
                var value = kvp.Value?.ToString() ?? string.Empty;
                var type = kvp.Key switch
                {
                    "sub" => ClaimTypes.NameIdentifier,
                    "nameid" => ClaimTypes.NameIdentifier,
                    "email" => ClaimTypes.Email,
                    "name" => ClaimTypes.Name,
                    "role" => ClaimTypes.Role,
                    _ => kvp.Key
                };
                claims.Add(new Claim(type, value));
            }
        }

        return claims;
    }
}
