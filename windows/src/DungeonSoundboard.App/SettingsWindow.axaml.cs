using Avalonia.Controls;
using Avalonia.Interactivity;
using DungeonSoundboard.App.ViewModels;

namespace DungeonSoundboard.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = new SettingsWindowViewModel(viewModel);
    }

    private void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
