using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aurora_LINK.Configuration;
using Aurora_LINK.Pages;
using Link.Core.Transport;
using Link.Client;
using Link.Client.Discovery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Aurora_LINK
{
    public sealed partial class MainWindow : Window
    {
        public LinkClient? Client { get; private set; }
        public ILinkTransport? Transport { get; private set; }
        public LinkDetectedDevice? ConnectedDevice { get; private set; }

        private readonly AuroraProjectService _projectService = AuroraProjectService.Instance;

        private static readonly Dictionary<string, Type> PageMap = new()
        {
            { "Dashboard", typeof(DashboardPage) },
            { "Inputs",    typeof(InputsPage) },
            { "Scenes",    typeof(ScenesPage) },
            { "System",    typeof(SystemPage) },
        };

        public MainWindow()
        {
            InitializeComponent();
            _projectService.DirtyChanged += OnProjectDirtyChanged;
            _projectService.ProjectChanged += OnProjectChanged;
            UpdateProjectStatus();
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
                    Transport = dialog.ConnectedTransport;
                    ConnectedDevice = dialog.SelectedDevice;

                    string model = ConnectedDevice?.DeviceInfo.Model ?? "Inconnu";
                    string port = ConnectedDevice?.PortName ?? "";

                    ConnectionStatus.Text = $"Connecté à {model} ({port})";
                    BtnReconnect.Visibility = Visibility.Visible;

                    NavView.SelectedItem = NavView.MenuItems[0];
                }
                else
                {
                    ConnectionStatus.Text = "Non connecté";
                    BtnReconnect.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text = $"Erreur de connexion : {ex.Message}";
                BtnReconnect.Visibility = Visibility.Visible;
            }
        }

        private async void BtnReconnect_Click(object sender, RoutedEventArgs e)
        {
            await DisconnectCurrentAsync();
            await ShowConnectionDialogAsync();
        }

        private async Task DisconnectCurrentAsync()
        {
            if (Transport is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }

            Client = null;
            Transport = null;
            ConnectedDevice = null;
            ConnectionStatus.Text = "Non connecté";
        }

        // ───────────────── Gestion de projet ─────────────────

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            _projectService.NewProject();
        }

        private async void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(AuroraProjectService.FileExtension);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                await _projectService.OpenAsync(file.Path);
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_projectService.FilePath is not null)
            {
                await _projectService.SaveAsync();
            }
            else
            {
                await SaveAsInternalAsync();
            }
        }

        private async void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            await SaveAsInternalAsync();
        }

        private async Task SaveAsInternalAsync()
        {
            var picker = new FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeChoices.Add("Projet Aurora", [AuroraProjectService.FileExtension]);
            picker.SuggestedFileName = _projectService.DisplayName;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            var file = await picker.PickSaveFileAsync();
            if (file is not null)
            {
                await _projectService.SaveAsAsync(file.Path);
            }
        }

        private void OnProjectDirtyChanged(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(UpdateProjectStatus);
        }

        private void OnProjectChanged(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateProjectStatus();
                RefreshCurrentPage();
            });
        }

        private void UpdateProjectStatus()
        {
            var name = _projectService.DisplayName;
            ProjectStatus.Text = _projectService.IsDirty ? $"{name} ●" : name;
        }

        // ───────────────── Rafraîchissement UI ─────────────────

        private void RefreshCurrentPage()
        {
            if (ContentFrame.Content is Page currentPage)
            {
                ContentFrame.Navigate(currentPage.GetType());
            }
        }

        // ───────────────── Export / Import .flora ─────────────────

        private async void BtnExportFlora_Click(object sender, RoutedEventArgs e)
        {
            // Valider d'abord
            var validation = _projectService.ValidateConfiguration();
            if (!validation.IsValid)
            {
                await ShowMessageAsync("Validation échouée", validation.Summary);
                return;
            }

            // Avertissements éventuels — demander confirmation
            if (validation.Warnings.Count > 0)
            {
                var confirm = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Avertissements",
                    Content = validation.Summary + "\n\nExporter quand même ?",
                    PrimaryButtonText = "Exporter",
                    CloseButtonText = "Annuler",
                    DefaultButton = ContentDialogButton.Primary,
                };

                if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                    return;
            }

            // Choisir le fichier de sortie
            var picker = new FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeChoices.Add("Firmware Aurora", [AuroraProjectService.FloraExtension]);
            picker.SuggestedFileName = _projectService.DisplayName;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;

            var error = await _projectService.ExportFloraAsync(file.Path);
            if (error is not null)
            {
                await ShowMessageAsync("Erreur d'export", error);
            }
            else
            {
                var fileInfo = new System.IO.FileInfo(file.Path);
                await ShowMessageAsync("Export réussi",
                    $"Fichier .flora généré avec succès.\n\n" +
                    $"Chemin : {file.Path}\n" +
                    $"Taille : {fileInfo.Length} bytes\n" +
                    $"Signature FLOR ✓ — CRC32 vérifié ✓");
            }
        }

        private async void BtnImportFlora_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(AuroraProjectService.FloraExtension);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            var file = await picker.PickSingleFileAsync();
            if (file is null)
                return;

            var error = await _projectService.ImportFloraAsync(file.Path);
            if (error is not null)
            {
                await ShowMessageAsync("Erreur d'import", error);
            }
            else
            {
                await ShowMessageAsync("Import réussi",
                    $"Configuration chargée depuis {file.Name}.\n" +
                    $"Les pages affichées reflèteront la nouvelle configuration.");
            }
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = "OK",
            };
            await dialog.ShowAsync();
        }

        // ───────────────── Téléversement ─────────────────

        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            if (Client is null)
            {
                await ShowMessageAsync("Erreur", "Aucun appareil connecté.");
                return;
            }

            // Valider la configuration
            var validation = _projectService.ValidateConfiguration();
            if (!validation.IsValid)
            {
                await ShowMessageAsync("Validation échouée", validation.Summary);
                return;
            }

            // Avertissements éventuels — demander confirmation
            if (validation.Warnings.Count > 0)
            {
                var confirm = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Avertissements",
                    Content = validation.Summary + "\n\nTéléverser quand même ?",
                    PrimaryButtonText = "Téléverser",
                    CloseButtonText = "Annuler",
                    DefaultButton = ContentDialogButton.Primary,
                };

                if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                    return;
            }

            // Sérialiser la configuration en binaire .flora
            var config = _projectService.GetConfiguration();
            byte[] floraData;
            try
            {
                floraData = AuroraConfigSerializer.Serialize(config);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Erreur de sérialisation", ex.Message);
                return;
            }

            // Vérification de relecture avant envoi
            if (AuroraConfigSerializer.Deserialize(floraData) is null)
            {
                await ShowMessageAsync("Erreur",
                    "Vérification du programme .flora échouée (CRC ou structure invalide).");
                return;
            }

            await PerformUploadAsync(floraData);
        }

        private async Task PerformUploadAsync(byte[] floraData)
        {
            using var cts = new CancellationTokenSource();

            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = floraData.Length,
                Value = 0,
            };

            var statusText = new TextBlock
            {
                Text = "Préparation du téléversement…",
            };

            var panel = new StackPanel
            {
                Spacing = 12,
                MinWidth = 340,
                Children = { statusText, progressBar },
            };

            var progressDialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Téléversement",
                Content = panel,
                CloseButtonText = "Annuler",
            };

            progressDialog.CloseButtonClick += (_, _) => cts.Cancel();

            var progress = new Progress<(int Sent, int Total)>(p =>
            {
                progressBar.Value = p.Sent;
                statusText.Text = $"Envoi… {p.Sent} / {p.Total} bytes";
            });

            string? errorMessage = null;
            bool cancelled = false;

            var dialogTask = progressDialog.ShowAsync();

            try
            {
                await AuroraUploadService.UploadAsync(Client!, floraData, progress, cts.Token);
                progressDialog.Hide();
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                progressDialog.Hide();
                errorMessage = ex.Message;
            }

            try { await dialogTask; } catch { }

            if (cancelled)
            {
                await ShowMessageAsync("Annulé", "Le téléversement a été annulé.");
            }
            else if (errorMessage is not null)
            {
                await ShowMessageAsync("Erreur de téléversement", errorMessage);
            }
            else
            {
                await ShowMessageAsync("Téléversement réussi",
                    $"Programme envoyé avec succès.\n\n" +
                    $"Taille : {floraData.Length} bytes\n" +
                    $"Intégrité vérifiée par le device ✓");
            }
        }
    }
}
