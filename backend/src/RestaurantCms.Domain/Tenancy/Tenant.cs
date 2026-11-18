using System.Text.RegularExpressions;
using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Tenancy;

public enum TenantPlan
{
    Starter,
    Growth,
    Enterprise
}

public enum TenantStatus
{
    Active,
    Suspended
}

/// <summary>A restaurant that subscribes to the platform. Everything else in the system belongs to one tenant.</summary>
public sealed partial class Tenant : Entity
{
    private Tenant() { }

    /// <summary>URL-safe identifier used in public links, e.g. /r/ember-oak.</summary>
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public TenantPlan Plan { get; private set; } = TenantPlan.Starter;
    public TenantStatus Status { get; private set; } = TenantStatus.Active;

    public bool IsActive => Status == TenantStatus.Active;
    public PlanLimits Limits => PlanLimits.For(Plan);

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$")]
    private static partial Regex SlugPattern();

    /// <summary>Slugs that would clash with app routes.</summary>
    private static readonly HashSet<string> Reserved = ["admin", "api", "login", "platform", "hubs", "hangfire", "health", "www", "app"];

    public static Result<Tenant> Create(string slug, string name, TenantPlan plan, DateTimeOffset now)
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();
        if (!SlugPattern().IsMatch(normalized))
            return Error.Validation("Tenant.Slug", "Use 3–40 lowercase letters, numbers or dashes (not at the start or end).");
        if (Reserved.Contains(normalized))
            return Error.Validation("Tenant.SlugReserved", $"'{normalized}' is reserved. Choose another address.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return Error.Validation("Tenant.Name", "Restaurant name is required (up to 100 characters).");

        return new Tenant { Slug = normalized, Name = name.Trim(), Plan = plan, CreatedAt = now };
    }

    public void Rename(string name)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
    }

    public void ChangePlan(TenantPlan plan, DateTimeOffset now)
    {
        Plan = plan;
        Touch(now);
    }

    public void Suspend(DateTimeOffset now)
    {
        Status = TenantStatus.Suspended;
        Touch(now);
    }

    public void Activate(DateTimeOffset now)
    {
        Status = TenantStatus.Active;
        Touch(now);
    }
}

/// <summary>What each subscription plan allows. Enforced by the application layer.</summary>
public sealed record PlanLimits(int MaxTables, int MaxStaff, bool QrOrdering, bool Reservations)
{
    public static PlanLimits For(TenantPlan plan) => plan switch
    {
        TenantPlan.Starter => new PlanLimits(MaxTables: 15, MaxStaff: 5, QrOrdering: true, Reservations: true),
        TenantPlan.Growth => new PlanLimits(MaxTables: 60, MaxStaff: 25, QrOrdering: true, Reservations: true),
        _ => new PlanLimits(MaxTables: int.MaxValue, MaxStaff: int.MaxValue, QrOrdering: true, Reservations: true)
    };
}
