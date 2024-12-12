using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Emulator;

public static class Extensions
{
    public static IServiceCollection AddBackgroundService<T>(this IServiceCollection services)
        where T : BackgroundService
    {
        return services.AddSingleton<T>()
            .AddHostedService(s => s.GetRequiredService<T>());
    }
}
