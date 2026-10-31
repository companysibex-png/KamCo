using System.ComponentModel.DataAnnotations;

namespace KamCo.Api.DTOs.Auth;

/// <summary>Payload for POST /api/auth/register.</summary>
public class RegisterDto
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Minimum 6 characters.</summary>
    [Required, MinLength(6), MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Payload for POST /api/auth/login.</summary>
public class LoginDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Returned on successful register / login.</summary>
public class AuthResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    /// <summary>Bearer token to send in the Authorization header.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Token expiry as UTC ISO-8601.</summary>
    public DateTime ExpiresAt { get; set; }
}
