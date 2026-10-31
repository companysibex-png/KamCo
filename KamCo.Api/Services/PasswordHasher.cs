using System.Security.Cryptography;

namespace KamCo.Api.Services;

/// <summary>Abstraction for password hashing so it can be mocked in tests.</summary>
public interface IPasswordHasher
{
    (string Hash, string Salt) Hash(string password);
    bool Verify(string password, string storedHash, string storedSalt);
}

/// <summary>
/// Password hashing with PBKDF2 (SHA-256, 100k iterations) — the approach recommended
/// by OWASP for ASP.NET Core apps. Salt is unique per user.
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;   // bytes
    private const int KeySize = 32;    // bytes
    private const int Iterations = 100_000;

    public (string Hash, string Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string storedHash, string storedSalt)
    {
        var salt = Convert.FromBase64String(storedSalt);
        var expected = Convert.FromBase64String(storedHash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        // Constant-time comparison to avoid timing attacks.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
