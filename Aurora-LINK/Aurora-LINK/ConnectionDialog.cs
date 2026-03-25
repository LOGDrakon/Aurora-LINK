using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Link.Client;
using Link.Client.Discovery;
using Link.Client.Extensions;
using Link.Core.Transport;
using Link.Transport.Serial;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

namespace Aurora_LINK;

public sealed class ConnectionDialog
{
    private readonly ContentDialog _dialog;
    private readonly ComboBox _comboBox;
    private readonly TextBlock _statusText;
    private readonly ProgressBar _progressBar;
    private readonly DispatcherQueue _dispatcherQueue;
    private LinkDeviceWatcher? _watcher;

    public LinkClient? ConnectedClient { get; private set; }
    public ILinkTransport? ConnectedTransport { get; private set; }
    public LinkDetectedDevice? SelectedDevice { get; private set; }
    public bool IsAuthenticated { get; private set; }

    public ConnectionDialog(XamlRoot xamlRoot)
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _statusText = new TextBlock
        {
            Text = "Recherche d'appareils LINK en cours..."
        };

        _progressBar = new ProgressBar
        {
            IsIndeterminate = true
        };

        _comboBox = new ComboBox
        {
            Header = "Appareil",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Aucun appareil détecté",
            ItemTemplate = (DataTemplate)XamlReader.Load(
                """
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <TextBlock>
                        <Run Text="{Binding PortName}" />
                        <Run Text=" : " />
                        <Run Text="{Binding DeviceInfo.Model}" />
                        <Run Text=" : " />
                        <Run Text="{Binding DeviceInfo.Uid}" />
                    </TextBlock>
                </DataTemplate>
                """)
        };
        _comboBox.SelectionChanged += OnSelectionChanged;

        var panel = new StackPanel
        {
            Spacing = 12,
            MinWidth = 360,
            Children =
            {
                _statusText,
                _progressBar,
                _comboBox
            }
        };

        _dialog = new ContentDialog
        {
            Title = "Connexion LINK",
            Content = panel,
            PrimaryButtonText = "Connecter",
            CloseButtonText = "Quitter",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
            XamlRoot = xamlRoot
        };
    }

    public async System.Threading.Tasks.Task<ContentDialogResult> ShowAsync()
    {
        StartWatcher();

        ContentDialogResult result;
        try
        {
            result = await _dialog.ShowAsync();
        }
        finally
        {
            await StopWatcherAsync();
        }

        if (result == ContentDialogResult.Primary &&
            _comboBox.SelectedItem is LinkDetectedDevice device)
        {
            try
            {
                var transport = new LinkSerialTransport(new LinkSerialOptions
                {
                    PortName = device.PortName,
                    BaudRate = 115200
                });

                var client = new LinkClient(new LinkClientOptions
                {
                    Transport = transport,
                    CommandTimeout = TimeSpan.FromSeconds(2)
                });

                await client.ConnectAsync();

                ConnectedClient = client;
                ConnectedTransport = transport;
                SelectedDevice = device;

                if (device.DeviceInfo.IsLocked)
                {
                    IsAuthenticated = await ShowAuthDialogAsync(client);
                }
                else
                {
                    IsAuthenticated = true;
                }
            }
            catch (Exception)
            {
                ConnectedClient = null;
                ConnectedTransport = null;
                SelectedDevice = null;
                throw;
            }
        }

        return result;
    }

    private void StartWatcher()
    {
        _watcher = new LinkDeviceWatcher(
            port => new LinkSerialTransport(new LinkSerialOptions
            {
                PortName = port,
                BaudRate = 115200
            }),
            timeout: TimeSpan.FromMilliseconds(800),
            appIdFilter: "AURORA");

        _comboBox.ItemsSource = _watcher.Devices;
        _watcher.Devices.CollectionChanged += OnDevicesChanged;
        _watcher.Start();
    }

    private async System.Threading.Tasks.Task StopWatcherAsync()
    {
        if (_watcher is not null)
        {
            _watcher.Devices.CollectionChanged -= OnDevicesChanged;
            await _watcher.DisposeAsync();
            _watcher = null;
        }
    }

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            int count = _watcher?.Devices.Count ?? 0;
            _statusText.Text = count > 0
                ? $"{count} appareil(s) détecté(s)"
                : "Recherche d'appareils LINK en cours...";
        });
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _dialog.IsPrimaryButtonEnabled = _comboBox.SelectedItem is LinkDetectedDevice;
    }

    private async Task<bool> ShowAuthDialogAsync(LinkClient client)
    {
        var passwordBox = new PasswordBox
        {
            PlaceholderText = "Mot de passe"
        };

        var errorText = new TextBlock
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Red),
            Visibility = Visibility.Collapsed
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children = { errorText, passwordBox }
        };

        var authDialog = new ContentDialog
        {
            Title = "Appareil verrouillé",
            Content = panel,
            PrimaryButtonText = "Déverrouiller",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _dialog.XamlRoot
        };

        while (true)
        {
            var authResult = await authDialog.ShowAsync();

            if (authResult != ContentDialogResult.Primary)
                return false;

            var password = passwordBox.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                errorText.Text = "Veuillez entrer un mot de passe.";
                errorText.Visibility = Visibility.Visible;
                continue;
            }

            try
            {
                var state = await client.AuthenticateAsync("AURORA", password);
                if (state.IsAuthenticated)
                    return true;

                errorText.Text = "Mot de passe incorrect.";
                errorText.Visibility = Visibility.Visible;
                passwordBox.Password = string.Empty;
            }
            catch (TimeoutException)
            {
                errorText.Text = "Délai d'attente dépassé. Réessayez.";
                errorText.Visibility = Visibility.Visible;
            }
        }
    }
}
