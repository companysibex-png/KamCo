using KamCo.Api.Models;
using KamCo.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace KamCo.Api.Data;

/// <summary>
/// Applies migrations and seeds demo data on startup (idempotent — safe on every boot).
/// Default admin: admin@kamco.com / Admin@123
/// Default customer: sara@kamco.com / User@123
/// If the orders table is empty, ~140 demo orders spread across the last ~120
/// days are generated, so the sales report (scripts/sales-report.py) has
/// meaningful data. Set KAMCO_NO_DEMO_ORDERS=1 to skip that.
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

        // ---------- Demo sales history ----------
        if (!await db.Orders.AnyAsync()
            && Environment.GetEnvironmentVariable("KAMCO_NO_DEMO_ORDERS") != "1")
        {
            await SeedSalesHistoryAsync(db);
        }
    }

    /// <summary>
    /// Generates a deterministic block of demo orders (~140, fixed RNG seed)
    /// across the last 120 days with a realistic status lifecycle:
    /// older orders are mostly shipped, recent ones still open, ~10% cancelled.
    /// Stock is reserved like real orders would, but never below
    /// <see cref="MinDemoStock"/> so the demo store stays usable.
    /// </summary>
    private const int MinDemoStock = 15;

    private static async Task SeedSalesHistoryAsync(AppDbContext db)
    {
        var products = await db.Products.Where(p => p.IsActive).ToListAsync();
        var userIds = await db.Users.Select(u => u.Id).ToListAsync();
        if (products.Count == 0 || userIds.Count == 0)
            return;

        var rng = new Random(20260101); // fixed seed => same demo data every time
        var now = DateTime.UtcNow;
        var remainingStock = products.ToDictionary(p => p.Id, p => p.StockQuantity);
        var orders = new List<Order>(140);

        for (var i = 0; i < 140; i++)
        {
            var daysAgo = rng.Next(0, 121);
            var createdAt = now
                .AddDays(-daysAgo)
                .AddHours(-rng.Next(0, 24))
                .AddMinutes(-rng.Next(0, 60));

            // Lifecycle realism: the older the order, the more likely it shipped.
            var status = daysAgo <= 3
                ? PickStatus(rng, (OrderStatus.Pending, 60), (OrderStatus.Paid, 30), (OrderStatus.Cancelled, 10))
                : daysAgo <= 14
                    ? PickStatus(rng, (OrderStatus.Pending, 10), (OrderStatus.Paid, 30), (OrderStatus.Shipped, 50), (OrderStatus.Cancelled, 10))
                    : PickStatus(rng, (OrderStatus.Shipped, 82), (OrderStatus.Cancelled, 18));

            var order = new Order { UserId = userIds[rng.Next(userIds.Count)], Status = status, CreatedAt = createdAt };

            // 1-3 distinct products per order, quantity 1-3 each.
            var lineCount = rng.Next(1, 4);
            foreach (var product in products.OrderBy(_ => rng.Next()).Take(lineCount))
            {
                var quantity = rng.Next(1, 4);
                if (remainingStock[product.Id] - quantity < MinDemoStock)
                    continue; // keep the demo catalog stocked — skip the line

                remainingStock[product.Id] -= quantity;
                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });
            }

            if (order.Items.Count == 0)
                continue;

            order.TotalAmount = order.Items.Sum(it => it.UnitPrice * it.Quantity);
            orders.Add(order);
        }

        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();

        // Keep the catalog consistent with the simulated reservations.
        foreach (var product in products)
            product.StockQuantity = remainingStock[product.Id];
        await db.SaveChangesAsync();
    }

    private static string PickStatus(Random rng, params (string Status, int Weight)[] options)
    {
        var roll = rng.Next(options.Sum(o => o.Weight));
        foreach (var (status, weight) in options)
        {
            if (roll < weight)
                return status;
            roll -= weight;
        }
        return options[^1].Status;
    }
}
