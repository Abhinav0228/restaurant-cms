using RestaurantCms.Domain.Common;

namespace RestaurantCms.Domain.Staff;

public sealed class StaffUser : Entity
{
    private StaffUser() { }

    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public StaffRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>The restaurant this person works for. Null only for platform administrators.</summary>
    public Guid? TenantId { get; private set; }

    public bool IsPlatformAdmin => Role == StaffRole.PlatformAdmin;

    public static StaffUser Create(Guid? tenantId, string email, string fullName, StaffRole role, string passwordHash) => new()
    {
        TenantId = role == StaffRole.PlatformAdmin ? null : tenantId ?? throw new ArgumentException("Restaurant staff need a tenant.", nameof(tenantId)),
        Email = email.Trim().ToLowerInvariant(),
        FullName = fullName.Trim(),
        Role = role,
        PasswordHash = passwordHash
    };

    public void SetPasswordHash(string hash) => PasswordHash = hash;
    public void SetActive(bool active) => IsActive = active;
}
