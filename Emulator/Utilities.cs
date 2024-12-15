using System.Reflection;
using Emulator.Abstractions;
using Emulator.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using XmppSharp;

namespace Emulator;

public static class Utilities
{
    public static IServiceCollection AddBootstrapServices(this IServiceCollection services)
    {
        var types = from t in typeof(Utilities).Assembly.GetTypes()
                    where t.GetCustomAttribute<BootstrapAttribute>() != null
                    select t;

        foreach (var serviceType in types)
        {
            services.AddSingleton(serviceType);

            if (serviceType.GetInterface("IBootstrap") != null)
                services.AddSingleton(typeof(IBootstrap), s => s.GetRequiredService(serviceType));

            if (serviceType.IsSubclassOf(typeof(BackgroundService)))
                services.AddSingleton(typeof(IHostedService), s => s.GetRequiredService(serviceType));

            foreach (var attribute in serviceType.GetCustomAttributes().OfType<IServiceImplementation>())
                services.AddSingleton(attribute.ImplementationType, s => s.GetRequiredService(serviceType));
        }

        return services;
    }

    public static IHost UseBootstrapedServices(this IHost host)
    {
        foreach (var service in host.Services.GetServices<IBootstrap>())
            Log.Debug("Starting service {Type}", service.GetType());

        return host;
    }

    public static uint FNV32(this string s) => FNV32(s.GetBytes());
    public static ulong FNV64(this string s) => FNV64(s.GetBytes());

    public static uint FNV32(this byte[] bytes)
    {
        const uint basis = 0x811c9dc5;
        const uint prime = 0x01000193;

        uint hash = basis;

        foreach (byte @byte in bytes)
        {
            hash *= prime;
            hash ^= @byte;
        }

        return hash;
    }

    public static ulong FNV64(this byte[] bytes)
    {
        const ulong basis = 0xcbf29ce484222325;
        const ulong prime = 0x00000100000001b3;

        ulong hash = basis;

        foreach (byte @byte in bytes)
        {
            hash *= prime;
            hash ^= @byte;
        }

        return hash;
    }
}
