using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace KamCo.Api.Tests;

public class CategoriesTests(KamCoApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Anonymous_list_returns_seeded_categories()
    {
        var body = await JsonAsync(await NewClient().GetAsync("/api/categories"));

        var names = body.EnumerateArray()
            .Select(c => c.GetProperty("name").GetString()).ToList();
        Assert.Contains("Electronics", names);
        Assert.Contains("Books", names);
        Assert.Contains("Home & Kitchen", names);
    }

    [Fact]
    public async Task Get_missing_category_returns_404()
    {
        var response = await NewClient().GetAsync("/api/categories/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_category_as_regular_user_is_forbidden()
    {
        var user = await UserAsync();

        var response = await user.PostAsJsonAsync("/api/categories",
            new { name = "User Category", description = "should fail" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_update_and_delete_category_as_admin()
    {
        var admin = await AdminAsync();

        var create = await admin.PostAsJsonAsync("/api/categories",
            new { name = $"Test Cat {Guid.NewGuid():N}", description = "by tests" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await JsonAsync(create)).GetProperty("id").GetInt32();

        var update = await admin.PutAsJsonAsync($"/api/categories/{id}",
            new { name = "Renamed Test Cat", description = "updated" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var delete = await admin.DeleteAsync($"/api/categories/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }
}
