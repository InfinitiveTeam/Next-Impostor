using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using Impostor.Api.Config;
using Impostor.Api.Events.Managers;
using Impostor.Api.Games;
using Impostor.Api.Games.Managers;
using Impostor.Api.Net.Custom;
using Impostor.Api.Net.Manager;
using Impostor.Api.Plugins;
using Impostor.Api.Utils;
using Impostor.Hazel.Extensions;
using Impostor.Server.Events;
using Impostor.Server.Http;
using Impostor.Server.Net;
using Impostor.Server.Net.Custom;
using Impostor.Server.Net.Factories;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.Messages;
using Impostor.Server.Plugins;
using Impostor.Server.Recorder;
using Impostor.Server.Service;
using Impostor.Server.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Serilog;
using Serilog.Events;
using static Impostor.Server.Http.AdminController;
using Impostor.Server.VoiceChat.Interstellar;

namespace Impostor.Server
{
    internal static class Program
    {
        private static readonly string _logFolder = Path.Combine(Directory.GetCurrentDirectory(), "Log");
        public static string _serverUrl = "https://imp.xtreme.net.cn";

        private static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // 显示启动横幅
            ShowStartupBanner();

            var textDir = Path.Combine(Directory.GetCurrentDirectory(), "Text");
            if (!Directory.Exists(textDir))
            {
                Directory.CreateDirectory(textDir);
                LogToConsole("📁 Created Text directory", ConsoleColor.DarkGray);
            }

            if (!Directory.Exists(_logFolder))
            {
                Directory.CreateDirectory(_logFolder);
                LogToConsole("📁 Created Log directory", ConsoleColor.DarkGray);
            }

