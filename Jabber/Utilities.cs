using System;
using Jabber.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jabber;

public static class Utilities
{
#if DEBUG
    public const bool IsDevBuild = true;
#else
    public const bool IsDevBuild = false;
#endif

    public static ILoggerFactory GetLoggerFactory(this IServiceProvider services)
        => services.GetRequiredService<ILoggerFactory>();

    public static ILogger<T> GetLogger<T>(this IServiceProvider services)
        => services.GetLoggerFactory().CreateLogger<T>();

    public static ILogger GetLogger(this IServiceProvider services, string categoryName)
        => services.GetLoggerFactory().CreateLogger(categoryName);

    public static IServiceCollection AddXmppServer(this IServiceCollection services, XmppOptions options)
    {
        var serviceId = Guid.NewGuid();

        services.AddKeyedSingleton(serviceId, (services, _) =>
        {
            var logger = services.GetLogger<XmppServer>();
            return new XmppServer(options, logger);
        });

        services.AddSingleton<IXmppServer>(s => s.GetKeyedService<XmppServer>(serviceId));
        services.AddSingleton<IHostedService>(s => s.GetKeyedService<XmppServer>(serviceId));

        return services;
    }

    public static IServiceCollection AddXmppServer(this IServiceCollection services, Action<XmppOptions> configure)
    {
        var serviceId = Guid.NewGuid();
        var options = XmppOptions.Default;

        configure(options);

        services.AddKeyedSingleton(serviceId, (services, _) =>
        {
            var logger = services.GetLogger<XmppServer>();
            return new XmppServer(options, logger);
        });

        services.AddSingleton<IXmppServer>(s => s.GetKeyedService<XmppServer>(serviceId));
        services.AddSingleton<IHostedService>(s => s.GetKeyedService<XmppServer>(serviceId));

        return services;
    }

    public static IServiceCollection AddXmppServer(this IServiceCollection services, string sectionName)
    {
        var serviceId = Guid.NewGuid();

        services.AddKeyedSingleton(serviceId, (services, _) =>
        {
            var options = services.GetService<IOptionsMonitor<XmppOptions>>();
            var logger = services.GetLogger<XmppServer>();
            return new XmppServer(options.Get(sectionName), logger);
        });

        services.AddSingleton<IXmppServer>(s => s.GetKeyedService<XmppServer>(serviceId));
        services.AddSingleton<IHostedService>(s => s.GetKeyedService<XmppServer>(serviceId));

        return services;
    }
}
