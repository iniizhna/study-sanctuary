using System.Windows;
using PomodoroApp.ViewModels;

namespace PomodoroApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
