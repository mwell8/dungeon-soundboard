using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DungeonSoundboard.App.Services;
using DungeonSoundboard.App.ViewModels;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using System.ComponentModel;
using System.Windows.Input;

namespace DungeonSoundboard.App;

public partial class MainWindow : Window
{
    internal const string TrackDropTargetClass = "trackDropTarget";
    internal const string PlaylistDropTargetClass = "playlistDropTarget";
    internal const RoutingStrategies HotkeyRoutingStrategy = RoutingStrategies.Tunnel;

    private const double TrackDragThreshold = 6;
    private static readonly DataFormat<string> MusicTrackDragFormat = DataFormat.CreateInProcessFormat<string>("dungeon-soundboard.music-track");
    private static readonly DataFormat<string> EffectTrackDragFormat = DataFormat.CreateInProcessFormat<string>("dungeon-soundboard.effect-track");
    private static readonly DataFormat<string> MusicPlaylistDragFormat = DataFormat.CreateInProcessFormat<string>("dungeon-soundboard.music-playlist");
    private static readonly DataFormat<string> EffectPlaylistDragFormat = DataFormat.CreateInProcessFormat<string>("dungeon-soundboard.effect-playlist");

    private PointerPressedEventArgs? _pendingTrackDragEvent;
    private Point? _pendingTrackDragStart;
    private Track? _pendingTrackDragTrack;
    private TrackRole? _pendingTrackDragRole;
    private TrackDragPayload? _activeTrackDrag;
    private Control? _activeTrackDropTarget;
    private bool _isTrackDragInProgress;
    private bool _suppressNextMusicTrackTap;
    private bool _suppressNextEffectTrackTap;
    private PointerPressedEventArgs? _pendingPlaylistDragEvent;
    private Point? _pendingPlaylistDragStart;
    private Playlist? _pendingMusicPlaylistDrag;
    private EffectPlaylist? _pendingEffectPlaylistDrag;
    private TrackRole? _pendingPlaylistDragRole;
    private Playlist? _draggedMusicPlaylist;
    private EffectPlaylist? _draggedEffectPlaylist;
    private Control? _activePlaylistDropTarget;
    private bool _isPlaylistDragInProgress;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, HotkeyRoutingStrategy, handledEventsToo: true);
        var viewModel = new MainWindowViewModel();
        viewModel.FileDialogService = new AvaloniaFileDialogService(this);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = viewModel;
        ApplyThemeResources(viewModel);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsPlaylistRenameVisible) && viewModel.IsPlaylistRenameVisible)
        {
            FocusRenameTextBox(PlaylistRenameTextBox);
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.Ui) ||
            e.PropertyName?.EndsWith("Brush", StringComparison.Ordinal) == true)
        {
            ApplyThemeResources(viewModel);
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsTrackRenameVisible) && viewModel.IsTrackRenameVisible)
        {
            FocusRenameTextBox(TrackRenameTextBox);
        }
    }

    private void ApplyThemeResources(MainWindowViewModel viewModel)
    {
        ApplyFluentAccentResources(viewModel.AccentBrush);
        if (Application.Current is not { } application)
        {
            return;
        }

        var resources = application.Resources;
        resources["ThemePanelBrush"] = viewModel.PanelBrush;
        resources["ThemePanelAltBrush"] = viewModel.PanelAltBrush;
        resources["ThemeCardBrush"] = viewModel.CardBrush;
        resources["ThemeCurrentCardBrush"] = viewModel.CurrentCardBrush;
        resources["ThemeAccentBrush"] = viewModel.AccentBrush;
        resources["ThemeDividerBrush"] = viewModel.DividerBrush;
        resources["ThemePanelTextPrimaryBrush"] = viewModel.TextPrimaryBrush;
        resources["ThemePanelTextSecondaryBrush"] = viewModel.TextSecondaryBrush;
        resources["ThemePanelAltTextPrimaryBrush"] = viewModel.PanelAltTextPrimaryBrush;
        resources["ThemePanelAltTextSecondaryBrush"] = viewModel.PanelAltTextSecondaryBrush;
        resources["ThemeCardTextPrimaryBrush"] = viewModel.CardTextPrimaryBrush;
        resources["ThemeCardTextSecondaryBrush"] = viewModel.CardTextSecondaryBrush;
        resources["ThemeCurrentCardTextPrimaryBrush"] = viewModel.CurrentCardTextPrimaryBrush;
        resources["ThemeCurrentCardTextSecondaryBrush"] = viewModel.CurrentCardTextSecondaryBrush;
        resources["ThemeAccentTextPrimaryBrush"] = viewModel.AccentTextPrimaryBrush;
        resources["ThemeAccentTextSecondaryBrush"] = viewModel.AccentTextSecondaryBrush;
        resources["OnAccentBrush"] = viewModel.AccentTextPrimaryBrush;
        resources["MenuShufflePlayText"] = viewModel.Ui.ShufflePlay;
        resources["MenuRenameText"] = viewModel.Ui.Rename;
        resources["MenuDeleteText"] = viewModel.Ui.Delete;
    }

    private void ApplyFluentAccentResources(IBrush brush)
    {
        if (brush is not SolidColorBrush solid)
        {
            return;
        }

        var accent = solid.Color;
        ApplyAccentPalette(Resources, accent);
        if (Application.Current is { } application)
        {
            ApplyAccentPalette(application.Resources, accent);
        }
    }

    private static void ApplyAccentPalette(IResourceDictionary resources, Color accent)
    {
        resources["SystemAccentColor"] = accent;
        resources["SystemAccentColorDark1"] = Shade(accent, 0.86);
        resources["SystemAccentColorDark2"] = Shade(accent, 0.70);
        resources["SystemAccentColorDark3"] = Shade(accent, 0.54);
        resources["SystemAccentColorLight1"] = Blend(accent, Colors.White, 0.18);
        resources["SystemAccentColorLight2"] = Blend(accent, Colors.White, 0.32);
        resources["SystemAccentColorLight3"] = Blend(accent, Colors.White, 0.48);
    }

    private static Color Shade(Color color, double factor)
    {
        byte Adjust(byte channel) => (byte)Math.Clamp(Math.Round(channel * factor), 0, 255);
        return Color.FromArgb(color.A, Adjust(color.R), Adjust(color.G), Adjust(color.B));
    }

    private static Color Blend(Color source, Color target, double amount)
    {
        byte Mix(byte left, byte right) => (byte)Math.Clamp(Math.Round(left + (right - left) * amount), 0, 255);
        return Color.FromArgb(source.A, Mix(source.R, target.R), Mix(source.G, target.G), Mix(source.B, target.B));
    }

    private static void FocusRenameTextBox(TextBox textBox)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!textBox.IsVisible)
            {
                return;
            }

            textBox.Focus();
            textBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var hotkey = HotkeyMapper.FromKeyEvent(e);
        if (hotkey is not null && viewModel.HandleDialogHotkey(hotkey))
        {
            e.Handled = true;
            return;
        }

        if (IsTextInputSource(e.Source) && viewModel.CaptureAction is null)
        {
            return;
        }

        if (hotkey is not null && viewModel.HandleHotkey(hotkey))
        {
            e.Handled = true;
        }
    }

    private void OnMusicTrackListKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleSelectAllSelectionKey(sender, e))
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel
            && HandleRenameSelectionKey(e, viewModel.RequestRenameMusicTrackItemCommand, viewModel.SelectedMusicTrack))
        {
            return;
        }

        if (DataContext is MainWindowViewModel playViewModel
            && HandlePlaySelectionKey(e, playViewModel.PlayMusicTrackCommand, playViewModel.SelectedMusicTrack))
        {
            return;
        }

        HandleDeleteSelectionKey(e, viewModel => viewModel.RequestDeleteMusicTrackCommand);
    }

    private void OnEffectTrackListKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleSelectAllSelectionKey(sender, e))
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel
            && HandleRenameSelectionKey(e, viewModel.RequestRenameEffectTrackItemCommand, viewModel.SelectedEffectTrack))
        {
            return;
        }

        if (DataContext is MainWindowViewModel playViewModel
            && HandlePlaySelectionKey(e, playViewModel.PlayEffectCommand, playViewModel.SelectedEffectTrack))
        {
            return;
        }

        HandleDeleteSelectionKey(e, viewModel => viewModel.RequestDeleteEffectTrackCommand);
    }

    private static bool HandleRenameSelectionKey(KeyEventArgs e, ICommand command, object? parameter)
    {
        if (!TryExecuteRenameSelection(e.Key, e.KeyModifiers, command, parameter))
        {
            return false;
        }

        e.Handled = true;
        return true;
    }

    private static bool HandlePlaySelectionKey(KeyEventArgs e, ICommand command, object? parameter)
    {
        if (!TryExecutePlaySelection(e.Key, e.KeyModifiers, command, parameter))
        {
            return false;
        }

        e.Handled = true;
        return true;
    }

    private static bool HandleSelectAllSelectionKey(object? sender, KeyEventArgs e)
    {
        if (!IsSelectAllSelectionKey(e.Key, e.KeyModifiers) || sender is not ListBox listBox)
        {
            return false;
        }

        if (!SelectAllListItems(listBox))
        {
            return false;
        }

        e.Handled = true;
        return true;
    }

    private void HandleDeleteSelectionKey(KeyEventArgs e, Func<MainWindowViewModel, ICommand> commandFactory)
    {
        if (!IsDeleteSelectionKey(e.Key, e.KeyModifiers) || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var command = commandFactory(viewModel);
        if (!command.CanExecute(null))
        {
            return;
        }

        command.Execute(null);
        e.Handled = true;
    }

    internal static bool IsDeleteSelectionKey(Key key, KeyModifiers modifiers)
    {
        return key == Key.Delete && modifiers == KeyModifiers.None;
    }

    internal static bool IsSelectAllSelectionKey(Key key, KeyModifiers modifiers)
    {
        return key == Key.A && modifiers == KeyModifiers.Control;
    }

    internal static bool IsPlaySelectionKey(Key key, KeyModifiers modifiers)
    {
        return (key == Key.Enter || key == Key.Space) && modifiers == KeyModifiers.None;
    }

    internal static bool IsRenameSelectionKey(Key key, KeyModifiers modifiers)
    {
        return key == Key.F2 && modifiers == KeyModifiers.None;
    }

    internal static bool IsTextInputSource(object? source)
    {
        var current = source as Control;
        while (current is not null)
        {
            if (current is TextBox)
            {
                return true;
            }

            current = current.Parent as Control;
        }

        return false;
    }

    internal static bool TryExecutePlaySelection(Key key, KeyModifiers modifiers, ICommand command, object? parameter)
    {
        if (!IsPlaySelectionKey(key, modifiers) || parameter is null || !command.CanExecute(parameter))
        {
            return false;
        }

        command.Execute(parameter);
        return true;
    }

    internal static bool TryExecuteRenameSelection(Key key, KeyModifiers modifiers, ICommand command, object? parameter)
    {
        if (!IsRenameSelectionKey(key, modifiers) || parameter is null || !command.CanExecute(parameter))
        {
            return false;
        }

        command.Execute(parameter);
        return true;
    }

    internal static bool SelectAllListItems(ListBox listBox)
    {
        listBox.SelectAll();
        return listBox.SelectedItems?.Count > 0;
    }

    private void OnMusicDragEnter(object? sender, DragEventArgs e)
    {
        SetMusicDropTargetActive(e, HasFiles(e) || IsTrackDrag(e, TrackRole.Music));
    }

    private void OnSectionSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveLayoutSplitHeights(
                SidebarSectionsGrid.RowDefinitions[0].ActualHeight,
                DeckSectionsGrid.RowDefinitions[0].ActualHeight);
        }
    }

    private void OnSectionGridSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || sender is not Grid grid)
        {
            return;
        }

        if (ReferenceEquals(grid, SidebarSectionsGrid))
        {
            ApplyClampedSectionHeight(grid, viewModel.SidebarMusicSectionGridLength.Value, 96, 96, 10);
        }
        else if (ReferenceEquals(grid, DeckSectionsGrid))
        {
            ApplyClampedSectionHeight(grid, viewModel.CenterMusicSectionGridLength.Value, 120, 120, 10);
        }
    }

    private static void ApplyClampedSectionHeight(Grid grid, double preferredHeight, double firstMinimum, double secondMinimum, double splitterHeight)
    {
        if (grid.RowDefinitions.Count < 3)
        {
            return;
        }

        grid.RowDefinitions[0].Height = new GridLength(ClampSectionHeight(
            preferredHeight,
            grid.Bounds.Height,
            firstMinimum,
            secondMinimum,
            splitterHeight));
    }

    internal static double ClampSectionHeight(double preferredHeight, double availableHeight, double firstMinimum, double secondMinimum, double splitterHeight)
    {
        var normalizedPreferred = double.IsFinite(preferredHeight) ? preferredHeight : firstMinimum;
        var maximum = Math.Max(firstMinimum, availableHeight - secondMinimum - splitterHeight);
        return Math.Clamp(normalizedPreferred, firstMinimum, maximum);
    }

    private void OnMusicDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = HasFiles(e);
        var hasTracks = IsTrackDrag(e, TrackRole.Music);
        SetMusicDropTargetActive(e, hasFiles || hasTracks);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : hasTracks ? TrackDragEffect(e) : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnMusicDragLeave(object? sender, DragEventArgs e)
    {
        SetMusicDropTargetActive(e, false);
    }

    private void OnEffectDragEnter(object? sender, DragEventArgs e)
    {
        SetEffectDropTargetActive(e, HasFiles(e) || IsTrackDrag(e, TrackRole.Effect));
    }

    private void OnEffectDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = HasFiles(e);
        var hasTracks = IsTrackDrag(e, TrackRole.Effect);
        SetEffectDropTargetActive(e, hasFiles || hasTracks);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : hasTracks ? TrackDragEffect(e) : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnEffectDragLeave(object? sender, DragEventArgs e)
    {
        SetEffectDropTargetActive(e, false);
    }

    private async void OnMusicDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetMusicDropTargetActive(false);
            if (IsTrackDrag(e, TrackRole.Music) && viewModel.SelectedMusicPlaylist is { } destination)
            {
                CompleteTrackTransfer(viewModel, destination.Id, null, e, TrackRole.Music);
            }
            else
            {
                await viewModel.ImportDroppedMusicAsync(GetDroppedPaths(e));
            }
        }

        e.Handled = true;
    }

    private async void OnEffectDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetEffectDropTargetActive(false);
            if (IsTrackDrag(e, TrackRole.Effect) && viewModel.SelectedEffectPlaylist is { } destination)
            {
                CompleteTrackTransfer(viewModel, destination.Id, null, e, TrackRole.Effect);
            }
            else
            {
                await viewModel.ImportDroppedEffectsAsync(GetDroppedPaths(e));
            }
        }

        e.Handled = true;
    }

    private void OnMusicTrackTapped(object? sender, TappedEventArgs e)
    {
        if (IsTileActionSource(e.Source) || ConsumeSuppressedTrackTap(ref _suppressNextMusicTrackTap))
        {
            e.Handled = true;
            return;
        }

        ExecuteTrackTileCommand(sender, (viewModel, tile) => viewModel.PlayMusicTrackTileCommand.Execute(tile));
        e.Handled = true;
    }

    private void OnEffectTrackTapped(object? sender, TappedEventArgs e)
    {
        if (IsTileActionSource(e.Source) || ConsumeSuppressedTrackTap(ref _suppressNextEffectTrackTap))
        {
            e.Handled = true;
            return;
        }

        ExecuteTrackTileCommand(sender, (viewModel, tile) => viewModel.PlayEffectTrackTileCommand.Execute(tile));
        e.Handled = true;
    }

    private void OnMusicTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedMusicTrackTiles(listBox.SelectedItems?.OfType<TrackTileViewModel>());
        }
    }

    private void OnEffectTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedEffectTrackTiles(listBox.SelectedItems?.OfType<TrackTileViewModel>());
        }
    }

    private void OnMusicPlaylistPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginPlaylistPointerDrag(sender, e, TrackRole.Music);
    }

    private void OnEffectPlaylistPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginPlaylistPointerDrag(sender, e, TrackRole.Effect);
    }

    private async void OnMusicPlaylistPointerMoved(object? sender, PointerEventArgs e)
    {
        await TryStartPlaylistDragAsync(e, TrackRole.Music);
    }

    private async void OnEffectPlaylistPointerMoved(object? sender, PointerEventArgs e)
    {
        await TryStartPlaylistDragAsync(e, TrackRole.Effect);
    }

    private void OnPlaylistPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPlaylistDragInProgress)
        {
            ClearPendingPlaylistDrag();
        }
    }

    private void OnMusicPlaylistDragEnter(object? sender, DragEventArgs e)
    {
        HandlePlaylistDragTarget(sender, e, TrackRole.Music);
    }

    private void OnMusicPlaylistDragOver(object? sender, DragEventArgs e)
    {
        HandlePlaylistDragTarget(sender, e, TrackRole.Music);
    }

    private void OnMusicPlaylistDrop(object? sender, DragEventArgs e)
    {
        CompletePlaylistDrop(sender, e, TrackRole.Music);
    }

    private void OnEffectPlaylistDragEnter(object? sender, DragEventArgs e)
    {
        HandlePlaylistDragTarget(sender, e, TrackRole.Effect);
    }

    private void OnEffectPlaylistDragOver(object? sender, DragEventArgs e)
    {
        HandlePlaylistDragTarget(sender, e, TrackRole.Effect);
    }

    private void OnEffectPlaylistDrop(object? sender, DragEventArgs e)
    {
        CompletePlaylistDrop(sender, e, TrackRole.Effect);
    }

    private void OnMusicTrackPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _suppressNextMusicTrackTap = !IsTileActionSource(e.Source) && TrackFromSender(sender) is not null && ShouldSuppressTilePlayback(e.KeyModifiers);
        BeginTrackPointerDrag(sender, e, TrackRole.Music);
    }

    private void OnEffectTrackPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _suppressNextEffectTrackTap = !IsTileActionSource(e.Source) && TrackFromSender(sender) is not null && ShouldSuppressTilePlayback(e.KeyModifiers);
        BeginTrackPointerDrag(sender, e, TrackRole.Effect);
    }

    private async void OnMusicTrackPointerMoved(object? sender, PointerEventArgs e)
    {
        await TryStartTrackDragAsync(e, TrackRole.Music);
    }

    private async void OnEffectTrackPointerMoved(object? sender, PointerEventArgs e)
    {
        await TryStartTrackDragAsync(e, TrackRole.Effect);
    }

    private void OnTrackPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isTrackDragInProgress)
        {
            ClearPendingTrackDrag();
        }
    }

    private void OnMusicTrackDragEnter(object? sender, DragEventArgs e)
    {
        HandleTrackDragTarget(sender, e, TrackRole.Music);
    }

    private void OnMusicTrackDragOver(object? sender, DragEventArgs e)
    {
        HandleTrackDragTarget(sender, e, TrackRole.Music);
    }

    private void OnMusicTrackDrop(object? sender, DragEventArgs e)
    {
        CompleteTrackDrop(sender, e, TrackRole.Music);
    }

    private void OnEffectTrackDragEnter(object? sender, DragEventArgs e)
    {
        HandleTrackDragTarget(sender, e, TrackRole.Effect);
    }

    private void OnEffectTrackDragOver(object? sender, DragEventArgs e)
    {
        HandleTrackDragTarget(sender, e, TrackRole.Effect);
    }

    private void OnEffectTrackDrop(object? sender, DragEventArgs e)
    {
        CompleteTrackDrop(sender, e, TrackRole.Effect);
    }

    private void OnPlayMusicTrackMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.PlayMusicTrackCommand.Execute(track));
    }

    private void OnRenameMusicTrackMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.RequestRenameMusicTrackItemCommand.Execute(track));
    }

    private void OnOpenTrackLocationMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.OpenTrackLocationCommand.Execute(track));
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
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.RequestDeleteMusicTrackItemCommand.Execute(track));
    }

    private void OnPlayEffectTrackMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.PlayEffectCommand.Execute(track));
    }

    private void OnRenameEffectTrackMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.RequestRenameEffectTrackItemCommand.Execute(track));
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
        ExecuteTrackCommand(sender, (viewModel, track) => viewModel.RequestDeleteEffectTrackItemCommand.Execute(track));
    }

    private void OnPlayMusicPlaylistMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteMusicPlaylistCommand(sender, (viewModel, playlist) => viewModel.PlayMusicPlaylistItemCommand.Execute(playlist));
    }

    private void OnPlayEffectPlaylistMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteEffectPlaylistCommand(sender, (viewModel, playlist) => viewModel.PlayEffectPlaylistItemCommand.Execute(playlist));
    }

    private void OnRenameMusicPlaylistMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteMusicPlaylistCommand(sender, (viewModel, playlist) => viewModel.RequestRenameMusicPlaylistItemCommand.Execute(playlist));
    }

    private void OnDeleteMusicPlaylistMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteMusicPlaylistCommand(sender, (viewModel, playlist) => viewModel.RequestDeleteMusicPlaylistItemCommand.Execute(playlist));
    }

    private void OnRenameEffectPlaylistMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteEffectPlaylistCommand(sender, (viewModel, playlist) => viewModel.RequestRenameEffectPlaylistItemCommand.Execute(playlist));
    }

    private void OnDeleteEffectPlaylistMenuClick(object? sender, RoutedEventArgs e)
    {
        ExecuteEffectPlaylistCommand(sender, (viewModel, playlist) => viewModel.RequestDeleteEffectPlaylistItemCommand.Execute(playlist));
    }

    private void ExecuteMusicPlaylistCommand(object? sender, Action<MainWindowViewModel, Playlist> execute)
    {
        if (DataContext is MainWindowViewModel viewModel && MusicPlaylistFromSender(sender) is { } playlist)
        {
            execute(viewModel, playlist);
        }
    }

    private void ExecuteEffectPlaylistCommand(object? sender, Action<MainWindowViewModel, EffectPlaylist> execute)
    {
        if (DataContext is MainWindowViewModel viewModel && EffectPlaylistFromSender(sender) is { } playlist)
        {
            execute(viewModel, playlist);
        }
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

    private static Playlist? MusicPlaylistFromSender(object? sender)
    {
        return sender is Control control ? control.DataContext as Playlist : null;
    }

    private static EffectPlaylist? EffectPlaylistFromSender(object? sender)
    {
        return sender is Control control ? control.DataContext as EffectPlaylist : null;
    }

    private void BeginPlaylistPointerDrag(object? sender, PointerPressedEventArgs e, TrackRole role)
    {
        if (!IsPlaylistDragHandleSource(e.Source))
        {
            ClearPendingPlaylistDrag();
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            ClearPendingPlaylistDrag();
            return;
        }

        var musicPlaylist = role == TrackRole.Music ? MusicPlaylistFromSender(sender) : null;
        var effectPlaylist = role == TrackRole.Effect ? EffectPlaylistFromSender(sender) : null;
        if (musicPlaylist is null && effectPlaylist is null)
        {
            ClearPendingPlaylistDrag();
            return;
        }

        _pendingPlaylistDragEvent = e;
        _pendingPlaylistDragStart = e.GetPosition(this);
        _pendingMusicPlaylistDrag = musicPlaylist;
        _pendingEffectPlaylistDrag = effectPlaylist;
        _pendingPlaylistDragRole = role;
    }

    private async Task TryStartPlaylistDragAsync(PointerEventArgs e, TrackRole role)
    {
        if (_isPlaylistDragInProgress
            || _pendingPlaylistDragEvent is null
            || _pendingPlaylistDragStart is not { } start
            || _pendingPlaylistDragRole != role)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ClearPendingPlaylistDrag();
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < TrackDragThreshold
            && Math.Abs(current.Y - start.Y) < TrackDragThreshold)
        {
            return;
        }

        var startEvent = _pendingPlaylistDragEvent;
        var musicPlaylist = _pendingMusicPlaylistDrag;
        var effectPlaylist = _pendingEffectPlaylistDrag;
        ClearPendingPlaylistDrag();
        await StartPlaylistDragAsync(startEvent, role, musicPlaylist, effectPlaylist);
    }

    private async Task StartPlaylistDragAsync(
        PointerPressedEventArgs startEvent,
        TrackRole role,
        Playlist? musicPlaylist,
        EffectPlaylist? effectPlaylist)
    {
        var playlistId = role == TrackRole.Music ? musicPlaylist?.Id : effectPlaylist?.Id;
        if (playlistId is null)
        {
            return;
        }

        _isPlaylistDragInProgress = true;
        _draggedMusicPlaylist = role == TrackRole.Music ? musicPlaylist : null;
        _draggedEffectPlaylist = role == TrackRole.Effect ? effectPlaylist : null;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(PlaylistDragFormat(role), playlistId.Value.ToString("D")));

        try
        {
            await DragDrop.DoDragDropAsync(startEvent, data, DragDropEffects.Move);
        }
        finally
        {
            ClearPlaylistDragState();
        }
    }

    private void HandlePlaylistDragTarget(object? sender, DragEventArgs e, TrackRole role)
    {
        var acceptsTrack = IsTrackDrag(e, role) && PlaylistIdFromSender(sender, role) is not null;
        var acceptsFiles = HasFiles(e) && PlaylistIdFromSender(sender, role) is not null;
        if (acceptsTrack || acceptsFiles)
        {
            SetActivePlaylistDropTarget(sender as Control);
            e.DragEffects = acceptsFiles ? DragDropEffects.Copy : TrackDragEffect(e);
            e.Handled = true;
            return;
        }

        if (!IsPlaylistDrag(e, role))
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            SetActivePlaylistDropTarget(null);
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (role == TrackRole.Music)
        {
            var target = MusicPlaylistFromSender(sender);
            if (_draggedMusicPlaylist is null || target is null || _draggedMusicPlaylist.Id == target.Id)
            {
                SetActivePlaylistDropTarget(null);
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            SetActivePlaylistDropTarget(sender as Control);
        }
        else
        {
            var target = EffectPlaylistFromSender(sender);
            if (_draggedEffectPlaylist is null || target is null || _draggedEffectPlaylist.Id == target.Id)
            {
                SetActivePlaylistDropTarget(null);
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            SetActivePlaylistDropTarget(sender as Control);
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private async void CompletePlaylistDrop(object? sender, DragEventArgs e, TrackRole role)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var destinationPlaylistId = PlaylistIdFromSender(sender, role);
        if (IsTrackDrag(e, role) && destinationPlaylistId is { } destinationId)
        {
            CompleteTrackTransfer(viewModel, destinationId, null, e, role);
            e.Handled = true;
            SetActivePlaylistDropTarget(null);
            return;
        }

        if (HasFiles(e) && destinationPlaylistId is not null)
        {
            if (role == TrackRole.Music)
            {
                await viewModel.ImportDroppedMusicAsync(GetDroppedPaths(e), MusicPlaylistFromSender(sender));
            }
            else
            {
                await viewModel.ImportDroppedEffectsAsync(GetDroppedPaths(e), EffectPlaylistFromSender(sender));
            }

            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            SetActivePlaylistDropTarget(null);
            return;
        }

        if (!IsPlaylistDrag(e, role))
        {
            return;
        }

        if (role == TrackRole.Music)
        {
            viewModel.MoveMusicPlaylistToTarget(_draggedMusicPlaylist, MusicPlaylistFromSender(sender));
        }
        else
        {
            viewModel.MoveEffectPlaylistToTarget(_draggedEffectPlaylist, EffectPlaylistFromSender(sender));
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
        ClearPlaylistDragState();
    }

    private static bool IsPlaylistDrag(DragEventArgs e, TrackRole role)
    {
        return e.DataTransfer.Contains(PlaylistDragFormat(role));
    }

    private static DataFormat<string> PlaylistDragFormat(TrackRole role)
    {
        return role == TrackRole.Music ? MusicPlaylistDragFormat : EffectPlaylistDragFormat;
    }

    private void BeginTrackPointerDrag(object? sender, PointerPressedEventArgs e, TrackRole role)
    {
        if (IsTileActionSource(e.Source) || TrackFromSender(sender) is not { } track)
        {
            ClearPendingTrackDrag();
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            ClearPendingTrackDrag();
            return;
        }

        _pendingTrackDragEvent = e;
        _pendingTrackDragStart = e.GetPosition(this);
        _pendingTrackDragTrack = track;
        _pendingTrackDragRole = role;
    }

    private async Task TryStartTrackDragAsync(PointerEventArgs e, TrackRole role)
    {
        if (_isTrackDragInProgress
            || _pendingTrackDragEvent is null
            || _pendingTrackDragStart is not { } start
            || _pendingTrackDragTrack is not { } track
            || _pendingTrackDragRole != role)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ClearPendingTrackDrag();
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < TrackDragThreshold
            && Math.Abs(current.Y - start.Y) < TrackDragThreshold)
        {
            return;
        }

        var startEvent = _pendingTrackDragEvent;
        ClearPendingTrackDrag();
        await StartTrackDragAsync(startEvent, track, role);
    }

    private async Task StartTrackDragAsync(PointerPressedEventArgs startEvent, Track track, TrackRole role)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var sourcePlaylistId = role == TrackRole.Music
            ? viewModel.SelectedMusicPlaylist?.Id
            : viewModel.SelectedEffectPlaylist?.Id;
        var trackIds = role == TrackRole.Music
            ? viewModel.MusicTrackIdsForDrag(track)
            : viewModel.EffectTrackIdsForDrag(track);
        if (sourcePlaylistId is null || trackIds.Count == 0)
        {
            return;
        }

        _isTrackDragInProgress = true;
        _activeTrackDrag = new TrackDragPayload(role, sourcePlaylistId.Value, trackIds);

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(TrackDragFormat(role), track.Id.ToString("D")));

        try
        {
            await DragDrop.DoDragDropAsync(startEvent, data, DragDropEffects.Move | DragDropEffects.Copy);
        }
        finally
        {
            ClearTrackDragState();
        }
    }

    private void HandleTrackDragTarget(object? sender, DragEventArgs e, TrackRole role)
    {
        if (!IsTrackDrag(e, role))
        {
            return;
        }

        var targetTrack = TrackFromSender(sender);
        if (_activeTrackDrag is not { } payload
            || payload.Role != role
            || targetTrack is null
            || payload.TrackIds.Contains(targetTrack.Id))
        {
            SetActiveTrackDropTarget(null);
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        SetActiveTrackDropTarget(sender as Control);
        e.DragEffects = TrackDragEffect(e);
        e.Handled = true;
    }

    private void CompleteTrackDrop(object? sender, DragEventArgs e, TrackRole role)
    {
        if (!IsTrackDrag(e, role))
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            var destinationPlaylistId = role == TrackRole.Music
                ? viewModel.SelectedMusicPlaylist?.Id
                : viewModel.SelectedEffectPlaylist?.Id;
            if (destinationPlaylistId is { } destinationId)
            {
                CompleteTrackTransfer(viewModel, destinationId, TrackFromSender(sender)?.Id, e, role);
            }
        }

        e.DragEffects = TrackDragEffect(e);
        e.Handled = true;
        ClearTrackDragState();
    }

    private void CompleteTrackTransfer(
        MainWindowViewModel viewModel,
        Guid destinationPlaylistId,
        Guid? targetTrackId,
        DragEventArgs e,
        TrackRole role)
    {
        if (_activeTrackDrag is not { } payload || payload.Role != role)
        {
            return;
        }

        var mode = e.KeyModifiers.HasFlag(KeyModifiers.Alt)
            ? TrackTransferMode.Copy
            : TrackTransferMode.Move;
        if (role == TrackRole.Music)
        {
            viewModel.TransferMusicTracks(
                payload.SourcePlaylistId,
                destinationPlaylistId,
                payload.TrackIds,
                targetTrackId,
                mode);
        }
        else
        {
            viewModel.TransferEffectTracks(
                payload.SourcePlaylistId,
                destinationPlaylistId,
                payload.TrackIds,
                targetTrackId,
                mode);
        }
    }

    private static bool IsTrackDrag(DragEventArgs e, TrackRole role)
    {
        return e.DataTransfer.Contains(TrackDragFormat(role));
    }

    private static DataFormat<string> TrackDragFormat(TrackRole role)
    {
        return role == TrackRole.Music ? MusicTrackDragFormat : EffectTrackDragFormat;
    }

    private void ClearPendingTrackDrag()
    {
        _pendingTrackDragEvent = null;
        _pendingTrackDragStart = null;
        _pendingTrackDragTrack = null;
        _pendingTrackDragRole = null;
    }

    private void ClearTrackDragState()
    {
        ClearPendingTrackDrag();
        _activeTrackDrag = null;
        SetActiveTrackDropTarget(null);
        _isTrackDragInProgress = false;
    }

    private void ClearPendingPlaylistDrag()
    {
        _pendingPlaylistDragEvent = null;
        _pendingPlaylistDragStart = null;
        _pendingMusicPlaylistDrag = null;
        _pendingEffectPlaylistDrag = null;
        _pendingPlaylistDragRole = null;
    }

    private void ClearPlaylistDragState()
    {
        ClearPendingPlaylistDrag();
        _draggedMusicPlaylist = null;
        _draggedEffectPlaylist = null;
        SetActivePlaylistDropTarget(null);
        _isPlaylistDragInProgress = false;
    }

    private void SetActiveTrackDropTarget(Control? target)
    {
        SetDropTargetClass(TrackDropTargetClass, target, ref _activeTrackDropTarget);
    }

    private void SetActivePlaylistDropTarget(Control? target)
    {
        SetDropTargetClass(PlaylistDropTargetClass, target, ref _activePlaylistDropTarget);
    }

    internal static void SetDropTargetClass(string dropTargetClass, Control? target, ref Control? activeTarget)
    {
        if (ReferenceEquals(activeTarget, target))
        {
            return;
        }

        activeTarget?.Classes.Remove(dropTargetClass);
        activeTarget = target;
        if (activeTarget is not null && !activeTarget.Classes.Contains(dropTargetClass))
        {
            activeTarget.Classes.Add(dropTargetClass);
        }
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

    internal static bool IsPlaylistActionSource(object? source)
    {
        var current = source as Control;
        while (current is not null)
        {
            if (current.Classes.Contains("playlistAction"))
            {
                return true;
            }

            current = current.Parent as Control;
        }

        return false;
    }

    internal static bool IsPlaylistDragHandleSource(object? source)
    {
        var current = source as Control;
        while (current is not null)
        {
            if (current.Classes.Contains("playlistDragHandle"))
            {
                return true;
            }

            current = current.Parent as Control;
        }

        return false;
    }

    internal static bool ShouldSuppressTilePlayback(KeyModifiers modifiers)
    {
        return modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Shift);
    }

    private static bool ConsumeSuppressedTrackTap(ref bool suppressNextTap)
    {
        if (!suppressNextTap)
        {
            return false;
        }

        suppressNextTap = false;
        return true;
    }

    private static bool HasFiles(DragEventArgs e)
    {
        return e.DataTransfer.Contains(DataFormat.File);
    }

    private static DragDropEffects TrackDragEffect(DragEventArgs e)
    {
        return e.KeyModifiers.HasFlag(KeyModifiers.Alt)
            ? DragDropEffects.Copy
            : DragDropEffects.Move;
    }

    private static Guid? PlaylistIdFromSender(object? sender, TrackRole role)
    {
        return role == TrackRole.Music
            ? MusicPlaylistFromSender(sender)?.Id
            : EffectPlaylistFromSender(sender)?.Id;
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

    private sealed record TrackDragPayload(TrackRole Role, Guid SourcePlaylistId, IReadOnlyList<Guid> TrackIds);
}
