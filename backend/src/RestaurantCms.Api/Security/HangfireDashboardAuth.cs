using Hangfire.Dashboard;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using RestaurantCms.Infrastructure.Identity;

namespace RestaurantCms.Api.Security;

/// <summary>Hangfire dashboard: local requests in Development, otherwise a platform-admin token.
/// Open /hangfire?access_token=… once; it is then kept in an HttpOnly cookie for the dashboard's own requests.</summary>
public sealed class HangfireDashboardAuth(IWebHostEnvironment env) : IDashboardAsyncAuthorizationFilter
{
    private const string Cookie = "hf_token";

    public async Task<bool> AuthorizeAsync(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (env.IsDevelopment() && http.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip)) return true;

        var token = http.Request.Query["access_token"].ToString();
        var fromQuery = !string.IsNullOrEmpty(token);
        if (!fromQuery) token = http.Request.Cookies[Cookie] ?? "";
        if (string.IsNullOrEmpty(token)) return false;

        var jwt = http.RequestServices.GetRequiredService<IOptions<JwtOptions>>().Value;
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, jwt.ValidationParameters());
        if (!result.IsValid || result.ClaimsIdentity.FindFirst(Claims.Role)?.Value != "PlatformAdmin") return false;

        if (fromQuery)
            http.Response.Cookies.Append(Cookie, token, new CookieOptions { HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/hangfire" });
        return true;
    }
}
