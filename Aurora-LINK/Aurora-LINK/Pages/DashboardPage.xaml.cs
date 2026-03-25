using System;
using System.Linq;
using Aurora_LINK.Configuration;
using Link.Core.Frames;
using Link.Core.Transport;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Aurora_LINK.Pages
{
    public sealed partial class DashboardPage : Page
    {
        private const int InputCount = 10;

        private readonly Ellipse[] _indicators = new Ellipse[InputCount];
        private readonly TextBlock[] _stateLabels = new TextBlock[InputCount];
        private readonly DispatcherQueue _dispatcherQueue;

        private ILinkTransport? _transport;

        public DashboardPage()
        {
            InitializeComponent();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            BuildInputIndicators();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void BuildInputIndicators()
        {
            for (int i = 0; i < InputCount; i++)
            {
                int col = i % 5;
                int row = i / 5;

                var cell = new StackPanel
                {
                    Spacing = 4,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var label = new TextBlock
                {
                    Text = $"I{i}",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var indicator = new Ellipse
                {
                    Width = 18,
                    Height = 18,
                    Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var stateText = new TextBlock
                {
                    Text = "—",
                    FontSize = 12,
                    Foreground = Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var br)
                        ? (Brush)br
                        : new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                cell.Children.Add(label);
                cell.Children.Add(indicator);
                cell.Children.Add(stateText);

                Grid.SetColumn(cell, col);
                Grid.SetRow(cell, row);
                InputsGrid.Children.Add(cell);

                _indicators[i] = indicator;
                _stateLabels[i] = stateText;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var project = AuroraProjectService.Instance.Project;
            PopulateActiveScene(project);

            var transport = App.MainWindow?.Transport;
            if (transport is null)
            {
                InputsConnectionStatus.Text = "Aucun appareil connecté";
                return;
            }

            _transport = transport;
            InputsConnectionStatus.Visibility = Visibility.Collapsed;
            InputsGrid.Visibility = Visibility.Visible;

            _transport.FrameReceived += OnFrameReceived;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_transport is not null)
            {
                _transport.FrameReceived -= OnFrameReceived;
                _transport = null;
            }
        }

        private void OnFrameReceived(LinkFrame frame)
        {
            // Le device envoie LINK:AURORA:GETINPUT:<payload>
            if (frame.AppId != "AURORA" || frame.Command != "GETINPUT")
                return;

            var payload = frame.Arguments.FirstOrDefault() ?? "";

            _dispatcherQueue.TryEnqueue(() => UpdateInputStates(payload));
        }

        private void UpdateInputStates(string payload)
        {
            for (int i = 0; i < InputCount; i++)
            {
                bool isHigh = i < payload.Length && payload[i] == '1';

                _indicators[i].Fill = new SolidColorBrush(
                    isHigh ? Microsoft.UI.Colors.LimeGreen : Microsoft.UI.Colors.Gray);

                _stateLabels[i].Text = isHigh ? "ON" : "OFF";
            }
        }

        private void PopulateActiveScene(AuroraProject project)
        {
            var autoScene = project.Config.Scenes
                .FirstOrDefault(s => ((AuroraSceneFlags)s.Flags).HasFlag(AuroraSceneFlags.AutoStart));

            if (autoScene is null)
            {
                ActiveSceneStatus.Text = "Aucune scène en auto-start";
                return;
            }

            ActiveSceneStatus.Visibility = Visibility.Collapsed;

            var mode = (AuroraLedMode)autoScene.Mode;

            ActiveScenePanel.Children.Add(new TextBlock
            {
                Text = $"Scène {autoScene.SceneId} — {AuroraDisplayNames.GetLedModeName(mode)}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            if (mode != AuroraLedMode.Off)
            {
                var colorPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10
                };

                var preview = new Border
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(
                        Windows.UI.Color.FromArgb(255, autoScene.Red, autoScene.Green, autoScene.Blue)),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var colorText = new TextBlock
                {
                    Text = $"R:{autoScene.Red}  G:{autoScene.Green}  B:{autoScene.Blue}",
                    Foreground = Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var brush)
                        ? (Brush)brush
                        : new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    VerticalAlignment = VerticalAlignment.Center
                };

                colorPanel.Children.Add(preview);
                colorPanel.Children.Add(colorText);
                ActiveScenePanel.Children.Add(colorPanel);
            }
        }
    }
}
