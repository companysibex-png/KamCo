using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamCo.Api.Models;

/// <summary>Order lifecycle status.</summary>
public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Shipped = "Shipped";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Pending, Paid, Shipped, Cancelled };
}

/// <summary>A customer order containing one or more order items.</summary>
public class Order
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Pending / Paid / Shipped / Cancelled.</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = OrderStatus.Pending;

    /// <summary>Server-side computed sum of all items. Never trusted from the client.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
