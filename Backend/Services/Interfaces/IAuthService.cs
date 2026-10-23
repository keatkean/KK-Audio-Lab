using Backend.DTOs;
using Backend.Models;

namespace Backend.Services.Interfaces;

// [EDUCATIONAL NOTE - Lab 8 S1]:
// Defining services via interfaces adheres to the Dependency Inversion Principle (DIP).
// Controllers depend on 'IAuthService' rather than the concrete 'AuthService' implementation,
// enabling easy unit testing, substitution of alternative identity providers, and mock injection.
public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);
    Task<User?> GetUserByIdAsync(string userId, CancellationToken ct = default);
}
