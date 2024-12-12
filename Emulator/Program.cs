using Emulator.Entities.Options;
using Emulator.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Emulator;

class Program
{
    static async Task Main(string[] args)
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Warning);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(theme: AnsiConsoleTheme.Code)
#if DEBUG
            .WriteTo.Conditional
            (
                s => s.Properties.ContainsKey("Xmpp"),
                s => s.File("Xmpp.log", restrictedToMinimumLevel: LogEventLevel.Verbose)
            )
#endif
            .WriteTo.Conditional
            (
                s => !s.Properties.ContainsKey("Xmpp"),
                s => s.File("Emulator.log")
            )
            .MinimumLevel.ControlledBy(levelSwitch)
            .CreateBootstrapLogger();

        var builder = Host.CreateApplicationBuilder(args);
        {
            builder.Logging
                .ClearProviders()
                .AddSerilog();

            builder.Configuration
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile("appsettings.Development.json", true);

            var config = builder.Configuration;

            var services = builder.Services;
            {
                services.Configure<XmppServerOptions>(config.GetSection("Xmpp"));
                services.Configure<TlsOptions>(config.GetSection("Tls"));
                services.AddBackgroundService<XmppServer>();
            }

            if (args.Contains("-devmode"))
                levelSwitch.MinimumLevel = LogEventLevel.Verbose;
            else
            {
                if (builder.Environment.IsDevelopment())
                    levelSwitch.MinimumLevel = LogEventLevel.Debug;
                else if (builder.Environment.IsStaging())
                    levelSwitch.MinimumLevel = LogEventLevel.Information;
                else
                    levelSwitch.MinimumLevel = LogEventLevel.Warning;
            }
        }
        var host = builder.Build();
        {

        }
        await host.RunAsync();
    }
}
