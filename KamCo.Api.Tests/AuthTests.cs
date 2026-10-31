using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KamCo.Api.Tests;

public class AuthTests(KamCoApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Register_returns_token_with_User_role()
    {
        var client = NewClient();
        var email = $"reg-{Guid.NewGuid():N}@test.kamco.com";

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "New Customer", email, password = "Secret@123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("User", body.GetProperty("role").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Register_duplicate_email_returns_409()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Dup", email = "sara@kamco.com", password = "Secret@123" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_short_password_returns_400()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Shorty", email = $"short-{Guid.NewGuid():N}@test.kamco.com", password = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_400()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "sara@kamco.com", password = "WrongPassword" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_seeded_admin_has_Admin_role()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@kamco.com", password = "Admin@123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Admin",
            (await JsonAsync(response)).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        var client = NewClient();

        var response = await client.GetAsync("/api/orders/my");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
