using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DungeonSoundboard.App.Controls;
using DungeonSoundboard.App.Services;
using DungeonSoundboard.Core.Models;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonSoundboard.App.Tests;

public sealed class MainWindowInteractionTests
{
    [Theory]
    [InlineData("MainWindow.axaml")]
    [InlineData("SettingsOverlay.axaml")]
    public void TooltipButtonsExposeAutomationNames(string xamlFileName)
    {
        var xaml = File.ReadAllText(FindAppXaml(xamlFileName));
        var buttonsWithTooltips = ButtonOpeningTags(xaml)
            .Where(tag => tag.Contains("ToolTip.Tip=", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(buttonsWithTooltips);
        Assert.DoesNotContain(buttonsWithTooltips, tag => !tag.Contains("AutomationProperties.Name=", StringComparison.Ordinal));
    }

    [Fact]
    public void SettingsOverlayCloseButtonExposesAutomationName()
    {
        var xaml = File.ReadAllText(FindAppXaml("SettingsOverlay.axaml"));
        var doneButton = Assert.Single(ButtonOpeningTags(xaml), tag => tag.Contains("CloseSettingsCommand", StringComparison.Ordinal));

        Assert.Contains("AutomationProperties.Name=", doneButton);
    }

    [Fact]
    public void SidebarPlaylistSplitterResizesWholeMusicAndSfxSections()
    {
        var xaml = File.ReadAllText(FindAppXaml("MainWindow.axaml"));

        Assert.DoesNotContain("RowDefinitions=\"Auto,*,10,Auto,*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SidebarSectionsGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SidebarMusicSectionGridLength", xaml, StringComparison.Ordinal);
        Assert.Contains("ResizeBehavior=\"PreviousAndNext\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding Ui.ResizePlaylistSections}\"", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(600, 500, 120, 120, 10, 370)]
    [InlineData(260, 500, 120, 120, 10, 260)]
    [InlineData(80, 500, 120, 120, 10, 120)]
    public void SectionHeightIsClampedToAvailableWindowSpace(
        double preferred,
        double available,
        double firstMinimum,
        double secondMinimum,
        double splitter,
        double expected)
    {
        Assert.Equal(expected, MainWindow.ClampSectionHeight(preferred, available, firstMinimum, secondMinimum, splitter));
    }

    [Fact]
    public void SidebarPlaylistsExposePersistentQuickLaunchButtons()
    {
        var xaml = File.ReadAllText(FindAppXaml("MainWindow.axaml"));

        Assert.Equal(2, CountOccurrences(xaml, "Classes=\"compact iconButton playlistAction playlistQuickPlay\""));
        Assert.Contains("PlayMusicPlaylistItemCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayEffectPlaylistItemCommand", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 200, 0)]
    [InlineData(50, 200, 0.25)]
    [InlineData(100, 200, 0.5)]
    [InlineData(200, 200, 1)]
    [InlineData(250, 200, 1)]
    public void JumpSliderMapsPointerPositionToAbsoluteValue(double position, double length, double expected)
    {
        Assert.Equal(expected, JumpSlider.ValueFromPosition(0, 1, position, length, isDirectionReversed: false), precision: 6);
    }

    [Fact]
    public void JumpSliderCanReversePointerPosition()
    {
        Assert.Equal(75, JumpSlider.ValueFromPosition(0, 100, 25, 100, isDirectionReversed: true), precision: 6);
    }

    [Fact]
    public void MainWindowHotkeysUseTunnelRoutingBeforeFocusedButtons()
    {
        Assert.Equal(RoutingStrategies.Tunnel, MainWindow.HotkeyRoutingStrategy);
    }

    [Fact]
    public void SettingsUsesAnOverlayInsideTheMainWindow()
    {
        var xaml = File.ReadAllText(FindAppXaml("MainWindow.axaml"));

        Assert.Contains("<local:SettingsOverlay", xaml, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsSettingsVisible}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsCapturingHotkey}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsHotkeyConflictVisible}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemSettingsExplainLocalDataAndShowProductMetadata()
    {
        var settingsXaml = File.ReadAllText(FindAppXaml("SettingsOverlay.axaml"));
        var project = File.ReadAllText(FindAppXaml("DungeonSoundboard.App.csproj"));

        Assert.Contains("Text=\"{Binding Ui.StorageDescription}\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Ui.ProfileDescription}\"", settingsXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"?\"", settingsXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Ui.ProfileHelp", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Ui.AuthorLabel}\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AppVersionText}\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("<Version>1.2.0</Version>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutAuthorNameIsABoostyLink()
    {
        var settingsXaml = File.ReadAllText(FindAppXaml("SettingsOverlay.axaml"));

        Assert.Contains("Command=\"{Binding OpenAuthorPageCommand}\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AppAuthorName}\"", settingsXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportStatusIsShownInOneToast()
    {
        var mainXaml = File.ReadAllText(FindAppXaml("MainWindow.axaml"));
        Assert.Equal(1, CountOccurrences(mainXaml, "Text=\"{Binding StatusMessage}\""));
    }

    [Theory]
    [InlineData("MainWindow.axaml")]
    [InlineData("SettingsOverlay.axaml")]
    public void AudioAndProgressSlidersUseAbsolutePointerControl(string xamlFileName)
    {
        var xaml = File.ReadAllText(FindAppXaml(xamlFileName));

        Assert.DoesNotContain("<Slider", xaml, StringComparison.Ordinal);
        Assert.Contains("<controls:JumpSlider", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Minimum=\"0.2\" Maximum=\"1\" Value=\"{Binding DuckingAmount}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Minimum=\"0.2\" Maximum=\"1\" Value=\"{Binding Main.DuckingAmount}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TrackPopupBindingsDoNotDependOnWindowVisualAncestors()
    {
        var xaml = File.ReadAllText(FindAppXaml("MainWindow.axaml"));
        var popupFragments = Regex.Matches(
                xaml,
                @"<Border\.ContextMenu>.*?</Border\.ContextMenu>|<Button\.Flyout>.*?</Button\.Flyout>",
                RegexOptions.Singleline)
            .Select(match => match.Value)
            .ToList();

        Assert.NotEmpty(popupFragments);
        Assert.DoesNotContain(popupFragments, fragment => fragment.Contains("AncestorType=Window", StringComparison.Ordinal));
        Assert.Contains("Header=\"{Binding PlayMenuText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding RenameMenuText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding OpenFileLocationMenuText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding VolumeMenuText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding BindHotkeyMenuText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding ClearHotkeyMenuText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"{Binding DeleteMenuText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding NormalVolumeMenuText}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TrackVolumePopupsUseApplicationLevelSemanticStyles()
    {
        var appXaml = File.ReadAllText(FindAppXaml("App.axaml"));
        var mainXaml = File.ReadAllText(FindAppXaml("MainWindow.axaml"));

        Assert.Contains("Style Selector=\"Button.trackVolumeReset\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("Style Selector=\"Border.trackVolumePopup TextBlock\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("ThemeCardTextPrimaryBrush", appXaml, StringComparison.Ordinal);
        Assert.Contains("ThemePanelTextPrimaryBrush", appXaml, StringComparison.Ordinal);
        Assert.Equal(4, CountOccurrences(mainXaml, "Classes=\"trackVolumeReset\""));
        Assert.Equal(2, CountOccurrences(mainXaml, "Classes=\"trackVolumePopup\""));
    }

    [Fact]
    public void PlayerBarUsesRequestedTransportAndMixerGeometry()
    {
        var xaml = File.ReadAllText(FindAppXaml("MainWindow.axaml"));

        Assert.Contains("<Setter Property=\"Width\" Value=\"34\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<Style Selector=\"Button.stopSfx\">", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Width\" Value=\"28\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"34\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"220\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"156\" ItemsSource=\"{Binding RepeatModeOptions}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"118\" ItemsSource=\"{Binding RepeatModeOptions}\"", xaml, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(xaml, "Width=\"90\""));
        Assert.Contains("Text=\"{Binding MusicVolumePercentText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding EffectsVolumePercentText}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DeckColumnsAndDensityAreBoundToPreferences()
    {
        var xaml = File.ReadAllText(FindAppXaml("MainWindow.axaml"));

        Assert.Contains("Columns=\"{Binding MusicColumns}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Columns=\"{Binding EffectsColumns}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TrackTilePadding", xaml, StringComparison.Ordinal);
        Assert.Contains("TrackTileMinHeight", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Key.A, KeyModifiers.Control, 0, "A", HotkeyModifier.Control, "Ctrl+A")]
    [InlineData(Key.Z, KeyModifiers.Shift, 25, "Z", HotkeyModifier.Shift, "Shift+Z")]
    [InlineData(Key.D1, KeyModifiers.None, 201, "1", HotkeyModifier.None, "1")]
    [InlineData(Key.NumPad9, KeyModifiers.None, 229, "9", HotkeyModifier.None, "9")]
    public void HotkeyMapperMapsWindowsCharacterKeys(
        Key key,
        KeyModifiers modifiers,
        ushort expectedKeyCode,
        string expectedLabel,
        HotkeyModifier expectedModifier,
        string expectedDisplayText)
    {
        var hotkey = HotkeyMapper.FromKey(key, modifiers);

        Assert.NotNull(hotkey);
        Assert.Equal(expectedKeyCode, hotkey.KeyCode);
        Assert.Equal(expectedLabel, hotkey.Label);
        Assert.Equal(expectedModifier, hotkey.Modifier);
        Assert.Equal(expectedDisplayText, hotkey.DisplayText);
    }

    [Theory]
    [InlineData(Key.Space, HotkeyConfiguration.SpaceKeyCode, "Space")]
    [InlineData(Key.Delete, HotkeyConfiguration.DeleteKeyCode, "Delete")]
    [InlineData(Key.Back, HotkeyConfiguration.DeleteKeyCode, "Delete")]
    [InlineData(Key.Enter, HotkeyConfiguration.ReturnKeyCode, "Return")]
    [InlineData(Key.Escape, HotkeyConfiguration.EscapeKeyCode, "Esc")]
    [InlineData(Key.OemPlus, HotkeyConfiguration.PlusKeyCode, "+")]
    [InlineData(Key.Add, HotkeyConfiguration.PlusKeyCode, "+")]
    [InlineData(Key.OemMinus, HotkeyConfiguration.MinusKeyCode, "-")]
    [InlineData(Key.Subtract, HotkeyConfiguration.MinusKeyCode, "-")]
    public void HotkeyMapperMapsWindowsControlKeys(Key key, ushort expectedKeyCode, string expectedLabel)
    {
        var hotkey = HotkeyMapper.FromKey(key, KeyModifiers.None);

        Assert.NotNull(hotkey);
        Assert.Equal(expectedKeyCode, hotkey.KeyCode);
        Assert.Equal(expectedLabel, hotkey.Label);
        Assert.Equal(HotkeyModifier.None, hotkey.Modifier);
    }

    [Theory]
    [InlineData(Key.F1, 240, "F1")]
    [InlineData(Key.F5, 244, "F5")]
    [InlineData(Key.F12, 251, "F12")]
    public void HotkeyMapperMapsFunctionKeys(Key key, ushort expectedKeyCode, string expectedLabel)
    {
        var hotkey = HotkeyMapper.FromKey(key, KeyModifiers.Shift);

        Assert.NotNull(hotkey);
        Assert.Equal(expectedKeyCode, hotkey.KeyCode);
        Assert.Equal(expectedLabel, hotkey.Label);
        Assert.Equal(HotkeyModifier.Shift, hotkey.Modifier);
        Assert.Equal($"Shift+{expectedLabel}", hotkey.DisplayText);
    }

    [Theory]
    [InlineData(Key.A, KeyModifiers.Alt)]
    [InlineData(Key.A, KeyModifiers.Meta)]
    [InlineData(Key.A, KeyModifiers.Control | KeyModifiers.Shift)]
    public void HotkeyMapperRejectsUnsupportedWindowsModifierCombinations(Key key, KeyModifiers modifiers)
    {
        Assert.Null(HotkeyMapper.FromKey(key, modifiers));
    }

    [Theory]
    [InlineData("tileAction")]
    [InlineData("tileIcon")]
    public void TileActionSourceRecognizesTrackTileActionClasses(string actionClass)
    {
        var button = new Button();
        button.Classes.Add(actionClass);

        Assert.True(MainWindow.IsTileActionSource(button));
    }

    [Fact]
    public void PlaylistActionSourceRecognizesPlaylistActionClass()
    {
        var button = new Button();
        button.Classes.Add("playlistAction");

        Assert.True(MainWindow.IsPlaylistActionSource(button));
    }

    [Fact]
    public void PlaylistDragStartsOnlyFromDragHandle()
    {
        var handle = new PathIcon();
        handle.Classes.Add("playlistDragHandle");
        var action = new Button();
        action.Classes.Add("playlistAction");

        Assert.True(MainWindow.IsPlaylistDragHandleSource(handle));
        Assert.False(MainWindow.IsPlaylistDragHandleSource(action));
        Assert.False(MainWindow.IsPlaylistDragHandleSource(new Border()));
    }

    [Fact]
    public void PlaylistRowsExposeTwoRealDragHandles()
    {
        var xaml = File.ReadAllText(FindAppXaml("MainWindow.axaml"));

        Assert.Equal(2, CountOccurrences(xaml, "Classes=\"playlistDragHandle\""));
        Assert.Equal(2, CountOccurrences(xaml, "Cursor=\"SizeAll\""));
    }

    [Theory]
    [InlineData(KeyModifiers.Control, true)]
    [InlineData(KeyModifiers.Shift, true)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift, true)]
    [InlineData(KeyModifiers.Alt, false)]
    [InlineData(KeyModifiers.None, false)]
    public void ModifiedTrackSelectionSuppressesTilePlayback(KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, MainWindow.ShouldSuppressTilePlayback(modifiers));
    }

    [Theory]
    [InlineData(Key.Delete, KeyModifiers.None, true)]
    [InlineData(Key.Delete, KeyModifiers.Control, false)]
    [InlineData(Key.Delete, KeyModifiers.Shift, false)]
    [InlineData(Key.Back, KeyModifiers.None, false)]
    public void DeleteSelectionKeyOnlyMatchesPlainDelete(Key key, KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, MainWindow.IsDeleteSelectionKey(key, modifiers));
    }

    [Theory]
    [InlineData(Key.A, KeyModifiers.Control, true)]
    [InlineData(Key.A, KeyModifiers.None, false)]
    [InlineData(Key.A, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.Delete, KeyModifiers.Control, false)]
    public void SelectAllSelectionKeyOnlyMatchesControlA(Key key, KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, MainWindow.IsSelectAllSelectionKey(key, modifiers));
    }

    [Fact]
    public void SelectAllListItemsSelectsEveryListItem()
    {
        var listBox = new ListBox
        {
            SelectionMode = SelectionMode.Multiple,
            ItemsSource = new[] { "one", "two", "three" }
        };

        Assert.True(MainWindow.SelectAllListItems(listBox));

        Assert.NotNull(listBox.SelectedItems);
        Assert.Equal(3, listBox.SelectedItems.Count);
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.None, true)]
    [InlineData(Key.Space, KeyModifiers.None, true)]
    [InlineData(Key.Enter, KeyModifiers.Control, false)]
    [InlineData(Key.Space, KeyModifiers.Shift, false)]
    [InlineData(Key.Delete, KeyModifiers.None, false)]
    public void PlaySelectionKeyOnlyMatchesPlainEnterOrSpace(Key key, KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, MainWindow.IsPlaySelectionKey(key, modifiers));
    }

    [Fact]
    public void TryExecutePlaySelectionRunsCommandWithSelectedTrack()
    {
        var command = new TestCommand();
        var selectedTrack = new object();

        Assert.True(MainWindow.TryExecutePlaySelection(Key.Enter, KeyModifiers.None, command, selectedTrack));

        Assert.Same(selectedTrack, command.LastParameter);
        Assert.Equal(1, command.ExecuteCount);
    }

    [Fact]
    public void TryExecutePlaySelectionIgnoresMissingSelection()
    {
        var command = new TestCommand();

        Assert.False(MainWindow.TryExecutePlaySelection(Key.Enter, KeyModifiers.None, command, null));

        Assert.Equal(0, command.ExecuteCount);
    }

    [Theory]
    [InlineData(Key.F2, KeyModifiers.None, true)]
    [InlineData(Key.F2, KeyModifiers.Control, false)]
    [InlineData(Key.Enter, KeyModifiers.None, false)]
    [InlineData(Key.R, KeyModifiers.Control, false)]
    public void RenameSelectionKeyOnlyMatchesPlainF2(Key key, KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, MainWindow.IsRenameSelectionKey(key, modifiers));
    }

    [Fact]
    public void TextInputSourceRecognizesTextBox()
    {
        Assert.True(MainWindow.IsTextInputSource(new TextBox()));
    }

    [Fact]
    public void TextInputSourceRejectsNonTextControls()
    {
        Assert.False(MainWindow.IsTextInputSource(new Button()));
        Assert.False(MainWindow.IsTextInputSource(null));
    }

    [Fact]
    public void TryExecuteRenameSelectionRunsCommandWithSelectedTrack()
    {
        var command = new TestCommand();
        var selectedTrack = new object();

        Assert.True(MainWindow.TryExecuteRenameSelection(Key.F2, KeyModifiers.None, command, selectedTrack));

        Assert.Same(selectedTrack, command.LastParameter);
        Assert.Equal(1, command.ExecuteCount);
    }

    [Fact]
    public void TryExecuteRenameSelectionIgnoresMissingSelection()
    {
        var command = new TestCommand();

        Assert.False(MainWindow.TryExecuteRenameSelection(Key.F2, KeyModifiers.None, command, null));

        Assert.Equal(0, command.ExecuteCount);
    }

    [Theory]
    [InlineData(MainWindow.TrackDropTargetClass)]
    [InlineData(MainWindow.PlaylistDropTargetClass)]
    public void DropTargetClassMovesBetweenItemsAndCanBeCleared(string dropTargetClass)
    {
        var first = new Border();
        var second = new Border();
        Control? activeTarget = null;

        MainWindow.SetDropTargetClass(dropTargetClass, first, ref activeTarget);

        Assert.Same(first, activeTarget);
        Assert.Contains(dropTargetClass, first.Classes);
        Assert.DoesNotContain(dropTargetClass, second.Classes);

        MainWindow.SetDropTargetClass(dropTargetClass, second, ref activeTarget);

        Assert.Same(second, activeTarget);
        Assert.DoesNotContain(dropTargetClass, first.Classes);
        Assert.Contains(dropTargetClass, second.Classes);

        MainWindow.SetDropTargetClass(dropTargetClass, null, ref activeTarget);

        Assert.Null(activeTarget);
        Assert.DoesNotContain(dropTargetClass, first.Classes);
        Assert.DoesNotContain(dropTargetClass, second.Classes);
    }

    private sealed class TestCommand : System.Windows.Input.ICommand
    {
#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public int ExecuteCount { get; private set; }
        public object? LastParameter { get; private set; }

        public bool CanExecute(object? parameter)
        {
            return parameter is not null;
        }

        public void Execute(object? parameter)
        {
            ExecuteCount++;
            LastParameter = parameter;
        }
    }

    private static string FindAppXaml(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "DungeonSoundboard.App",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(
                directory.FullName,
                "windows",
                "src",
                "DungeonSoundboard.App",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName}.");
    }

    private static IReadOnlyList<string> ButtonOpeningTags(string xaml)
    {
        var tags = new List<string>();
        using var reader = new StringReader(xaml);
        string? line;
        List<string>? currentTag = null;

        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.TrimStart();
            if (currentTag is null && !trimmed.StartsWith("<Button", StringComparison.Ordinal))
            {
                continue;
            }

            currentTag ??= [];
            currentTag.Add(line);
            if (!trimmed.Contains('>'))
            {
                continue;
            }

            tags.Add(string.Join('\n', currentTag));
            currentTag = null;
        }

        return tags;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = text.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }
}
