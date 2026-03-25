using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Aurora_LINK.Pages
{
    public sealed partial class SystemPage : Page
    {
        public SystemPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var device = App.MainWindow?.ConnectedDevice;
            if (device is null)
                return;

            DeviceModel.Text = device.DeviceInfo.Model ?? "Inconnu";
            DeviceFirmware.Text = !string.IsNullOrEmpty(device.DeviceInfo.Version)
                ? device.DeviceInfo.Version
                : "Inconnue";
            DeviceUid.Text = device.DeviceInfo.Uid ?? "Non disponible";
            DevicePort.Text = device.PortName;
        }

        private async void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            await ShowNotImplementedAsync();
        }

        private async Task ShowNotImplementedAsync()
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Fonctionnalité non disponible",
                Content = "La modification du mot de passe n'est pas encore implémentée.\n\n" +
                          "Cette fonctionnalité sera disponible dans une prochaine version.",
                CloseButtonText = "OK",
            };
            await dialog.ShowAsync();
        }
    }
}
