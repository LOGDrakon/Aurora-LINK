using System.ComponentModel;
using Aurora_LINK.Configuration;
using Microsoft.UI.Xaml.Controls;

namespace Aurora_LINK.Pages
{
    public sealed partial class InputsPage : Page
    {
        public InputsPageViewModel ViewModel { get; } = new();

        private readonly AuroraProjectService _projectService = AuroraProjectService.Instance;

        public InputsPage()
        {
            InitializeComponent();

            // Charger depuis le projet courant
            var config = _projectService.GetConfiguration();
            ViewModel.LoadFrom(config);

            // Écouter les modifications de chaque entrée pour enregistrer dynamiquement
            foreach (var input in ViewModel.Inputs)
                input.PropertyChanged += OnInputPropertyChanged;
        }

        private void OnInputPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Sauvegarder les modifications dans le projet
            var config = _projectService.GetConfiguration();
            ViewModel.SaveTo(config);
            _projectService.LoadConfiguration(config);
        }
    }
}
