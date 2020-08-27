using System;
using SimAware.Client.Logic;
using SimAware.Client.SimConnectFSX;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DiscordRPC;
using System.Diagnostics;
using System.Windows.Interop;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.IO;
using DiscordRPC.Logging;

namespace SimAware.Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        #region Single Instance Enforcer

        readonly SingletonApplicationEnforcer enforcer = new SingletonApplicationEnforcer(args =>
        {
            Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Current.MainWindow as MainWindow;
                if (mainWindow != null && args != null)
                {
                    mainWindow.RestoreWindow();
                }
            });
        }, "SimAware.Client");

        #endregion

        public ServiceProvider ServiceProvider { get; private set; }

        private MainWindow mainWindow = null;
        private IntPtr Handle;

        protected override void OnStartup(StartupEventArgs e)
        {
            if(!e.Args.Contains("--dev-instance") && enforcer.ShouldApplicationExit())
            {
                try
                {
                    Shutdown();
                }
                catch { }
            }

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Loaded += MainWindow_Loaded;
            mainWindow.Show();
        }

        private void ConfigureServices(ServiceCollection services)
        {

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<IFlightConnector, MicrosoftSimConnection>();
            services.AddTransient(typeof(MainWindow));

            var discordRpcClient = new DiscordRpcClient("746726004998799460");
            discordRpcClient.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
            discordRpcClient.OnReady += (sender, e) =>
            {
                Debug.WriteLine("Connected to Discord RPC");
            };
            discordRpcClient.OnPresenceUpdate += (sender, e) =>
            {
                Debug.WriteLine($"Presence Updated {e.Presence}");
            };
            services.AddSingleton(discordRpcClient);
            services.AddSingleton<DiscordRichPresenceLogic>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var flightConnector = ServiceProvider.GetService<IFlightConnector>();
            if(flightConnector is MicrosoftSimConnection simConnect)
            {
                simConnect.Closed += SimConnect_Closed;

                Handle = new WindowInteropHelper(sender as Window).Handle;
                var HandleSource = HwndSource.FromHwnd(Handle);
                HandleSource.AddHook(simConnect.HandleSimConnectEvents);

                var viewModel = ServiceProvider.GetService<MainViewModel>();

                try
                {
                    await InitializeSimConnectAsync(simConnect, viewModel).ConfigureAwait(true);
                }
                catch (BadImageFormatException)
                {
                    var result = MessageBox.Show(mainWindow,
                        @"SimConnect has not been detected. This is essential to connect to Microsoft Flight Simulator.

Do you want to install it now?
When installation is complete, please restart.", 
                        "Missing Core Framework",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error
                        );

                    if(result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo {
                                FileName = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "SimConnect.msi"),
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    }

                    Shutdown(-1);
                }
            }
        }

        private async Task InitializeSimConnectAsync(MicrosoftSimConnection simConnect, MainViewModel viewModel)
        {
            while (true)
            {
                try
                {
                    var slowMode = false;
                    viewModel.SimConnectionState = ConnectionState.Connecting;
                    simConnect.Initialize(Handle, slowMode);
                    viewModel.SimConnectionState = ConnectionState.Connected;
                    break;
                }
                catch (COMException) {
                    viewModel.SimConnectionState = ConnectionState.Failed;
                    await Task.Delay(5000).ConfigureAwait(true);
                }
            }
        }

        private async void SimConnect_Closed(object sender, EventArgs e)
        {
            var simConnect = sender as MicrosoftSimConnection;
            var viewModel = ServiceProvider.GetService<MainViewModel>();
            viewModel.SimConnectionState = ConnectionState.Idle;

            await InitializeSimConnectAsync(simConnect, viewModel).ConfigureAwait(true);

        }

    }
}
