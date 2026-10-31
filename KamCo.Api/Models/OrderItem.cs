using System.ComponentModel.DataAnnotations.Schema;

namespace KamCo.Api.Models;

/// <summary>A single line item of an order. Price is captured at purchase time.</summary>
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }

    /// <summary>Unit price copied from the product when the order was placed.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }
}
