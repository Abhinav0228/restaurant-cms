using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Domain;
using RestaurantCms.Domain.Staff;
using RestaurantCms.Domain.Tenancy;

namespace RestaurantCms.Infrastructure.Identity;

public static class Claims
{
    public const string UserId = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string TenantId = "tid";
    public const string TenantSlug = "tslug";
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "restaurant-cms";
    public string Audience { get; set; } = "restaurant-cms";
    /// <summary>At least 32 characters. Set via user-secrets / environment in every real deployment.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 12;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = SigningKey(),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        NameClaimType = Claims.Name,
        RoleClaimType = Claims.Role,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(StaffUser user, Tenant? tenant)
    {
        var o = options.Value;
        var expires = clock.UtcNow.AddHours(o.ExpiryHours);
        var claims = new List<Claim>
        {
            new(Claims.UserId, user.Id.ToString()),
            new("email", user.Email),
            new(Claims.Name, user.FullName),
            new(Claims.Role, user.Role.ToString())
        };
        if (tenant is not null)
        {
            claims.Add(new Claim(Claims.TenantId, tenant.Id.ToString()));
            claims.Add(new Claim(Claims.TenantSlug, tenant.Slug));
        }
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(o.SigningKey(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(claims)
        });
        return (token, expires);
    }
}

internal sealed class IdentityPasswordHasher : Application.Abstractions.IPasswordHasher
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object User = new();

    public string Hash(string password) => Hasher.HashPassword(User, password);
    public bool Verify(string hash, string password) => Hasher.VerifyHashedPassword(User, hash, password) != PasswordVerificationResult.Failed;
}

/// <summary>The signed-in staff member, read from the request's claims (empty in background jobs).</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    private ClaimsPrincipal? Principal => http.HttpContext?.User;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid? UserId => Guid.TryParse(Principal?.FindFirst(Claims.UserId)?.Value, out var id) ? id : null;
    public string? Name => Principal?.FindFirst(Claims.Name)?.Value;
    public StaffRole? Role => Enum.TryParse<StaffRole>(Principal?.FindFirst(Claims.Role)?.Value, out var r) ? r : null;
}

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
