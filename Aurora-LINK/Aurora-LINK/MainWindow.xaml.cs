using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aurora_LINK.Pages;
using Link.Client;
using Link.Client.Discovery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Aurora_LINK
{
    public sealed partial class MainWindow : Window
    {
        public LinkClient? Client { get; private set; }
        public LinkDetectedDevice? ConnectedDevice { get; private set; }

        private static readonly Dictionary<string, Type> PageMap = new()
        {
            { "Dashboard", typeof(DashboardPage) },
            { "LEDs",      typeof(LEDsPage) },
            { "Inputs",    typeof(InputsPage) },
            { "Scenes",    typeof(ScenesPage) },
            { "System",    typeof(SystemPage) },
        };

        public MainWindow()
        {
            InitializeComponent();
            ((FrameworkElement)Content).Loaded += OnContentLoaded;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item &&
                item.Tag is string tag &&
                PageMap.TryGetValue(tag, out var pageType))
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private async void OnContentLoaded(object sender, RoutedEventArgs e)
        {
            ((FrameworkElement)Content).Loaded -= OnContentLoaded;
            await ShowConnectionDialogAsync();
        }

        private async Task ShowConnectionDialogAsync()
        {
            var dialog = new ConnectionDialog(Content.XamlRoot);

            try
            {
                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && dialog.ConnectedClient is not null)
                {
                    Client = dialog.ConnectedClient;
                    ConnectedDevice = dialog.SelectedDevice;

                    string model = ConnectedDevice?.DeviceInfo.Model ?? "Inconnu";
                    string port = ConnectedDevice?.PortName ?? "";

                    if (dialog.IsAuthenticated)
                    {
                        ConnectionStatus.Text = $"Connecté à {model} ({port})";
                    }
                    else
                    {
                        ConnectionStatus.Text = $"Connecté à {model} ({port}) — verrouillé";
                    }
                }
                else
                {
                    ConnectionStatus.Text = "Non connecté — aucun appareil sélectionné";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text = $"Erreur de connexion : {ex.Message}";
            }
        }
    }
}
