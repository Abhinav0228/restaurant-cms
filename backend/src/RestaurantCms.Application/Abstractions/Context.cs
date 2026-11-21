using RestaurantCms.Domain;

namespace RestaurantCms.Application.Abstractions;

/// <summary>The restaurant the current request or job is working for.
/// Resolved from the staff token, or from the slug in a public URL.</summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    string? Slug { get; }
    bool IsResolved { get; }
}

/// <summary>Allows background jobs and onboarding to act as a specific tenant.</summary>
public interface ITenantContextSetter
{
    void Set(Guid tenantId, string? slug);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Name { get; }
    StaffRole? Role { get; }
    bool IsAuthenticated { get; }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
