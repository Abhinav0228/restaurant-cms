using System.Net.Mail;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Settings;
using RestaurantCms.Domain.Staff;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Application.Features.Platform;

public sealed record TenantDto(Guid Id, string Slug, string Name, TenantPlan Plan, TenantStatus Status, DateTimeOffset CreatedAt);

public static class TenantMapping
{
    public static TenantDto ToDto(this Tenant t) => new(t.Id, t.Slug, t.Name, t.Plan, t.Status, t.CreatedAt);
}

/// <summary>Seeds starter content for a new restaurant (implemented by Infrastructure).</summary>
public interface ITenantStarterContent
{
    Task AddAsync(Guid tenantId, CancellationToken ct);
}

/// <summary>Onboards a new restaurant: tenant, default settings and its owner account (optionally a sample menu and tables).</summary>
public static class CreateTenant
{
    public sealed record Command(string Slug, string Name, TenantPlan Plan, string OwnerEmail, string OwnerName,
        string OwnerPassword, string TimeZoneId = "Asia/Kolkata", string Currency = "INR", bool WithSampleData = false)
        : ICommand<TenantDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator()
        {
            Required("slug", x => x.Slug);
            Required("name", x => x.Name);
            MaxLength("name", x => x.Name, 100);
            Rule("ownerEmail", x => !string.IsNullOrWhiteSpace(x.OwnerEmail) && MailAddress.TryCreate(x.OwnerEmail.Trim(), out _), "Enter a valid email.");
            Required("ownerName", x => x.OwnerName);
            Rule("ownerPassword", x => (x.OwnerPassword ?? "").Length >= 8, "Password must be at least 8 characters.");
            Rule("currency", x => (x.Currency ?? "").Trim().Length == 3, "Use a 3-letter currency code.");
        }
    }

    internal sealed class Handler(ITenantRepository tenants, IStaffRepository staff, ISettingsRepository settings,
        ITenantContextSetter tenantContext, ITenantStarterContent starter, IPasswordHasher hasher, IUnitOfWork uow, IClock clock)
        : ICommandHandler<Command, TenantDto>
    {
        public async Task<Result<TenantDto>> Handle(Command command, CancellationToken ct)
        {
            var now = clock.UtcNow;
            var created = Tenant.Create(command.Slug, command.Name, command.Plan, now);
            if (created.IsFailure) return created.Error!;
            var tenant = created.Value;
            if (await tenants.SlugExistsAsync(tenant.Slug, ct))
                return Error.Conflict("Tenant.SlugTaken", $"'{tenant.Slug}' is already taken.");
            if (await staff.EmailExistsAsync(command.OwnerEmail.Trim().ToLowerInvariant(), ct))
                return Error.Conflict("Staff.Duplicate", "Someone with this email already has an account.");

            tenants.Add(tenant);
            // Everything below belongs to the new restaurant.
            tenantContext.Set(tenant.Id, tenant.Slug);

            var s = RestaurantSettings.CreateDefault();
            var configured = s.Update(command.Name, null, command.Currency, 5, 0, command.TimeZoneId, null, null, null, true, 5, now);
            if (configured.IsFailure) return configured.Error!;
            settings.Add(s);
            staff.Add(StaffUser.Create(tenant.Id, command.OwnerEmail, command.OwnerName, StaffRole.Admin, hasher.Hash(command.OwnerPassword)));
            await uow.SaveChangesAsync(ct);

            if (command.WithSampleData) await starter.AddAsync(tenant.Id, ct);
            return tenant.ToDto();
        }
    }
}

public static class GetTenants
{
    public sealed record Query : IQuery<IReadOnlyList<TenantDto>>;

    internal sealed class Handler(ITenantRepository tenants) : IQueryHandler<Query, IReadOnlyList<TenantDto>>
    {
        public async Task<Result<IReadOnlyList<TenantDto>>> Handle(Query query, CancellationToken ct) =>
            (await tenants.GetAllAsync(ct)).OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
    }
}

public static class UpdateTenantSubscription
{
    public sealed record Command(Guid TenantId, TenantPlan Plan, TenantStatus Status) : ICommand<TenantDto>;

    internal sealed class Handler(ITenantRepository tenants, IUnitOfWork uow, IClock clock) : ICommandHandler<Command, TenantDto>
    {
        public async Task<Result<TenantDto>> Handle(Command command, CancellationToken ct)
        {
            var tenant = await tenants.GetAsync(command.TenantId, ct);
            if (tenant is null) return Error.NotFound("Tenant", command.TenantId);
            var now = clock.UtcNow;
            tenant.ChangePlan(command.Plan, now);
            if (command.Status == TenantStatus.Suspended) tenant.Suspend(now); else tenant.Activate(now);
            await uow.SaveChangesAsync(ct);
            return tenant.ToDto();
        }
    }
}
