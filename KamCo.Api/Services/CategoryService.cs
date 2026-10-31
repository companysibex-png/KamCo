using KamCo.Api.Common;
using KamCo.Api.Data;
using KamCo.Api.DTOs.Categories;
using Microsoft.EntityFrameworkCore;

namespace KamCo.Api.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync();
    Task<CategoryDto> GetByIdAsync(int id);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto);
    Task<CategoryDto> UpdateAsync(int id, UpdateCategoryDto dto);
    Task DeleteAsync(int id);
}

public class CategoryService(AppDbContext db) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync()
    {
        var categories = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ProductCount = c.Products.Count
            })
            .ToListAsync();

        return categories;
    }

    public async Task<CategoryDto> GetByIdAsync(int id)
    {
        var category = await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ProductCount = c.Products.Count
            })
            .FirstOrDefaultAsync();

        return category ?? throw new NotFoundException($"Category {id} was not found.");
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        var name = dto.Name.Trim();
        var duplicate = await db.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower());
        if (duplicate)
            throw new ConflictException($"Category '{name}' already exists.");

        var category = new Models.Category { Name = name, Description = dto.Description?.Trim() };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        return new CategoryDto { Id = category.Id, Name = category.Name, Description = category.Description, ProductCount = 0 };
    }

    public async Task<CategoryDto> UpdateAsync(int id, UpdateCategoryDto dto)
    {
        var category = await db.Categories.FindAsync(id)
            ?? throw new NotFoundException($"Category {id} was not found.");

        var name = dto.Name.Trim();
        var duplicate = await db.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower());
        if (duplicate)
            throw new ConflictException($"Category '{name}' already exists.");

        category.Name = name;
        category.Description = dto.Description?.Trim();
        await db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var category = await db.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Category {id} was not found.");

        if (category.Products.Count > 0)
            throw new BadRequestException(
                $"Category '{category.Name}' still has {category.Products.Count} product(s). Move or delete them first.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync();
    }
}
