using System.ComponentModel.DataAnnotations;

namespace KamCo.Api.DTOs.Orders;

public class OrderItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>Unit price at the time the order was placed.</summary>
    public decimal UnitPrice { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UserId { get; set; }
    public string? CustomerName { get; set; }
    public IReadOnlyList<OrderItemDto> Items { get; set; } = Array.Empty<OrderItemDto>();
}

public class CreateOrderDto
{
    [Required, MinLength(1, ErrorMessage = "Order must contain at least one item.")]
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class CreateOrderItemDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000.")]
    public int Quantity { get; set; }
}

/// <summary>Payload for PATCH /api/orders/{id}/status (Admin only).</summary>
public class UpdateOrderStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
}
