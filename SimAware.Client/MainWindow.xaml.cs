using SimAware.Client.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SimAware.Client.SimConnectFSX;

namespace SimAware.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private int MinimumUpdatePeriod = 500;

        private readonly Random random = new Random();

        private readonly MainViewModel viewModel;
        private readonly DiscordRichPresenceLogic discordRichPresenceLogic;
        private readonly IFlightConnector flightConnector;
        public MainWindow(IFlightConnector flightConnector, MainViewModel viewModel, DiscordRichPresenceLogic discordRichPresenceLogic)
        {
            InitializeComponent();

            this.flightConnector = flightConnector;
            this.viewModel = viewModel;
            this.discordRichPresenceLogic = discordRichPresenceLogic;


        }

        public void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Title = "SimAware Client";

            viewModel.Callsign = GenerateCallSign();

            discordRichPresenceLogic.Initialize();
            discordRichPresenceLogic.Start(viewModel.Callsign);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private string GenerateCallSign()
        {
            var builder = new StringBuilder();
            builder.Append(((char)('A' + random.Next(26))).ToString());
            builder.Append(((char)('A' + random.Next(26))).ToString());
            builder.Append("-");
            builder.Append(((char)('A' + random.Next(26))).ToString());
            builder.Append(((char)('A' + random.Next(26))).ToString());
            builder.Append(((char)('A' + random.Next(26))).ToString());
            return builder.ToString();
        }
    }
}
