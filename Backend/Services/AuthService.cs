using System.Security.Claims;
using System.Text;
using Backend.DTOs;
using Backend.Models;
using Backend.Services.Interfaces;
using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Services;

// [EDUCATIONAL NOTE - Lab 8 S1]:
// Password Security & Salt Hashing:
// We use BCrypt.Net.BCrypt to hash incoming passwords with an automatically generated work-factor salt.
// A raw SHA-256 hash can be brute-forced with modern GPUs in minutes. BCrypt is intentionally
// computationally expensive (adaptive cost factor), mitigating dictionary and rainbow-table attacks.
//
// [EDUCATIONAL NOTE - Lab 8 S2]:
// Modern Token-Based Authentication with JsonWebTokenHandler:
// .NET 10 uses Microsoft.IdentityModel.JsonWebTokens (JsonWebTokenHandler) which is up to 30% faster
// and allocates significantly less memory than the legacy JwtSecurityTokenHandler.
// The resulting compact JWT string contains signed Claims (ID, Email, Role) that our API verifies
// on subsequent requests via standard Bearer authentication.
public class AuthService : IAuthService
{
    private readonly ILiteDatabase _db;
    private readonly IConfiguration _config;
    private readonly string _jwtKey;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly int _jwtExpiryHours;

    public AuthService(ILiteDatabase db, IConfiguration config)
    {
        _db = db;
        _config = config;

        var jwtSection = _config.GetSection("JwtSettings");
        _jwtKey = jwtSection["SecretKey"] ?? "KkAudioLab_SuperSecretKey_ForPolytechnicLabCourse12345!";
        _jwtIssuer = jwtSection["Issuer"] ?? "KkAudioLab";
        _jwtAudience = "KkAudioLab_StudentPortal";
        _jwtExpiryHours = int.TryParse(jwtSection["ExpiryHours"], out var hours) ? hours : 24;
    }

    public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
    {
        var users = _db.GetCollection<User>("users");
        users.EnsureIndex(u => u.Email, unique: true);

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        // Check if email already registered
        if (users.Exists(u => u.Email == normalizedEmail))
        {
            throw new InvalidOperationException($"A student or user with email '{dto.Email}' already exists.");
        }

        // Salt and hash the password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 11);

        var newUser = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = dto.FullName.Trim(),
            Role = "Student",
            CreatedAtUtc = DateTime.UtcNow
        };

        users.Insert(newUser);

        var token = GenerateJwtToken(newUser);
        return Task.FromResult(new AuthResponseDto(token, newUser.Id, newUser.Email, newUser.FullName, newUser.Role));
    }

    public Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
    {
        var users = _db.GetCollection<User>("users");
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = users.FindOne(u => u.Email == normalizedEmail);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var token = GenerateJwtToken(user);
        return Task.FromResult(new AuthResponseDto(token, user.Id, user.Email, user.FullName, user.Role));
    }

    public Task<User?> GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        var users = _db.GetCollection<User>("users");
        var user = users.FindById(userId);
        return Task.FromResult<User?>(user);
    }

    private string GenerateJwtToken(User user)
    {
        var handler = new JsonWebTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));

        var claims = new Dictionary<string, object>
        {
            [ClaimTypes.NameIdentifier] = user.Id,
            [ClaimTypes.Email] = user.Email,
            [ClaimTypes.Name] = user.FullName,
            [ClaimTypes.Role] = user.Role,
            ["sub"] = user.Id
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            Expires = DateTime.UtcNow.AddHours(_jwtExpiryHours),
            Issuer = _jwtIssuer,
            Audience = _jwtAudience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return handler.CreateToken(descriptor);
    }
}
