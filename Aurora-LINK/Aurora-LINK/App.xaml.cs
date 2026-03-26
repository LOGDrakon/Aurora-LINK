using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Aurora_LINK
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public static MainWindow? MainWindow { get; private set; }

        public App()
        {
            InitializeComponent();
            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            WriteCrashLog(e.Exception);
        }

        private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            WriteCrashLog(e.ExceptionObject as Exception);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteCrashLog(e.Exception);
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                var window = new MainWindow();
                MainWindow = window;
                _window = window;
                _window.Activate();
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex);
                ShowFatalError(ex.Message);
            }
        }

        private static void WriteCrashLog(Exception? ex)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Aurora-LINK",
                    "crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path,
                    $"[{DateTime.Now:O}] {ex}\n---\n");
            }
            catch
            {
                // Cannot write log – ignore to avoid secondary crash.
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

        private static void ShowFatalError(string message)
        {
            MessageBox(0, message, "Aurora-LINK – Erreur fatale", 0x10 /* MB_ICONERROR */);
        }
    }
}
