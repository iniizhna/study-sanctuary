using System.Windows;
using PomodoroApp.ViewModels;

namespace PomodoroApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainWindowViewModel();
        DataContext = vm;

        // Let the ViewModel signal when it wants the window chrome changed.
        vm.FullScreenToggled += isFullScreen =>
        {
            if (isFullScreen)
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
            }
        };
    }
}
