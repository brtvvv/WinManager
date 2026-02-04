using System.Windows;
using WinManager.ViewModels;

namespace WinManager;

public partial class MainWindow : Window
{
    // On load, initialize Programs VM so statuses are fetched before user actions.
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.Programs.InitializeAsync();
        }
    }
}

