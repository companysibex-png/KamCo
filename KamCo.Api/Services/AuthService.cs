using KamCo.Api.Common;
using KamCo.Api.Data;
using KamCo.Api.DTOs.Auth;
using KamCo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KamCo.Api.Services;

/// <summary>Business logic for registration and login.</summary>
public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
}

public class AuthService(AppDbContext db, IPasswordHasher hasher, ITokenService tokenService) : IAuthService
{
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(u => u.Email == email);
        if (exists)
            throw new ConflictException($"A user with email '{email}' is already registered.");

        var (hash, salt) = hasher.Hash(dto.Password);

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = "User" // Public sign-ups always get the "User" role.
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (token, expiresAt) = tokenService.CreateToken(user);
        return ToResponse(user, token, expiresAt);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !hasher.Verify(dto.Password, user.PasswordHash, user.PasswordSalt))
            throw new BadRequestException("Invalid email or password.");

        var (token, expiresAt) = tokenService.CreateToken(user);
        return ToResponse(user, token, expiresAt);
    }

    private static AuthResponseDto ToResponse(User user, string token, DateTime expiresAt) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role,
        Token = token,
        ExpiresAt = expiresAt
    };
}
