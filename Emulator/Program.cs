using Emulator.Entities.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace Emulator;

class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(theme: AnsiConsoleTheme.Code)
            //.WriteTo.File("App.log", rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true)
            .MinimumLevel.Verbose()
            .CreateBootstrapLogger();

        var builder = Host.CreateApplicationBuilder(args);
        {
            builder.Configuration
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile("appsettings.Development.json", true);

            var config = builder.Configuration;

            builder.Logging
                .ClearProviders()
                .AddSerilog();

            var services = builder.Services;
            {
                services.Configure<XmppOptions>(config.GetSection("Xmpp"));
                services.Configure<TlsOptions>(config.GetSection("Tls"));
                services.Configure<ChatOptions>(config.GetSection("GameChat"));
                services.AddBootstrapServices();
            }
        }
        var host = builder.Build();
        {
            host.UseBootstrapedServices();
        }
        await host.RunAsync();
    }
}
