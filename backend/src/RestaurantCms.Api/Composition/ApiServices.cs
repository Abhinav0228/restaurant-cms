using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RestaurantCms.Api.Caching;
using RestaurantCms.Api.Errors;
using RestaurantCms.Api.Security;
using RestaurantCms.Application;
using RestaurantCms.Infrastructure;
using RestaurantCms.Infrastructure.Caching;
using RestaurantCms.Infrastructure.Identity;
using RestaurantCms.Infrastructure.Realtime;

namespace RestaurantCms.Api.Composition;

public static class ApiServices
{
    public static WebApplicationBuilder AddApi(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;

        services.AddApplication();
        services.AddInfrastructure(config, runHangfireServer: config.GetValue("Hangfire:RunServer", true));

        // JSON: enums as strings everywhere (HTTP and SignalR)
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Auth: JWT bearer (SignalR passes the token in the query string)
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = jwt.Value.ValidationParameters();
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments(OrdersHub.Path))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });
        Policies.Configure(services.AddAuthorizationBuilder());

        // Errors, docs, health
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddOpenApi();
        services.AddMemoryCache();

        services.AddCors(o => o.AddDefaultPolicy(p => p
            .WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        // Public endpoints are rate limited per restaurant + client IP
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(Policies.PublicRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
                $"{ctx.GetRouteValue("tenant")}|{ctx.Connection.RemoteIpAddress}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
        });

        // Output cache for the public menu, evicted per restaurant on change
        services.AddOutputCache(o => o.AddPolicy(PublicMenuCachePolicy.Name, new PublicMenuCachePolicy()));
        services.AddSingleton<ITenantCacheInvalidator, OutputCacheInvalidator>();

        // Observability: traces + metrics; exported when OTEL_EXPORTER_OTLP_ENDPOINT is set
        var otel = services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("restaurant-cms-api"))
            .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddNpgsql())
            .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentationIfAvailable());
        if (!string.IsNullOrWhiteSpace(config["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otel.UseOtlpExporter();

        return builder;
    }

    private static MeterProviderBuilder AddRuntimeInstrumentationIfAvailable(this MeterProviderBuilder m) =>
        m.AddMeter("System.Runtime");
}
