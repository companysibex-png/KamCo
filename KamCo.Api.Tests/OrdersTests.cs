using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace KamCo.Api.Tests;

public class OrdersTests(KamCoApiFactory factory) : ApiTestBase(factory)
{
    private static object OrderBody(int productId, int quantity = 1)
        => new { items = new[] { new { productId, quantity } } };

    [Fact]
    public async Task Place_order_computes_total_server_side_and_reserves_stock()
    {
        var admin = await AdminAsync();
        var (user, _) = await NewUserAsync("orderer");
        var productId = await CreateProductAsync(admin, price: 5m, stock: 10);

        var create = await user.PostAsJsonAsync("/api/orders", OrderBody(productId, 3));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var order = await JsonAsync(create);
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        Assert.Equal(15m, order.GetProperty("totalAmount").GetDecimal());

        var product = await JsonAsync(await admin.GetAsync($"/api/products/{productId}"));
        Assert.Equal(7, product.GetProperty("stockQuantity").GetInt32());
    }

    [Fact]
    public async Task Duplicate_product_lines_are_collapsed()
    {
        var admin = await AdminAsync();
        var (user, _) = await NewUserAsync("dup");
        var productId = await CreateProductAsync(admin, price: 2m, stock: 100);

        var create = await user.PostAsJsonAsync("/api/orders", new
        {
            items = new[]
            {
                new { productId, quantity = 1 },
                new { productId, quantity = 2 }
            }
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var order = await JsonAsync(create);
        Assert.Equal(1, order.GetProperty("items").GetArrayLength());
        Assert.Equal(6m, order.GetProperty("totalAmount").GetDecimal()); // 3 × 2
    }

    [Fact]
    public async Task Order_with_insufficient_stock_returns_400()
    {
        var admin = await AdminAsync();
        var (user, _) = await NewUserAsync("stock");
        var productId = await CreateProductAsync(admin, price: 1m, stock: 2);

        var create = await user.PostAsJsonAsync("/api/orders", OrderBody(productId, 5));

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Order_with_unknown_product_returns_400()
    {
        var (user, _) = await NewUserAsync("ghost");

        var create = await user.PostAsJsonAsync("/api/orders", OrderBody(999999));

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Users_see_only_their_own_orders()
    {
        var admin = await AdminAsync();
        var (userA, idA) = await NewUserAsync("owner");
        var (userB, _) = await NewUserAsync("intruder");
        var productId = await CreateProductAsync(admin);

        var create = await userA.PostAsJsonAsync("/api/orders", OrderBody(productId));
        var orderId = (await JsonAsync(create)).GetProperty("id").GetInt32();

        // B cannot open A's order → 403
        var forbidden = await userB.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // B's own list is empty
        var mine = await JsonAsync(await userB.GetAsync("/api/orders/my"));
        Assert.Equal(0, mine.GetArrayLength());

        // A sees it in "my"
        var mineA = await JsonAsync(await userA.GetAsync("/api/orders/my"));
        Assert.Equal(1, mineA.GetArrayLength());
        Assert.Equal(idA, mineA[0].GetProperty("userId").GetInt32());

        // Admin sees everything
        var all = await JsonAsync(await admin.GetAsync("/api/orders"));
        Assert.Contains(all.EnumerateArray(),
            o => o.GetProperty("id").GetInt32() == orderId);
    }

    [Fact]
    public async Task Regular_user_cannot_list_all_orders()
    {
        var user = await UserAsync();
        var response = await user.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_moves_order_through_lifecycle()
    {
        var admin = await AdminAsync();
        var (user, _) = await NewUserAsync("lifecycle");
        var productId = await CreateProductAsync(admin);

        var orderId = (await JsonAsync(
            await user.PostAsJsonAsync("/api/orders", OrderBody(productId))))
            .GetProperty("id").GetInt32();

        var patch = new { status = "Paid" };
        var paid = await admin.PatchAsJsonAsync($"/api/orders/{orderId}/status", patch);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal("Paid", (await JsonAsync(paid)).GetProperty("status").GetString());

        var shipped = await admin.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new { status = "Shipped" });
        Assert.Equal("Shipped", (await JsonAsync(shipped)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancelled_order_cannot_be_reactivated()
    {
        var admin = await AdminAsync();
        var (user, _) = await NewUserAsync("cancel");
        var productId = await CreateProductAsync(admin);

        var orderId = (await JsonAsync(
            await user.PostAsJsonAsync("/api/orders", OrderBody(productId))))
            .GetProperty("id").GetInt32();

        await admin.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new { status = "Cancelled" });

        var revive = await admin.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new { status = "Paid" });
        Assert.Equal(HttpStatusCode.BadRequest, revive.StatusCode);
    }

    [Fact]
    public async Task Invalid_status_value_returns_400()
    {
        var admin = await AdminAsync();
        var (user, _) = await NewUserAsync("badstatus");
        var productId = await CreateProductAsync(admin);

        var orderId = (await JsonAsync(
            await user.PostAsJsonAsync("/api/orders", OrderBody(productId))))
            .GetProperty("id").GetInt32();

        var response = await admin.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new { status = "Teleported" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Regular_user_cannot_change_order_status()
    {
        var admin = await AdminAsync();
        var (user, _) = await NewUserAsync("noadmin");
        var productId = await CreateProductAsync(admin);

        var orderId = (await JsonAsync(
            await user.PostAsJsonAsync("/api/orders", OrderBody(productId))))
            .GetProperty("id").GetInt32();

        var response = await user.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new { status = "Paid" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
