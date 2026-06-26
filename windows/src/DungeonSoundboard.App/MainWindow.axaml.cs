using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DungeonSoundboard.App.Services;
using DungeonSoundboard.App.ViewModels;
using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel();
        viewModel.FileDialogService = new AvaloniaFileDialogService(this);
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.Source is TextBox && viewModel.CaptureAction is null)
        {
            return;
        }

        var hotkey = HotkeyMapper.FromKeyEvent(e);
        if (hotkey is not null && viewModel.HandleHotkey(hotkey))
        {
            e.Handled = true;
        }
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var settingsWindow = new SettingsWindow(viewModel);
        await settingsWindow.ShowDialog(this);
    }

    private void OnMusicDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnEffectDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnMusicDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ImportDroppedMusic(GetDroppedPaths(e));
        }

        e.Handled = true;
    }

    private void OnEffectDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ImportDroppedEffects(GetDroppedPaths(e));
        }

        e.Handled = true;
    }

    private void OnMusicTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.SelectedMusicTrack is not null)
        {
            viewModel.PlayMusicTrackCommand.Execute(viewModel.SelectedMusicTrack);
        }
    }

    private void OnEffectTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.SelectedEffectTrack is not null)
        {
            viewModel.PlayEffectCommand.Execute(viewModel.SelectedEffectTrack);
        }
    }

    private static bool HasFiles(DragEventArgs e)
    {
        return e.DataTransfer.Contains(DataFormat.File);
    }

    private static IEnumerable<string> GetDroppedPaths(DragEventArgs e)
    {
        return e.DataTransfer.TryGetFiles()?
            .Select(item => item.Path)
            .Where(uri => uri.IsAbsoluteUri)
            .Select(uri => uri.LocalPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            ?? [];
    }
}
