using System;
using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using JKang.IpcServiceFramework.Hosting;
using JKang.IpcServiceFramework.Hosting.NamedPipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NativeLib;
using TabletDriverLib;
using TabletDriverLib.Contracts;
using TabletDriverPlugin;

namespace OpenTabletDriver.Daemon
{
    using static Log;

    partial class Program
    {
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => 
            {
                var exception = (Exception)e.ExceptionObject;
                File.WriteAllLines(Path.Join(AppInfo.Current.AppDataDirectory, "daemon.log"),
                    new string[]
                    {
                        DateTime.Now.ToString(),
                        exception.GetType().FullName,
                        exception.Message,
                        exception.Source,
                        exception.StackTrace,
                        exception.TargetSite.Name
                    }
                );
            };

            var rootCommand = new RootCommand("OpenTabletDriver")
            {
                new Option(new string[] { "--appdata", "-a" }, "Application data directory")
                {
                    Argument = new Argument<DirectoryInfo>("appdata")
                },
                new Option(new string[] { "--config", "-c" }, "Configuration directory")
                {
                    Argument = new Argument<DirectoryInfo> ("config")
                },
                new Option(new string[] { "--hide", "-h"}, "Hide the daemon window.")
                {
                    Argument = new Argument<bool>("hideWindow")
                }
            };
            rootCommand.Handler = CommandHandler.Create<DirectoryInfo, DirectoryInfo, bool>((appdata, config, hideWindow) => 
            {
                AppInfo.Current.AppDataDirectory = appdata?.FullName;
                AppInfo.Current.ConfigurationDirectory = config?.FullName;
                HideWindow = hideWindow;
            });
            rootCommand.Invoke(args);

            Daemon = new DriverDaemon();
            if (HideWindow && PlatformInfo.IsWindows)
            {
                var windowHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                NativeLib.Windows.Windows.ShowWindow(windowHandle, 0);
            }
            await CreateHostBuilder().Build().RunAsync();
        }

        static IHostBuilder CreateHostBuilder() => 
            Host.CreateDefaultBuilder()
                .ConfigureServices(services => 
                {
                    services.AddSingleton<IDriverDaemon, DriverDaemon>((s) => Daemon);
                })
                .ConfigureIpcHost(builder => 
                {
                    builder.AddNamedPipeEndpoint<IDriverDaemon>("OpenTabletDriver");
                })
                .ConfigureLogging(builder => 
                {
                });

        static DriverDaemon Daemon { set; get; }
        static bool Running { set; get; }
        static bool HideWindow { set; get; }
    }
}
