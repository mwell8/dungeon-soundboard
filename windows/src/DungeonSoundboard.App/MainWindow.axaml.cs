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

    private void OnMusicDragEnter(object? sender, DragEventArgs e)
    {
        SetMusicDropTargetActive(e, HasFiles(e));
    }

    private void OnMusicDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = HasFiles(e);
        SetMusicDropTargetActive(e, hasFiles);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnMusicDragLeave(object? sender, DragEventArgs e)
    {
        SetMusicDropTargetActive(e, false);
    }

    private void OnEffectDragEnter(object? sender, DragEventArgs e)
    {
        SetEffectDropTargetActive(e, HasFiles(e));
    }

    private void OnEffectDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = HasFiles(e);
        SetEffectDropTargetActive(e, hasFiles);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnEffectDragLeave(object? sender, DragEventArgs e)
    {
        SetEffectDropTargetActive(e, false);
    }

    private void OnMusicDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetMusicDropTargetActive(false);
            viewModel.ImportDroppedMusic(GetDroppedPaths(e));
        }

        e.Handled = true;
    }

    private void OnEffectDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetEffectDropTargetActive(false);
            viewModel.ImportDroppedEffects(GetDroppedPaths(e));
        }

        e.Handled = true;
    }

    private void OnMusicTrackTapped(object? sender, TappedEventArgs e)
    {
        if (IsTileActionSource(e.Source))
        {
            e.Handled = true;
            return;
        }

        ExecuteTrackTileCommand(sender, (viewModel, tile) => viewModel.PlayMusicTrackTileCommand.Execute(tile));
        e.Handled = true;
    }

    private void OnEffectTrackTapped(object? sender, TappedEventArgs e)
    {
        if (IsTileActionSource(e.Source))
        {
            e.Handled = true;
            return;
        }

        ExecuteTrackTileCommand(sender, (viewModel, tile) => viewModel.PlayEffectTrackTileCommand.Execute(tile));
        e.Handled = true;
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

    private void ExecuteTrackTileCommand(object? sender, Action<MainWindowViewModel, TrackTileViewModel> execute)
    {
        if (DataContext is MainWindowViewModel viewModel && TrackTileFromSender(sender) is { } tile)
        {
            execute(viewModel, tile);
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

    private static TrackTileViewModel? TrackTileFromSender(object? sender)
    {
        return sender is Control control ? control.DataContext as TrackTileViewModel : null;
    }

    internal static bool IsTileActionSource(object? source)
    {
        var current = source as Control;
        while (current is not null)
        {
            if (current.Classes.Contains("tileAction") || current.Classes.Contains("tileIcon"))
            {
                return true;
            }

            current = current.Parent as Control;
        }

        return false;
    }

    private static bool HasFiles(DragEventArgs e)
    {
        return e.DataTransfer.Contains(DataFormat.File);
    }

    private void SetMusicDropTargetActive(DragEventArgs e, bool isActive)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetMusicDropTargetActive(isActive);
        }

        e.Handled = true;
    }

    private void SetEffectDropTargetActive(DragEventArgs e, bool isActive)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetEffectDropTargetActive(isActive);
        }

        e.Handled = true;
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
