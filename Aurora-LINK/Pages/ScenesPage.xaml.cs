using System.ComponentModel;
using Aurora_LINK.Configuration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Aurora_LINK.Pages
{
    public sealed partial class ScenesPage : Page
    {
        public ScenesPageViewModel ViewModel { get; } = new();

        private readonly AuroraProjectService _projectService = AuroraProjectService.Instance;

        public ScenesPage()
        {
            InitializeComponent();

            // Charger depuis le projet courant
            var config = _projectService.GetConfiguration();
            ViewModel.LoadFrom(config);

            // Écouter les modifications de chaque scène
            foreach (var scene in ViewModel.Scenes)
                scene.PropertyChanged += OnScenePropertyChanged;

            ViewModel.Scenes.CollectionChanged += OnScenesCollectionChanged;
        }

        private void OnAddScene(object sender, RoutedEventArgs e)
        {
            ViewModel.AddScene();
            // Écouter la nouvelle scène
            if (ViewModel.SelectedScene is not null)
                ViewModel.SelectedScene.PropertyChanged += OnScenePropertyChanged;
            SaveToProject();
        }

        private void OnRemoveScene(object sender, RoutedEventArgs e)
        {
            ViewModel.RemoveSelected();
            SaveToProject();
        }

        private void OnScenesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Pas besoin de sauvegarder ici car Add/Remove le font déjà
        }

        private void OnScenePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SaveToProject();
        }

        private void SaveToProject()
        {
            var config = _projectService.GetConfiguration();
            ViewModel.SaveTo(config);
            _projectService.LoadConfiguration(config);
        }
    }
}
