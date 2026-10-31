using KamCo.Api.Common;
using KamCo.Api.Data;
using KamCo.Api.DTOs.Products;
using Microsoft.EntityFrameworkCore;

namespace KamCo.Api.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAsync(ProductQueryParameters query);
    Task<ProductDto> GetByIdAsync(int id);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto> UpdateAsync(int id, UpdateProductDto dto);
    Task DeleteAsync(int id);
}

public class ProductService(AppDbContext db) : IProductService
{
    public async Task<PagedResult<ProductDto>> GetAsync(ProductQueryParameters query)
    {
        var products = db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .AsQueryable();

        // Free-text search on name (case-insensitive; SQLite LIKE is already case-insensitive for ASCII).
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(p => p.Name.Contains(term) || (p.Description != null && p.Description.Contains(term)));
        }

        if (query.CategoryId.HasValue)
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);

        var totalCount = await products.CountAsync();

        var page = Math.Max(query.Page, 1);

        // Project to DTO first (shared by both code paths below).
        IQueryable<ProductDto> projected = products
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                IsActive = p.IsActive,
                CategoryId = p.CategoryId,
                CategoryName = p.Category!.Name,
                CreatedAt = p.CreatedAt
            });

        IReadOnlyList<ProductDto> items;

        var sortBy = query.SortBy?.ToLowerInvariant();
        if (sortBy is "price_asc" or "price_desc")
        {
            // SQLite cannot ORDER BY a decimal column, so price sorting is applied
            // in memory over the filtered set. Fine for demo-scale catalogs.
            var all = await projected.ToListAsync();
            items = sortBy == "price_asc"
                ? all.OrderBy(p => p.Price).Skip((page - 1) * query.PageSize).Take(query.PageSize).ToList()
                : all.OrderByDescending(p => p.Price).Skip((page - 1) * query.PageSize).Take(query.PageSize).ToList();
        }
        else
        {
            projected = sortBy == "name"
                ? projected.OrderBy(p => p.Name)
                : projected.OrderByDescending(p => p.CreatedAt);

            items = await projected
                .Skip((page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();
        }

        return new PagedResult<ProductDto>
        {
            Items = items,
            Page = page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ProductDto> GetByIdAsync(int id)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                IsActive = p.IsActive,
                CategoryId = p.CategoryId,
                CategoryName = p.Category!.Name,
                CreatedAt = p.CreatedAt
            })
            .FirstOrDefaultAsync();

        return product ?? throw new NotFoundException($"Product {id} was not found.");
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        await EnsureCategoryExists(dto.CategoryId);

        var product = new Models.Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            ImageUrl = dto.ImageUrl,
            CategoryId = dto.CategoryId,
            IsActive = true
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return await GetByIdAsync(product.Id);
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductDto dto)
    {
        var product = await db.Products.FindAsync(id)
            ?? throw new NotFoundException($"Product {id} was not found.");

        await EnsureCategoryExists(dto.CategoryId);

        product.Name = dto.Name.Trim();
        product.Description = dto.Description?.Trim();
        product.Price = dto.Price;
        product.StockQuantity = dto.StockQuantity;
        product.ImageUrl = dto.ImageUrl;
        product.IsActive = dto.IsActive;
        product.CategoryId = dto.CategoryId;
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var product = await db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Product {id} was not found.");

        var usedInOrders = await db.OrderItems.AnyAsync(i => i.ProductId == id);
        if (usedInOrders)
        {
            // Keep order history intact — just unpublish the product.
            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return;
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync();
    }

    private async Task EnsureCategoryExists(int categoryId)
    {
        var exists = await db.Categories.AnyAsync(c => c.Id == categoryId);
        if (!exists)
            throw new BadRequestException($"Category {categoryId} does not exist.");
    }
}
