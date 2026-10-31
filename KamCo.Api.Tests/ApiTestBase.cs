using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KamCo.Api.Tests;

/// <summary>Shared helpers: HTTP client, auth logins, JSON assertions.</summary>
public abstract class ApiTestBase : IClassFixture<KamCoApiFactory>
{
    protected ApiTestBase(KamCoApiFactory factory) => Factory = factory;

    protected KamCoApiFactory Factory { get; }

    protected HttpClient NewClient() => Factory.CreateClient();

    /// <summary>Logs in with the seeded accounts and returns an authorized client.</summary>
    protected async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected Task<HttpClient> AdminAsync() => LoginAsync("admin@kamco.com", "Admin@123");
    protected Task<HttpClient> UserAsync() => LoginAsync("sara@kamco.com", "User@123");

    /// <summary>Registers a fresh user (unique email) and returns its client + id.</summary>
    protected async Task<(HttpClient Client, int UserId)> NewUserAsync(string prefix = "user")
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@test.kamco.com";
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Test Customer", email, password = "Secret@123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", body.GetProperty("token").GetString());
        return (client, body.GetProperty("id").GetInt32());
    }

    protected static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Creates a fresh product as admin and returns its id.</summary>
    protected async Task<int> CreateProductAsync(HttpClient adminClient,
        string name = "Test Product", decimal price = 5m, int stock = 10, int categoryId = 1)
    {
        var response = await adminClient.PostAsJsonAsync("/api/products",
            new { name, description = "created by tests", price, stockQuantity = stock, categoryId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetInt32();
    }
}
