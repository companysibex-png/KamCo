using System.ComponentModel.DataAnnotations;

namespace KamCo.Api.DTOs.Products;

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateProductDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
    public int StockQuantity { get; set; }

    public string? ImageUrl { get; set; }

    [Required(ErrorMessage = "CategoryId is required.")]
    public int CategoryId { get; set; }
}

public class UpdateProductDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
    public int StockQuantity { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "CategoryId is required.")]
    public int CategoryId { get; set; }
}

/// <summary>Query string parameters for GET /api/products (search, filter, paging).</summary>
public class ProductQueryParameters
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;

    /// <summary>Free-text search on product name (case-insensitive).</summary>
    public string? Search { get; set; }

    /// <summary>Only return products in this category.</summary>
    public int? CategoryId { get; set; }

    /// <summary>1-based page number.</summary>
    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 ? 10 : (value > MaxPageSize ? MaxPageSize : value);
    }

    /// <summary>Sort by "price_asc", "price_desc" or "name" (default: newest first).</summary>
    public string? SortBy { get; set; }
}

/// <summary>Wrapped paged result returned by GET /api/products.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