            LogToConsole("🔧 Initializing logging system...", ConsoleColor.DarkGray);

            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .WriteTo.Console(
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
               .WriteTo.File(
                    Path.Combine(_logFolder, "{Date}.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
               .CreateBootstrapLogger();

            try
            {
                var version = DotnetUtils.Version;
                LogToConsole($"🚀 Starting Next-Impostor v{version}", ConsoleColor.Cyan);
                LogToConsole($"📅 {DateTime.Now:yyyy-MM-dd HH:mm:ss}", ConsoleColor.DarkGray);
                LogToConsole($"🏃 Arguments: {(args.Length > 0 ? string.Join(" ", args) : "(none)")}", ConsoleColor.DarkGray);

                CreateHostBuilder(args).Build().Run();

                LogToConsole("👋 Next-Impostor shutdown completed", ConsoleColor.Green);
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                    FATAL ERROR                            ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                Log.Fatal(ex, "NImpostor terminated unexpectedly");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ShowStartupBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine(@"║                                                            ║");
            Console.WriteLine(@"║     ███╗   ██╗██╗███╗   ███╗██████╗  ██████╗ ███████╗      ║");
            Console.WriteLine(@"║     ████╗  ██║██║████╗ ████║██╔══██╗██╔═══██╗██╔════╝      ║");
            Console.WriteLine(@"║     ██╔██╗ ██║██║██╔████╔██║██████╔╝██║   ██║███████╗      ║");
            Console.WriteLine(@"║     ██║╚██╗██║██║██║╚██╔╝██║██╔═══╝ ██║   ██║╚════██║      ║");
            Console.WriteLine(@"║     ██║ ╚████║██║██║ ╚═╝ ██║██║     ╚██████╔╝███████║      ║");
            Console.WriteLine(@"║     ╚═╝  ╚═══╝╚═╝╚═╝     ╚═╝╚═╝      ╚═════╝ ╚══════╝      ║");
            Console.WriteLine(@"║                                                            ║");
            Console.WriteLine(@"║                    Among Us Server                         ║");
            Console.WriteLine(@"║                 https://amongusclub.cn/                    ║");
            Console.WriteLine(@"║                                                            ║");
            Console.WriteLine(@"╚════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        internal static void LogToConsole(string message, ConsoleColor color = ConsoleColor.White)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }

        private static IConfiguration CreateConfiguration(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder();

            configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
            configurationBuilder.AddJsonFile("config.json", true);
            configurationBuilder.AddJsonFile("config.Development.json", true);
            configurationBuilder.AddEnvironmentVariables(prefix: "IMPOSTOR_");
            configurationBuilder.AddCommandLine(args);

            var config = configurationBuilder.Build();
            LogToConsole($"📄 Configuration loaded ({(File.Exists("config.json") ? "config.json found" : "using defaults")})",
                        File.Exists("config.json") ? ConsoleColor.Green : ConsoleColor.Yellow);

            return config;
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            var configuration = CreateConfiguration(args);
            var pluginConfig = configuration.GetSection("PluginLoader")
                .Get<PluginConfig>() ?? new PluginConfig();
            var httpConfig = configuration.GetSection(HttpServerConfig.Section)
                .Get<HttpServerConfig>() ?? new HttpServerConfig();

            // LogToConsole($"🔌 Plugin loader: {pluginConfig.Paths.ToList()?.ToString()}", ConsoleColor.DarkGray);
            LogToConsole($"🌐 HTTP server: {(httpConfig.Enabled ? $"Enabled on {httpConfig.ListenIp}:{httpConfig.ListenPort}" : "Disabled")}",
                        httpConfig.Enabled ? ConsoleColor.Green : ConsoleColor.DarkGray);

            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseContentRoot(Directory.GetCurrentDirectory())
#if DEBUG
                .UseEnvironment(Environment.GetEnvironmentVariable("IMPOSTOR_ENV") ?? "Development")
#else
                .UseEnvironment("Production")
#endif
                .ConfigureAppConfiguration(builder =>
                {
                    builder.AddConfiguration(configuration);
                })
                .ConfigureServices((host, services) =>
                {
                    var debug = host.Configuration
                        .GetSection(DebugConfig.Section)
                        .Get<DebugConfig>() ?? new DebugConfig();

                    services.AddHttpClient();
                    services.AddSingleton<ServerEnvironment>();
                    services.AddSingleton<IServerEnvironment>(p => p.GetRequiredService<ServerEnvironment>());
                    services.AddSingleton<IDateTimeProvider, RealDateTimeProvider>();
                    services.AddSingleton<IpLocationService>();

                    services.AddSingleton<BanService>();

                    services.Configure<DebugConfig>(host.Configuration.GetSection(DebugConfig.Section));
                    services.Configure<AntiCheatConfig>(host.Configuration.GetSection(AntiCheatConfig.Section));
                    services.Configure<CompatibilityConfig>(host.Configuration.GetSection(CompatibilityConfig.Section));
                    services.Configure<ServerConfig>(host.Configuration.GetSection(ServerConfig.Section));
                    services.Configure<TimeoutConfig>(host.Configuration.GetSection(TimeoutConfig.Section));
                    services.Configure<HttpServerConfig>(host.Configuration.GetSection(HttpServerConfig.Section));
                    services.Configure<HostInfoConfig>(host.Configuration.GetSection(HostInfoConfig.Section));

                    services.AddMemoryCache();

                    services.AddSingleton<ICompatibilityManager, CompatibilityManager>();
                    services.AddSingleton<ClientManager>();
                    services.AddSingleton<IClientManager>(p => p.GetRequiredService<ClientManager>());
                    services.AddSingleton<EmailService>();

                    if (debug.GameRecorderEnabled)
                    {
                        LogToConsole("🎥 Game recorder enabled", ConsoleColor.Magenta);
                        services.AddSingleton<ObjectPoolProvider>(new DefaultObjectPoolProvider());
                        services.AddSingleton<ObjectPool<PacketSerializationContext>>(serviceProvider =>
                        {
                            var provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
                            var policy = new PacketSerializationContextPooledObjectPolicy();
                            return provider.Create(policy);
                        });

                        services.AddSingleton<PacketRecorder>();
                        services.AddHostedService(sp => sp.GetRequiredService<PacketRecorder>());
                        services.AddSingleton<IClientFactory, ClientFactory<ClientRecorder>>();
                    }
                    else
                    {
                        services.AddSingleton<IClientFactory, ClientFactory<Client>>();
                    }

                    services.AddSingleton<GameManager>();
                    services.AddSingleton<IGameManager>(p => p.GetRequiredService<GameManager>());
                    services.AddSingleton<ListingManager>();

                    services.AddEventPools();
                    services.AddHazel();
                    services.AddSingleton<ICustomMessageManager<ICustomRootMessage>, CustomMessageManager<ICustomRootMessage>>();
                    services.AddSingleton<ICustomMessageManager<ICustomRpc>, CustomMessageManager<ICustomRpc>>();
                    services.AddSingleton<IMessageWriterProvider, MessageWriterProvider>();
                    services.AddSingleton<IGameCodeFactory, GameCodeFactory>();
                    services.AddSingleton<IEventManager, EventManager>();
                    services.AddSingleton<Matchmaker>();
                    services.AddHostedService<MatchmakerService>();

                    LogToConsole("✅ Services registered successfully", ConsoleColor.Green);
                })
                .UseSerilog((context, loggerConfiguration) =>
                {
#if DEBUG
                    var logLevel = LogEventLevel.Debug;
                    LogToConsole("🔍 Debug mode enabled - Verbose logging", ConsoleColor.Yellow);
#else
                    var logLevel = LogEventLevel.Information;
#endif

                    if (args.Contains("--verbose"))
                    {
                        logLevel = LogEventLevel.Verbose;
                        LogToConsole("📢 Verbose logging enabled", ConsoleColor.Yellow);
                    }
                    else if (args.Contains("--errors-only"))
                    {
                        logLevel = LogEventLevel.Error;
                        LogToConsole("⚠️  Errors-only logging enabled", ConsoleColor.Yellow);
                    }

                    static Assembly? LoadSerilogAssembly(AssemblyLoadContext loadContext, AssemblyName name)
                    {
                        var paths = new[] { AppDomain.CurrentDomain.BaseDirectory, Directory.GetCurrentDirectory() };
                        foreach (var path in paths)
                        {
                            try
                            {
                                return loadContext.LoadFromAssemblyPath(Path.Combine(path, name.Name + ".dll"));
                            }
                            catch (FileNotFoundException)
                            {
                            }
                        }

                        return null;
                    }

                    AssemblyLoadContext.Default.Resolving += LoadSerilogAssembly;

                    loggerConfiguration
                    .MinimumLevel.Is(logLevel)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(
                        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
                    .WriteTo.File(
                        Path.Combine(_logFolder, "{Date}.log"),
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                        );
                    AssemblyLoadContext.Default.Resolving -= LoadSerilogAssembly;
                })
                .UseConsoleLifetime()
                .UsePluginLoader(pluginConfig);

            if (httpConfig.Enabled)
            {
                hostBuilder.ConfigureWebHostDefaults(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddControllers();
                        if (httpConfig.EnableVoiceChatServer)
                        {
                            services.AddSingleton<VoiceRoomManager>();
                        }
                    });

                    builder.Configure(app =>
                    {
                        var pluginLoaderService = app.ApplicationServices.GetRequiredService<PluginLoaderService>();
                        foreach (var pluginInformation in pluginLoaderService.Plugins)
                        {
                            if (pluginInformation.Startup is IPluginHttpStartup httpStartup)
                            {
                                httpStartup.ConfigureWebApplication(app);
                            }
                        }

                        if (httpConfig.EnableVoiceChatServer)
                        {
                            app.UseWebSockets();
                            app.UseMiddleware<VoiceWebSocketMiddleware>();
                        }

                        app.UseRouting();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                        });
                    });

                    builder.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.Listen(IPAddress.Parse(httpConfig.ListenIp), httpConfig.ListenPort);
                    });
                });
            }

            LogToConsole("🎯 Host builder configured", ConsoleColor.Green);
            return hostBuilder;
        }
    }
}
