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

    private void OnPlayMusicTrackMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.PlayMusicTrackCommand.Execute(track));
    }

    private void OnBindMusicTrackMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.BindMusicTrackItemCommand.Execute(track));
    }

    private void OnClearMusicTrackHotkeyMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.ClearMusicTrackItemBindingCommand.Execute(track));
    }

    private void OnMoveMusicTrackUpMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.MoveMusicTrackItemUpCommand.Execute(track));
    }

    private void OnMoveMusicTrackDownMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.MoveMusicTrackItemDownCommand.Execute(track));
    }

    private void OnDeleteMusicTrackMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.DeleteMusicTrackItemCommand.Execute(track));
    }

    private void OnPlayEffectTrackMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.PlayEffectCommand.Execute(track));
    }

    private void OnBindEffectTrackMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.BindEffectTrackItemCommand.Execute(track));
    }

    private void OnClearEffectTrackHotkeyMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.ClearEffectTrackItemBindingCommand.Execute(track));
    }

    private void OnMoveEffectTrackUpMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.MoveEffectTrackItemUpCommand.Execute(track));
    }

    private void OnMoveEffectTrackDownMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.MoveEffectTrackItemDownCommand.Execute(track));
    }

    private void OnDeleteEffectTrackMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.DeleteEffectTrackItemCommand.Execute(track));
    }

    private void ExecuteTrackCommand(object? sender, Action<MainWindowViewModel, Track> execute)
    {
        if (DataContext is MainWindowViewModel viewModel && TrackFromSender(sender) is { } track)
        {
            execute(viewModel, track);
        }
    }

    private static Track? TrackFromSender(object? sender)
    {
        if (sender is not Control control)
        {
            return null;
        }

        return control.Tag as Track
            ?? (control.DataContext as TrackTileViewModel)?.Track
            ?? control.DataContext as Track;
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
