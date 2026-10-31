using KamCo.Api.Models;
using KamCo.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace KamCo.Api.Data;

/// <summary>
/// Applies migrations and seeds demo data on startup (idempotent — safe on every boot).
/// Default admin: admin@kamco.com / Admin@123
/// Default customer: sara@kamco.com / User@123
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher)
    {
        await db.Database.EnsureCreatedAsync();

        if (!await db.Users.AnyAsync())
        {
            var (adminHash, adminSalt) = hasher.Hash("Admin@123");
            var (userHash, userSalt) = hasher.Hash("User@123");

            db.Users.AddRange(
                new User
                {
                    FullName = "KamCo Admin",
                    Email = "admin@kamco.com",
                    PasswordHash = adminHash,
                    PasswordSalt = adminSalt,
                    Role = "Admin",
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    FullName = "Sara Customer",
                    Email = "sara@kamco.com",
                    PasswordHash = userHash,
                    PasswordSalt = userSalt,
                    Role = "User",
                    CreatedAt = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "Electronics", Description = "Gadgets, accessories and devices" },
                new Category { Name = "Books", Description = "Printed and digital books" },
                new Category { Name = "Home & Kitchen", Description = "Everything for a smarter home" });
            await db.SaveChangesAsync();
        }

        if (!await db.Products.AnyAsync())
        {
            var electronics = await db.Categories.FirstAsync(c => c.Name == "Electronics");
            var books = await db.Categories.FirstAsync(c => c.Name == "Books");
            var home = await db.Categories.FirstAsync(c => c.Name == "Home & Kitchen");

            db.Products.AddRange(
                new Product
                {
                    Name = "Wireless Mechanical Keyboard",
                    Description = "87-key mechanical keyboard with hot-swappable switches, 2.4GHz + Bluetooth + USB-C.",
                    Price = 89.99m, StockQuantity = 25, CategoryId = electronics.Id,
                    ImageUrl = "https://example.com/images/keyboard.jpg"
                },
                new Product
                {
                    Name = "USB-C Fast Charger 65W",
                    Description = "GaN fast charger with dual USB-C ports. Charges a laptop and a phone at once.",
                    Price = 34.50m, StockQuantity = 120, CategoryId = electronics.Id,
                    ImageUrl = "https://example.com/images/charger.jpg"
                },
                new Product
                {
                    Name = "Noise Cancelling Headphones",
                    Description = "Over-ear Bluetooth headphones with 40h battery life and active noise cancelling.",
                    Price = 149.00m, StockQuantity = 40, CategoryId = electronics.Id,
                    ImageUrl = "https://example.com/images/headphones.jpg"
                },
                new Product
                {
                    Name = "Clean Code - Robert C. Martin",
                    Description = "A handbook of agile software craftsmanship. Classic must-read for developers.",
                    Price = 32.99m, StockQuantity = 60, CategoryId = books.Id,
                    ImageUrl = "https://example.com/images/cleancode.jpg"
                },
                new Product
                {
                    Name = "Smart LED Desk Lamp",
                    Description = "App-controlled LED lamp, 5 color temperatures, USB charging port on the base.",
                    Price = 27.90m, StockQuantity = 80, CategoryId = home.Id,
                    ImageUrl = "https://example.com/images/lamp.jpg"
                },
                new Product
                {
                    Name = "Stainless Steel Water Bottle 1L",
                    Description = "Double-wall vacuum insulated bottle. Keeps drinks cold 24h / hot 12h.",
                    Price = 19.95m, StockQuantity = 200, CategoryId = home.Id,
                    ImageUrl = "https://example.com/images/bottle.jpg"
                });
            await db.SaveChangesAsync();
        }
    }
}
