using System.Net.Mail;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Common;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Common;
using RestaurantCms.Domain.Staff;

namespace RestaurantCms.Application.Features.Identity;

public sealed record StaffDto(Guid Id, string Email, string FullName, StaffRole Role, bool IsActive);
public sealed record TenantSummary(Guid Id, string Slug, string Name);
public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, StaffDto User, TenantSummary? Tenant);

public static class StaffMapping
{
    public static StaffDto ToDto(this StaffUser u) => new(u.Id, u.Email, u.FullName, u.Role, u.IsActive);
}

public static class Login
{
    public sealed record Command(string Email, string Password) : ICommand<LoginResponse>;

    internal sealed class Handler(IStaffRepository staff, ITenantRepository tenants, IPasswordHasher hasher, ITokenService tokens)
        : ICommandHandler<Command, LoginResponse>
    {
        public async Task<Result<LoginResponse>> Handle(Command command, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
                return Error.Unauthorized("Email and password are required.");
            var user = await staff.FindForSignInAsync(command.Email.Trim().ToLowerInvariant(), ct);
            // Same message for unknown user and wrong password: don't reveal which emails exist.
            if (user is null || !user.IsActive || !hasher.Verify(user.PasswordHash, command.Password))
                return Error.Unauthorized("Invalid email or password.");

            var tenant = user.TenantId is { } tid ? await tenants.GetAsync(tid, ct) : null;
            if (user.TenantId is not null && (tenant is null || !tenant.IsActive))
                return Error.Unauthorized("This restaurant's account is suspended. Contact support.");

            var (token, expires) = tokens.CreateToken(user, tenant);
            return new LoginResponse(token, expires, user.ToDto(), tenant is null ? null : new TenantSummary(tenant.Id, tenant.Slug, tenant.Name));
        }
    }
}

public static class GetStaff
{
    public sealed record Query : IQuery<IReadOnlyList<StaffDto>>;

    internal sealed class Handler(IStaffRepository staff) : IQueryHandler<Query, IReadOnlyList<StaffDto>>
    {
        public async Task<Result<IReadOnlyList<StaffDto>>> Handle(Query query, CancellationToken ct) =>
            (await staff.GetForTenantAsync(ct)).OrderBy(u => u.Role).ThenBy(u => u.FullName).Select(u => u.ToDto()).ToList();
    }
}

public static class CreateStaff
{
    public sealed record Command(string Email, string FullName, StaffRole Role, string Password) : ICommand<StaffDto>;

    internal sealed class Validator : Validator<Command>
    {
        public Validator()
        {
            Required("fullName", x => x.FullName);
            MaxLength("fullName", x => x.FullName, 80);
            Rule("email", x => !string.IsNullOrWhiteSpace(x.Email) && MailAddress.TryCreate(x.Email.Trim(), out _), "Enter a valid email.");
            Rule("password", x => (x.Password ?? "").Length >= 8, "Password must be at least 8 characters.");
            Rule("role", x => x.Role != StaffRole.PlatformAdmin, "Restaurant staff cannot be platform admins.");
        }
    }

    internal sealed class Handler(IStaffRepository staff, ITenantRepository tenants, ITenantContext tenant, IPasswordHasher hasher, IUnitOfWork uow)
        : ICommandHandler<Command, StaffDto>
    {
        public async Task<Result<StaffDto>> Handle(Command command, CancellationToken ct)
        {
            var limits = (await tenants.GetAsync(tenant.TenantId, ct))?.Limits;
            if (limits is not null && await staff.CountForTenantAsync(ct) >= limits.MaxStaff)
                return Error.Conflict("Plan.StaffLimit", $"Your plan allows {limits.MaxStaff} staff accounts. Upgrade to add more.");
            if (await staff.EmailExistsAsync(command.Email.Trim().ToLowerInvariant(), ct))
                return Error.Conflict("Staff.Duplicate", "Someone with this email already has an account.");

            var user = StaffUser.Create(tenant.TenantId, command.Email, command.FullName, command.Role, hasher.Hash(command.Password));
            staff.Add(user);
            await uow.SaveChangesAsync(ct);
            return user.ToDto();
        }
    }
}
