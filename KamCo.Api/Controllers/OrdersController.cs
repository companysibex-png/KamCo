using KamCo.Api.DTOs.Orders;
using KamCo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KamCo.Api.Controllers;

/// <summary>
/// Customer orders. Any authenticated user can place and view their own orders;
/// Admin can list all orders and change their status.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Invalid token."));

    /// <summary>Place a new order. Prices and stock are validated server-side.</summary>
    /// <remarks>
    /// Sample body:
    ///
    ///     { "items": [ { "productId": 1, "quantity": 2 }, { "productId": 4, "quantity": 1 } ] }
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto dto)
    {
        var order = await orderService.CreateAsync(CurrentUserId, dto);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    /// <summary>List all orders. Admin only.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetAll()
        => Ok(await orderService.GetAllAsync());

    /// <summary>List the current user's own orders.</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetMine()
        => Ok(await orderService.GetForUserAsync(CurrentUserId));

    /// <summary>Get one order. Users see only their own; Admin sees everything.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(int id)
    {
        var isAdmin = User.IsInRole("Admin");
        return Ok(await orderService.GetByIdAsync(id, CurrentUserId, isAdmin));
    }

    /// <summary>Change an order's status. Admin only.</summary>
    /// <param name="id">Order id.</param>
    /// <param name="dto">One of: Pending, Paid, Shipped, Cancelled.</param>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, UpdateOrderStatusDto dto)
        => Ok(await orderService.UpdateStatusAsync(id, dto));
}
