using System.Windows;
using PomodoroApp.ViewModels;

namespace PomodoroApp;

public partial class DashboardWindow : Window
{
    public DashboardWindow()
    {
        InitializeComponent();
        DataContext = new DashboardViewModel();
    }
}
