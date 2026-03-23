using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aurora_LINK.Configuration;
using Aurora_LINK.Pages;
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
                    Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus.Text = $"Erreur de connexion : {ex.Message}";
            }
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
                await ShowMessageAsync("Export réussi",
                    $"Fichier .flora généré avec succès.\n\n" +
                    $"Chemin : {file.Path}\n" +
                    $"Taille : {AuroraConfiguration.FlashPageSize} bytes (page Flash complète)\n" +
                    $"CRC32 vérifié ✓");
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
            await ShowMessageAsync(
                "Fonctionnalité non disponible",
                "Le téléversement vers le module Aurora n’est pas encore implémenté.\n\n" +
                "Cette fonctionnalité sera disponible dans une prochaine version.");
        }
    }
}
