using System.ComponentModel.DataAnnotations;

namespace KamCo.Api.Models;

/// <summary>
/// Application user for authentication and order ownership.
/// </summary>
public class User
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Never exposed through the API. Stores the PBKDF2 hash only.</summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Random per-user salt used when hashing the password.</summary>
    [Required]
    public string PasswordSalt { get; set; } = string.Empty;

    /// <summary>"Admin" or "User".</summary>
    [Required, MaxLength(20)]
    public string Role { get; set; } = "User";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
