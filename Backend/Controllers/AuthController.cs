using System.Security.Claims;
using Backend.DTOs;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

// [EDUCATIONAL NOTE - Lab 8 S1 & S2]:
// AuthController exposes authentication endpoints for student registration and login.
// Upon successful credentials verification, a signed JSON Web Token (JWT) is returned.
// The frontend stores this token and includes it in the 'Authorization: Bearer <token>' header
// on all subsequent protected requests.
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Registers a new student user and returns a signed JWT.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto dto, CancellationToken ct)
    {
        var response = await _authService.RegisterAsync(dto, ct);
        return Ok(response);
    }

    /// <summary>
    /// Authenticates a student user and returns a signed JWT.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
    {
        var response = await _authService.LoginAsync(dto, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns the currently authenticated student profile using JWT claims.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "User identity claim not found in token." });
        }

        var user = await _authService.GetUserByIdAsync(userId, ct);
        if (user == null)
        {
            return NotFound(new { error = "User record not found." });
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.CreatedAtUtc
        });
    }
}
