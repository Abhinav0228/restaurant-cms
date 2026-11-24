using RestaurantCms.Application.Abstractions;

namespace RestaurantCms.Infrastructure.Tenancy;

/// <summary>Scoped holder of the current restaurant. Set by the API's tenant middleware (from the staff token
/// or the public URL slug) or by the job runner. Can only be set once per scope.</summary>
public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    public Guid TenantId { get; private set; }
    public string? Slug { get; private set; }
    public bool IsResolved => TenantId != Guid.Empty;

    public void Set(Guid tenantId, string? slug)
    {
        if (IsResolved && TenantId != tenantId)
            throw new InvalidOperationException("The tenant for this request has already been set.");
        TenantId = tenantId;
        Slug = slug;
    }
}
