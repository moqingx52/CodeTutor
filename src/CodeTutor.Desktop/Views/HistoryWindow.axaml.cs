using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CodeTutor.Desktop.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow() => InitializeComponent();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnLoadClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.HistoryWindowViewModel vm)
        {
            await vm.LoadSelectedAsync();
            Close(true);
        }
    }

    private async void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ViewModels.HistoryWindowViewModel vm)
        {
            await vm.LoadSelectedAsync();
            Close(true);
        }
    }
}
