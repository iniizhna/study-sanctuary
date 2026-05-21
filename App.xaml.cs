using System.Windows;

namespace PomodoroApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global handler for uncaught exceptions on the UI thread.
        // Keeps the app from silently dying; logs and shows a friendly error.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            args.Handled = true; // don't crash; let the user keep the session data
        };
    }
}
