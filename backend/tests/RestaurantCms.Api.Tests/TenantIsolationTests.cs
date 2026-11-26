using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantCms.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RestaurantCms.Api.Tests;

/// <summary>Boots the real API against a throwaway PostgreSQL container, with the demo platform data seeded.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Integration!123";
    private PostgreSqlContainer? _db;
    public string? SkipReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _db = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _db.StartAsync();
        }
        catch (Exception ex)
        {
            SkipReason = "Docker is not available, so the database integration tests were skipped: " + ex.Message;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _db?.GetConnectionString());
        builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789-abcdef");
        builder.UseSetting("Seed:Demo", "true");
        builder.UseSetting("Seed:Password", Password);
        builder.UseSetting("Hangfire:RunServer", "false");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_db is not null) await _db.DisposeAsync();
    }
}

public class TenantIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private async Task<HttpClient> SignInAs(string email)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiFactory.Password }, Json);
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> NewRestaurant(string slug)
    {
        var platform = await SignInAs("platform@restaurant.local");
        var created = await platform.PostAsJsonAsync("/api/v1/platform/tenants", new
        {
            slug, name = $"Cafe {slug}", plan = "Growth", ownerEmail = $"owner@{slug}.test", ownerName = "Owner",
            ownerPassword = ApiFactory.Password, withSampleData = false,
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return await SignInAs($"owner@{slug}.test");
    }

    [Fact]
    public async Task A_restaurant_never_sees_another_restaurants_tables()
    {
        Assert.SkipWhen(factory.SkipReason is not null, factory.SkipReason ?? "");
        var a = await NewRestaurant("iso-a");
        var b = await NewRestaurant("iso-b");

        var made = await b.PostAsJsonAsync("/api/v1/tables", new { code = "B9", label = "B's table", seats = 2, area = "Indoor", gridX = 0, gridY = 0 }, Json, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        var tableId = (await made.Content.ReadFromJsonAsync<JsonElement>(Json, TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();

        var aFloor = await a.GetStringAsync("/api/v1/tables", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("B9", aFloor);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/v1/tables/{tableId}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Guest_links_are_scoped_to_the_restaurant_in_the_url()
    {
        Assert.SkipWhen(factory.SkipReason is not null, factory.SkipReason ?? "");
        await NewRestaurant("iso-c");
        var guest = factory.CreateClient();

        // The demo restaurant has table T1; the new, empty restaurant must not expose it.
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/v1/public/ember-oak/tables/T1", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/v1/public/iso-c/tables/T1", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/v1/public/no-such-cafe/menu", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Orders_are_numbered_per_restaurant_from_1001()
    {
        Assert.SkipWhen(factory.SkipReason is not null, factory.SkipReason ?? "");
        var ct = TestContext.Current.CancellationToken;
        var owner = await NewRestaurant("iso-num");

        var category = await owner.PostAsJsonAsync("/api/v1/menu/categories", new { name = "All day" }, Json, ct);
        Assert.Equal(HttpStatusCode.Created, category.StatusCode);
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>(Json, ct)).GetProperty("id").GetGuid();
        var item = await owner.PostAsJsonAsync("/api/v1/menu/items", new { categoryId, name = "Chai", price = 40, prepMinutes = 3 }, Json, ct);
        Assert.Equal(HttpStatusCode.Created, item.StatusCode);
        var menuItemId = (await item.Content.ReadFromJsonAsync<JsonElement>(Json, ct)).GetProperty("id").GetGuid();

        var numbers = new List<int>();
        for (var i = 0; i < 2; i++)
        {
            var placed = await owner.PostAsJsonAsync("/api/v1/orders",
                new { type = "Takeaway", customerName = "Test", lines = new[] { new { menuItemId, quantity = 1 } } }, Json, ct);
            Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
            numbers.Add((await placed.Content.ReadFromJsonAsync<JsonElement>(Json, ct)).GetProperty("number").GetInt32());
        }
        Assert.Equal([1001, 1002], numbers);
    }

    [Fact]
    public async Task Suspending_a_restaurant_takes_effect_immediately()
    {
        Assert.SkipWhen(factory.SkipReason is not null, factory.SkipReason ?? "");
        var ct = TestContext.Current.CancellationToken;
        var owner = await NewRestaurant("iso-sus");
        var guest = factory.CreateClient();

        // Warm the tenant lookup and public menu caches first.
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/v1/public/iso-sus/menu", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/v1/tables", ct)).StatusCode);

        var platform = await SignInAs("platform@restaurant.local");
        var tenants = await platform.GetFromJsonAsync<JsonElement>("/api/v1/platform/tenants", Json, ct);
        var id = tenants.EnumerateArray().Single(t => t.GetProperty("slug").GetString() == "iso-sus").GetProperty("id").GetGuid();
        var suspended = await platform.PutAsJsonAsync($"/api/v1/platform/tenants/{id}/subscription", new { plan = "Growth", status = "Suspended" }, Json, ct);
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/v1/public/iso-sus/menu", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/v1/tables", ct)).StatusCode);
    }

    [Fact]
    public void Every_model_change_has_a_migration()
    {
        Assert.SkipWhen(factory.SkipReason is not null, factory.SkipReason ?? "");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Fails when an entity or configuration changed without `dotnet ef migrations add <Name>`.
        Assert.False(db.Database.HasPendingModelChanges(),
            "The EF Core model has changes that are not in a migration. Add one with: dotnet ef migrations add <Name> " +
            "-p src/RestaurantCms.Infrastructure -s src/RestaurantCms.Api -o Persistence/Migrations");
        Assert.Empty(db.Database.GetPendingMigrations());
    }

    [Fact]
    public async Task Staff_api_requires_sign_in()
    {
        Assert.SkipWhen(factory.SkipReason is not null, factory.SkipReason ?? "");
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/tables", TestContext.Current.CancellationToken)).StatusCode);
    }
}
