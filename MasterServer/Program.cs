using Jabber;
using Jabber.Net;
using Jabber.Sasl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using XmppSharp;
using XmppSharp.Protocol.Core.Sasl;

namespace MasterServer;

static class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(theme: AnsiConsoleTheme.Code)
            .MinimumLevel.Verbose()
            .CreateLogger();

        var builder = Host.CreateApplicationBuilder(args);
        {
            var env = builder.Environment;

            builder.Logging.ClearProviders()
                .AddSerilog();

            builder.Configuration
                .AddJsonFile("appsettings.json", Utilities.IsDevBuild)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true)

                // optional, warface related settings
                .AddJsonFile("Lobby.json", true)
                .AddJsonFile("Inventory.json", true)
                .AddJsonFile("Shop.json", true)
                ;

            var config = builder.Configuration;
            var services = builder.Services;

            services.AddXmppServer(XmppOptions.Default);
        }
        var app = builder.Build();
        {
            var server = app.Services.GetRequiredService<IXmppServer>();

            server.OnConnection += connection =>
            {
                AuthenticationHandler? handler = default;

                connection.OnAuthentication += element =>
                {
                    if (element is Auth auth && handler == null)
                    {
                        Log.Debug("auth received. with mechanism {Name}", auth.Mechanism);

                        handler = AuthenticationHandler
                                .SupportedMechanisms
                                .FirstOrDefault(x => x.Key == auth.Mechanism)
                                .Value;

                        Log.Debug("auth handler instance: {Obj}", handler);

                        if (handler == null)
                            throw new JabberSaslException(FailureCondition.Aborted);
                    }

                    AsyncHelper.RunSync(() => handler!.Invoke(connection, element));

                    Log.Debug("invoke auth handler {Obj}", handler);
                };
            };
        }
        await app.RunAsync();
    }
}
