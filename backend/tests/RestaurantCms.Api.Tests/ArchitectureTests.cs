using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantCms.Application;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain.Common;
using RestaurantCms.Infrastructure;
using RestaurantCms.Infrastructure.Persistence;

namespace RestaurantCms.Api.Tests;

/// <summary>Guards the layering and multi-tenancy rules so they can't erode as the product grows.</summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Entity).Assembly;
    private static readonly Assembly Application = typeof(ICommand<>).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;

    private static IEnumerable<string> References(Assembly a) => a.GetReferencedAssemblies().Select(r => r.Name!);

    [Fact]
    public void Domain_depends_on_nothing_but_the_base_library() =>
        Assert.All(References(Domain), name => Assert.True(name.StartsWith("System") || name == "netstandard", $"Domain references {name}"));

    [Fact]
    public void Application_does_not_know_about_the_database_or_the_web()
    {
        var forbidden = new[] { "RestaurantCms.Infrastructure", "RestaurantCms.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "Hangfire" };
        Assert.All(References(Application), name => Assert.DoesNotContain(forbidden, f => name.StartsWith(f)));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_api() =>
        Assert.DoesNotContain("RestaurantCms.Api", References(Infrastructure));

    [Fact]
    public void Every_command_and_query_has_exactly_one_handler()
    {
        var messages = Application.GetTypes().Where(t => !t.IsAbstract && t.GetInterfaces().Any(i =>
            i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICommand<>) || i.GetGenericTypeDefinition() == typeof(IQuery<>))));
        var handled = Application.GetTypes().Where(t => t is { IsAbstract: false, ContainsGenericParameters: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) || i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)))
            .Select(i => i.GetGenericArguments()[0]).ToList();

        Assert.All(messages, m => Assert.True(handled.Count(h => h == m) == 1, $"{m.DeclaringType?.Name}.{m.Name} needs exactly one handler"));
    }

    [Fact]
    public void Use_cases_are_not_public_handlers()
    {
        // Endpoints depend on ICommandHandler<,>, never on concrete handlers, so the pipeline (validation, logging) can't be bypassed.
        var publicHandlers = Application.GetTypes().Where(t => t.IsPublic && t.Name == "Handler");
        Assert.Empty(publicHandlers);
    }

    [Fact]
    public void Every_tenant_owned_table_is_filtered_by_tenant()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=model_only",
            ["Jwt:Key"] = new string('k', 40),
        }).Build();
        var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(config, runHangfireServer: false);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantOwned = db.Model.GetEntityTypes().Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType) && !e.IsOwned()).ToList();
        Assert.NotEmpty(tenantOwned);

        var set = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!;
        foreach (var entity in tenantOwned)
        {
            var query = (IQueryable)set.MakeGenericMethod(entity.ClrType).Invoke(db, null)!;
            var sql = query.ToQueryString(); // translated without touching the database
            Assert.True(sql.Contains("\"TenantId\" =", StringComparison.Ordinal), $"{entity.ClrType.Name} is missing its tenant filter:\n{sql}");
        }
    }
}
