using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace KamCo.Api.Tests;

public class ProductsTests(KamCoApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Anonymous_list_returns_seeded_catalog()
    {
        var body = await JsonAsync(await NewClient().GetAsync("/api/products"));

        Assert.True(body.GetProperty("totalCount").GetInt32() >= 6);
        Assert.True(body.GetProperty("items").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Search_by_name_finds_keyboard()
    {
        var body = await JsonAsync(
            await NewClient().GetAsync("/api/products?search=keyboard"));

        var items = body.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.Contains(items.EnumerateArray(),
            i => i.GetProperty("name").GetString()!.Contains("Keyboard"));
    }

    [Fact]
    public async Task Sort_by_price_ascending()
    {
        var body = await JsonAsync(
            await NewClient().GetAsync("/api/products?sortBy=price_asc&pageSize=50"));

        var prices = body.GetProperty("items")
            .EnumerateArray().Select(i => i.GetProperty("price").GetDecimal()).ToList();
        Assert.Equal(prices.OrderBy(p => p), prices);
    }

    [Fact]
    public async Task Pagination_returns_requested_page_size()
    {
        var body = await JsonAsync(
            await NewClient().GetAsync("/api/products?page=1&pageSize=2"));

        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 6);
        Assert.True(body.GetProperty("totalPages").GetInt32() >= 3);
    }

    [Fact]
    public async Task Get_missing_product_returns_404()
    {
        var response = await NewClient().GetAsync("/api/products/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_product_as_regular_user_is_forbidden()
    {
        var user = await UserAsync();

        var response = await user.PostAsJsonAsync("/api/products",
            new { name = "Nope", price = 1m, stockQuantity = 1, categoryId = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_update_and_delete_product_as_admin()
    {
        var admin = await AdminAsync();

        // CREATE
        var create = await admin.PostAsJsonAsync("/api/products",
            new { name = "Integration Test Gadget", price = 12.5m, stockQuantity = 7, categoryId = 1 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await JsonAsync(create);
        var id = created.GetProperty("id").GetInt32();
        Assert.Equal("Integration Test Gadget", created.GetProperty("name").GetString());

        // UPDATE
        var update = await admin.PutAsJsonAsync($"/api/products/{id}",
            new { name = "Integration Test Gadget v2", price = 15m, stockQuantity = 9, isActive = true, categoryId = 1 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await JsonAsync(update);
        Assert.Equal(15m, updated.GetProperty("price").GetDecimal());

        // DELETE
        var delete = await admin.DeleteAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var gone = await NewClient().GetAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Create_product_with_zero_price_returns_400()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/products",
            new { name = "Free?", price = 0m, stockQuantity = 1, categoryId = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
