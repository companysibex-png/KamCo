using KamCo.Api.Common;
using KamCo.Api.Data;
using KamCo.Api.DTOs.Orders;
using KamCo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KamCo.Api.Services;

public interface IOrderService
{
    /// <summary>Creates an order for the given user. Prices/stock are computed server-side.</summary>
    Task<OrderDto> CreateAsync(int userId, CreateOrderDto dto);

    /// <summary>Admin only: every order in the system.</summary>
    Task<IReadOnlyList<OrderDto>> GetAllAsync();

    /// <summary>Orders belonging to one user (admin can pass any id).</summary>
    Task<IReadOnlyList<OrderDto>> GetForUserAsync(int userId);

    Task<OrderDto> GetByIdAsync(int orderId, int requesterId, bool isAdmin);

    /// <summary>Admin only: move an order through its lifecycle.</summary>
    Task<OrderDto> UpdateStatusAsync(int orderId, UpdateOrderStatusDto dto);
}

public class OrderService(AppDbContext db) : IOrderService
{
    public async Task<OrderDto> CreateAsync(int userId, CreateOrderDto dto)
    {
        // Collapse duplicate product lines (e.g. same ProductId sent twice).
        var requested = dto.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var productIds = requested.Keys.ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        if (products.Count != productIds.Count)
        {
            var missing = productIds.Except(products.Keys).OrderBy(x => x);
            throw new BadRequestException($"Product(s) not found: {string.Join(", ", missing)}");
        }

        var order = new Order { UserId = userId, Status = OrderStatus.Pending };

        foreach (var (productId, quantity) in requested)
        {
            var product = products[productId];

            if (!product.IsActive)
                throw new BadRequestException($"Product '{product.Name}' is not available.");
            if (product.StockQuantity < quantity)
                throw new BadRequestException(
                    $"Not enough stock for '{product.Name}'. Available: {product.StockQuantity}, requested: {quantity}.");

            product.StockQuantity -= quantity; // Reserve stock immediately.

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = quantity,
                UnitPrice = product.Price
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return await GetByIdAsync(order.Id, userId, isAdmin: true);
    }

    public async Task<IReadOnlyList<OrderDto>> GetAllAsync()
    {
        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<OrderDto>> GetForUserAsync(int userId)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(Map).ToList();
    }

    public async Task<OrderDto> GetByIdAsync(int orderId, int requesterId, bool isAdmin)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
            throw new NotFoundException($"Order {orderId} was not found.");

        if (!isAdmin && order.UserId != requesterId)
            throw new ForbiddenException("You can only view your own orders.");

        return Map(order);
    }

    public async Task<OrderDto> UpdateStatusAsync(int orderId, UpdateOrderStatusDto dto)
    {
        var status = dto.Status.Trim();
        if (!OrderStatus.All.Contains(status))
            throw new BadRequestException($"Invalid status '{status}'. Allowed: {string.Join(", ", OrderStatus.All)}.");

        var order = await db.Orders.FindAsync(orderId)
            ?? throw new NotFoundException($"Order {orderId} was not found.");

        // Business rule: cancelled orders must not keep moving forward.
        if (order.Status == OrderStatus.Cancelled && status != OrderStatus.Cancelled)
            throw new BadRequestException("A cancelled order cannot be reactivated.");

        order.Status = status;
        await db.SaveChangesAsync();

        return await GetByIdAsync(orderId, requesterId: 0, isAdmin: true);
    }

    private static OrderDto Map(Order o) => new()
    {
        Id = o.Id,
        Status = o.Status,
        TotalAmount = o.TotalAmount,
        CreatedAt = o.CreatedAt,
        UserId = o.UserId,
        CustomerName = o.User?.FullName,
        Items = o.Items.Select(i => new OrderItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? $"Product {i.ProductId}",
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList()
    };
}
