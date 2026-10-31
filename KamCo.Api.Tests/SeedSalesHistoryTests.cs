using System.Net.Http.Json;
using Xunit;

namespace KamCo.Api.Tests;

/// <summary>
/// Verifies the demo sales history that the seeder generates when the
/// orders table is empty — the data source for scripts/sales-report.py.
/// </summary>
public class SeedSalesHistoryTests(KamCoApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Demo_orders_exist_in_reasonable_volume()
    {
        var admin = await AdminAsync();

        var orders = await JsonAsync(await admin.GetAsync("/api/orders"));

        Assert.True(orders.GetArrayLength() >= 100,
            $"expected >= 100 demo orders, got {orders.GetArrayLength()}");
    }

    [Fact]
    public async Task Demo_orders_use_all_lifecycle_statuses()
    {
        var admin = await AdminAsync();

        var orders = await JsonAsync(await admin.GetAsync("/api/orders"));

        var statuses = orders.EnumerateArray()
            .Select(o => o.GetProperty("status").GetString()).ToHashSet();
        Assert.Superset(
            new HashSet<string> { "Pending", "Paid", "Shipped", "Cancelled" },
            statuses);
    }

    [Fact]
    public async Task Demo_orders_span_multiple_months()
    {
        var admin = await AdminAsync();

        var orders = await JsonAsync(await admin.GetAsync("/api/orders"));

        var months = orders.EnumerateArray()
            .Select(o => o.GetProperty("createdAt").GetString()![..7])
            .ToHashSet();
        Assert.True(months.Count >= 2,
            $"expected orders across >= 2 months, got {months.Count}");
    }

    [Fact]
    public async Task Every_demo_order_has_items_and_positive_total()
    {
        var admin = await AdminAsync();

        var orders = await JsonAsync(await admin.GetAsync("/api/orders"));

        foreach (var order in orders.EnumerateArray())
        {
            Assert.True(order.GetProperty("items").GetArrayLength() >= 1);
            Assert.True(order.GetProperty("totalAmount").GetDecimal() > 0);
        }
    }
}
