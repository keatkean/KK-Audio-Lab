using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

// [EDUCATIONAL NOTE - Lab 8 S1]:
// DTOs (Data Transfer Objects) establish a strict contract between the client and server.
// They prevent Over-Posting (Mass Assignment) attacks where a malicious client might try
// to send internal fields like 'PasswordHash', 'Role', or 'Id'.
// Validation annotations like [Required] and [EmailAddress] run during ASP.NET Core model binding.

public record RegisterRequestDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6, ErrorMessage = "Password must be at least 6 characters.")] string Password,
    [Required, MinLength(2)] string FullName
);

public record LoginRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponseDto(
    string Token,
    string UserId,
    string Email,
    string FullName,
    string Role
);
