using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestaurantCms.Application.Abstractions;
using RestaurantCms.Application.Behaviors;
using RestaurantCms.Application.Common;
using RestaurantCms.Application.Events;
using RestaurantCms.Application.Features.BackgroundJobs;
using RestaurantCms.Application.Features.Tables;

namespace RestaurantCms.Application;

public static class DependencyInjection
{
    /// <summary>Use cases from the Application assembly, with logging and validation wrapped around each.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(GetFloor).Assembly;
        var types = assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }).ToList();

        foreach (var type in types)
        {
            foreach (var itf in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var def = itf.GetGenericTypeDefinition();
                if (def == typeof(ICommandHandler<,>))
                {
                    var args = itf.GetGenericArguments();
                    services.AddScoped(type);
                    services.AddScoped(itf, sp =>
                    {
                        var handler = sp.GetRequiredService(type);
                        var validators = sp.GetServices(typeof(IValidator<>).MakeGenericType(args[0]));
                        var validating = Activator.CreateInstance(typeof(ValidatingCommandHandler<,>).MakeGenericType(args),
                            handler, CastEnumerable(validators, typeof(IValidator<>).MakeGenericType(args[0])))!;
                        var loggerType = typeof(ILogger<>).MakeGenericType(typeof(LoggingCommandHandler<,>).MakeGenericType(args));
                        return Activator.CreateInstance(typeof(LoggingCommandHandler<,>).MakeGenericType(args), validating, sp.GetRequiredService(loggerType))!;
                    });
                }
                else if (def == typeof(IQueryHandler<,>))
                {
                    var args = itf.GetGenericArguments();
                    services.AddScoped(type);
                    services.AddScoped(itf, sp =>
                    {
                        var validators = sp.GetServices(typeof(IValidator<>).MakeGenericType(args[0]));
                        return Activator.CreateInstance(typeof(ValidatingQueryHandler<,>).MakeGenericType(args),
                            sp.GetRequiredService(type), CastEnumerable(validators, typeof(IValidator<>).MakeGenericType(args[0])))!;
                    });
                }
                else if (def == typeof(IValidator<>))
                {
                    services.AddScoped(itf, type);
                }
                else if (def == typeof(IDomainEventHandler<>))
                {
                    services.AddScoped(itf, sp => sp.GetRequiredService(type));
                    services.AddScoped(type);
                }
            }
        }

        // Shared, non-handler application services.
        foreach (var type in types.Where(t => t.Name is "TableViews" or "OrderPlacement"))
            services.AddScoped(type);
        services.AddScoped<RestaurantJobs>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services;
    }


    private static object CastEnumerable(IEnumerable<object?> items, Type elementType)
    {
        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var item in items) list.Add(item);
        return list;
    }
}
