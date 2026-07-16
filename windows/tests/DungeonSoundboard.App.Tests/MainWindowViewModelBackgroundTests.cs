using DungeonSoundboard.App.Services;
using DungeonSoundboard.App.ViewModels;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Avalonia.Media;
using Xunit;

namespace DungeonSoundboard.App.Tests;

public sealed class MainWindowViewModelBackgroundTests
{
    [Fact]
    public void BackgroundSettingsClampAndSave()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.BackgroundLayoutMode = BackgroundLayoutMode.Fit;
        viewModel.BackgroundOpacity = 99;
        viewModel.BackgroundDimOverlay = -3;
        viewModel.BackgroundBlurRadius = 99;

        Assert.Equal(BackgroundLayoutMode.Fit, storage.State.Theme.Background.LayoutMode);
        Assert.Equal(1, storage.State.Theme.Background.Opacity);
        Assert.Equal(0.15, storage.State.Theme.Background.DimOverlay);
        Assert.Equal(24, storage.State.Theme.Background.BlurRadius);
        Assert.True(storage.SaveCount >= 4);
    }

    [Fact]
    public void ClearBackgroundImageReturnsToThemeBackground()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.ClearBackgroundImageCommand.Execute(null);

        Assert.False(viewModel.HasBackgroundImage);
        Assert.Equal(BackgroundMode.None, storage.State.Theme.Background.Mode);
        Assert.Null(storage.State.Theme.Background.ImageOriginalPath);
        Assert.Equal("Theme preset background", viewModel.BackgroundImageStatus);
    }

    [Fact]
    public void ThemePresetChangePreservesSelectedBackgroundImage()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.SelectedThemePreset = viewModel.ThemePresetOptions.First(option => option.Preset == ThemePreset.ForestMist);

        Assert.Equal(ThemePreset.ForestMist, storage.State.Theme.Preset);
        Assert.Equal(BackgroundMode.Image, storage.State.Theme.Background.Mode);
        Assert.Equal("C:\\audio\\background.png", storage.State.Theme.Background.ImageOriginalPath);
        Assert.Equal(BackgroundLayoutMode.Fill, storage.State.Theme.Background.LayoutMode);
    }

    [Fact]
    public void ThemePresetOptionsExposeSwatchBrushes()
    {
        using var viewModel = CreateViewModel(StorageWithImageBackground());

        Assert.Equal(
            [ThemePreset.ClassicDungeon, ThemePreset.TavernEmber, ThemePreset.MoonlitCrypt, ThemePreset.ForestMist],
            viewModel.ThemePresetOptions.Select(option => option.Preset).ToArray());

        foreach (var option in viewModel.ThemePresetOptions)
        {
            Assert.IsType<SolidColorBrush>(option.SwatchBackgroundBrush);
            Assert.IsType<SolidColorBrush>(option.SwatchPanelBrush);
            Assert.IsType<SolidColorBrush>(option.SwatchAccentBrush);
        }
    }

    [Fact]
    public void CustomThemePresetCommandsSaveApplyAndDeletePresets()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.PanelOpacity = 0.64;
        viewModel.BackgroundBlurRadius = 8;
        viewModel.CustomThemePresetName = "  Table Night  ";

        Assert.True(viewModel.SaveCurrentThemeAsCustomPresetCommand.CanExecute(null));

        viewModel.SaveCurrentThemeAsCustomPresetCommand.Execute(null);

        var savedPreset = Assert.Single(storage.State.CustomThemePresets);
        Assert.Equal("Table Night", savedPreset.Name);
        Assert.Null(savedPreset.Theme.Preset);
        Assert.Equal(0.64, savedPreset.Theme.Chrome.PanelOpacity);
        Assert.Equal(8, savedPreset.Theme.Background.BlurRadius);
        Assert.Equal("", viewModel.CustomThemePresetName);
        Assert.True(viewModel.HasCustomThemePresets);
        Assert.Equal("1 saved custom theme", viewModel.CustomThemePresetSummary);

        var option = Assert.Single(viewModel.CustomThemePresetOptions);
        Assert.Equal("Table Night", option.Name);
        Assert.IsType<SolidColorBrush>(option.SwatchBackgroundBrush);
        Assert.IsType<SolidColorBrush>(option.SwatchPanelBrush);
        Assert.IsType<SolidColorBrush>(option.SwatchAccentBrush);

        viewModel.SelectedThemePreset = viewModel.ThemePresetOptions.First(preset => preset.Preset == ThemePreset.ForestMist);
        Assert.Equal(ThemePreset.ForestMist, storage.State.Theme.Preset);

        viewModel.ApplyCustomThemePresetCommand.Execute(option);

        Assert.Null(storage.State.Theme.Preset);
        Assert.Null(viewModel.SelectedThemePreset);
        Assert.Equal(0.64, storage.State.Theme.Chrome.PanelOpacity);
        Assert.Equal(8, storage.State.Theme.Background.BlurRadius);

        viewModel.DeleteCustomThemePresetCommand.Execute(option);

        Assert.Empty(storage.State.CustomThemePresets);
        Assert.Empty(viewModel.CustomThemePresetOptions);
        Assert.False(viewModel.HasCustomThemePresets);
        Assert.Equal("No saved custom themes", viewModel.CustomThemePresetSummary);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void ManualThemeAdjustmentMarksSelectedThemeAsCustom()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        Assert.Equal(ThemePreset.ClassicDungeon, viewModel.SelectedThemePreset?.Preset);

        viewModel.AccentIntensity = 0.95;

        Assert.Null(storage.State.Theme.Preset);
        Assert.Null(viewModel.SelectedThemePreset);
    }

    [Fact]
    public void ThemePaletteColorOptionsExposeAllEditableColors()
    {
        using var viewModel = CreateViewModel(StorageWithImageBackground());

        Assert.Equal(
            [
                "Background Top",
                "Background Bottom",
                "Panel",
                "Panel Alt",
                "Card",
                "Current Card",
                "Accent",
                "Text Primary",
                "Text Secondary",
                "Danger"
            ],
            viewModel.ThemePaletteColorOptions.Select(option => option.Name).ToArray());
    }

    [Fact]
    public void ThemePaletteColorOptionsExposeChannelAccessibilityNames()
    {
        using var viewModel = CreateViewModel(StorageWithImageBackground());
        var accent = Assert.Single(viewModel.ThemePaletteColorOptions, option => option.Name == "Accent");

        Assert.Equal("Accent red channel", accent.RedChannelName);
        Assert.Equal("Accent green channel", accent.GreenChannelName);
        Assert.Equal("Accent blue channel", accent.BlueChannelName);
    }

    [Fact]
    public void ThemePresetAndPaletteOptionNamesUseSelectedLanguage()
    {
        using var viewModel = CreateViewModel(StorageWithImageBackground());
        var russian = Assert.Single(viewModel.LanguageOptions, option => option.Language == AppLanguage.Russian);

        viewModel.SelectedLanguage = russian;

        Assert.Equal(
            [
                "Классическое подземелье",
                "Угли таверны",
                "Лунный склеп",
                "Лесной туман"
            ],
            viewModel.ThemePresetOptions.Select(option => option.Name).ToArray());
        Assert.Equal(
            [
                "Фон сверху",
                "Фон снизу",
                "Панель",
                "Дополнительная панель",
                "Карточка",
                "Текущая карточка",
                "Акцент",
                "Основной текст",
                "Вторичный текст",
                "Опасность"
            ],
            viewModel.ThemePaletteColorOptions.Select(option => option.Name).ToArray());
    }

    [Fact]
    public void ThemePaletteColorOptionUpdatesPaletteSavesAndMarksThemeAsCustom()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);
        var accent = Assert.Single(viewModel.ThemePaletteColorOptions, option => option.Name == "Accent");

        accent.Red = 0.25;
        accent.Green = 0.50;
        accent.Blue = 0.75;

        Assert.Null(storage.State.Theme.Preset);
        Assert.Null(viewModel.SelectedThemePreset);
        Assert.Equal(0.25, storage.State.Theme.Palette.Accent.Red);
        Assert.Equal(0.50, storage.State.Theme.Palette.Accent.Green);
        Assert.Equal(0.75, storage.State.Theme.Palette.Accent.Blue);
        Assert.Equal("#FF4080BF", accent.HexText);
        Assert.Equal("64", accent.RedValueText);
        Assert.Equal(Color.FromRgb(64, 128, 191), ColorOf(viewModel.AccentBrush));
        Assert.True(storage.SaveCount >= 3);
    }

    [Fact]
    public void ThemePaletteColorOptionsRefreshAfterPresetChange()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);
        var accent = Assert.Single(viewModel.ThemePaletteColorOptions, option => option.Name == "Accent");

        viewModel.SelectedThemePreset = viewModel.ThemePresetOptions.First(option => option.Preset == ThemePreset.ForestMist);

        Assert.Equal(0.56, accent.Red, 2);
        Assert.Equal(0.82, accent.Green, 2);
        Assert.Equal(0.60, accent.Blue, 2);
        Assert.Equal("#FF8FD199", accent.HexText);
        Assert.Equal(ThemePreset.ForestMist, storage.State.Theme.Preset);
    }

    [Fact]
    public void ThemeResetPreservesBackgroundAndRestoreDefaultsClearsIt()
    {
        var storage = StorageWithImageBackground();
        storage.State.Theme = ThemeRenderer.ThemeFor(ThemePreset.ForestMist);
        storage.State.Theme.Background.Mode = BackgroundMode.Image;
        storage.State.Theme.Background.ImageOriginalPath = "C:\\audio\\background.png";
        storage.State.Theme.Background.LayoutMode = BackgroundLayoutMode.Tile;
        storage.State.Theme.Chrome.Density = InterfaceDensity.Spacious;
        using var viewModel = CreateViewModel(storage);

        viewModel.PanelOpacity = 0.64;
        viewModel.AccentIntensity = 1;
        viewModel.ResetThemeCommand.Execute(null);

        Assert.Equal(ThemePreset.ClassicDungeon, storage.State.Theme.Preset);
        Assert.Equal(ThemePreset.ClassicDungeon, viewModel.SelectedThemePreset?.Preset);
        Assert.Equal(ThemeRenderer.DefaultTheme.Chrome.PanelOpacity, storage.State.Theme.Chrome.PanelOpacity);
        Assert.Equal(ThemeRenderer.DefaultTheme.Chrome.AccentIntensity, storage.State.Theme.Chrome.AccentIntensity);
        Assert.Equal(BackgroundMode.Image, storage.State.Theme.Background.Mode);
        Assert.Equal("C:\\audio\\background.png", storage.State.Theme.Background.ImageOriginalPath);
        Assert.Equal(BackgroundLayoutMode.Tile, storage.State.Theme.Background.LayoutMode);

        viewModel.RestoreVisualDefaultsCommand.Execute(null);

        Assert.Equal(ThemePreset.ClassicDungeon, storage.State.Theme.Preset);
        Assert.Equal(ThemePreset.ClassicDungeon, viewModel.SelectedThemePreset?.Preset);
        Assert.Equal(BackgroundMode.None, storage.State.Theme.Background.Mode);
        Assert.Null(storage.State.Theme.Background.ImageOriginalPath);
        Assert.False(viewModel.HasBackgroundImage);
        Assert.Equal("Theme preset background", viewModel.BackgroundImageStatus);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void ThemePanelOpacityAppliesToPanelAndCardBrushes()
    {
        var storage = StorageWithImageBackground();
        storage.State.Theme.Chrome.PanelOpacity = 0.64;
        using var viewModel = CreateViewModel(storage);

        Assert.Equal(163, AlphaOf(viewModel.PanelBrush));
        Assert.Equal(163, AlphaOf(viewModel.PanelAltBrush));
        Assert.Equal(163, AlphaOf(viewModel.CardBrush));
        Assert.Equal(163, AlphaOf(viewModel.CurrentCardBrush));
        Assert.Equal(255, AlphaOf(viewModel.AccentBrush));
        Assert.Equal(255, AlphaOf(viewModel.TextPrimaryBrush));
    }

    [Fact]
    public void PanelOpacitySettingClampsSavesAndUpdatesBrushes()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.PanelOpacity = 0.64;

        Assert.Equal(0.64, storage.State.Theme.Chrome.PanelOpacity);
        Assert.Equal(163, AlphaOf(viewModel.PanelBrush));
        Assert.True(storage.SaveCount > 0);

        viewModel.PanelOpacity = 99;

        Assert.Equal(0.98, storage.State.Theme.Chrome.PanelOpacity);
        Assert.Equal(250, AlphaOf(viewModel.PanelBrush));

        viewModel.PanelOpacity = -5;

        Assert.Equal(0.55, storage.State.Theme.Chrome.PanelOpacity);
        Assert.Equal(140, AlphaOf(viewModel.PanelBrush));
    }

    [Fact]
    public void ChromeCornerRadiusSettingClampsSavesAndUpdatesPanelRadius()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.ChromeCornerRadius = 18;

        Assert.Equal(18, storage.State.Theme.Chrome.CornerRadius);
        Assert.Equal(18, viewModel.PanelCornerRadius.TopLeft);
        Assert.True(storage.SaveCount > 0);

        viewModel.ChromeCornerRadius = 99;

        Assert.Equal(24, storage.State.Theme.Chrome.CornerRadius);
        Assert.Equal(24, viewModel.PanelCornerRadius.TopLeft);

        viewModel.ChromeCornerRadius = -5;

        Assert.Equal(8, storage.State.Theme.Chrome.CornerRadius);
        Assert.Equal(8, viewModel.PanelCornerRadius.TopLeft);
    }

    [Fact]
    public void InterfaceDensitySettingSavesAndUpdatesLayoutMetrics()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        Assert.Contains(viewModel.InterfaceDensityOptions, option => option.Value == InterfaceDensity.Compact);
        Assert.Equal(12, viewModel.PanelPadding.Left);
        Assert.Equal(36, viewModel.TrackTileMinHeight);

        viewModel.InterfaceDensity = InterfaceDensity.Spacious;

        Assert.Equal(InterfaceDensity.Spacious, storage.State.Theme.Chrome.Density);
        Assert.Equal(16, viewModel.PanelPadding.Left);
        Assert.Equal(42, viewModel.TrackTileMinHeight);
        Assert.Equal(10, viewModel.TrackTileSpacing);
        Assert.Equal(10, viewModel.TransportBarPadding.Left);
        Assert.True(storage.SaveCount > 0);

        viewModel.InterfaceDensity = InterfaceDensity.Compact;

        Assert.Equal(InterfaceDensity.Compact, storage.State.Theme.Chrome.Density);
        Assert.Equal(10, viewModel.PanelPadding.Left);
        Assert.Equal(32, viewModel.TrackTileMinHeight);
        Assert.Equal(6, viewModel.TrackTileSpacing);
        Assert.Equal(8, viewModel.TransportBarPadding.Left);
    }

    [Fact]
    public void AccentIntensitySettingClampsSavesAndUpdatesCurrentCardBrush()
    {
        var storage = StorageWithImageBackground();
        storage.State.Theme.Chrome.AccentIntensity = 0;
        using var viewModel = CreateViewModel(storage);
        var lowAccentColor = ColorOf(viewModel.CurrentCardBrush);

        viewModel.AccentIntensity = 1;

        Assert.Equal(1, storage.State.Theme.Chrome.AccentIntensity);
        Assert.NotEqual(lowAccentColor, ColorOf(viewModel.CurrentCardBrush));
        Assert.True(storage.SaveCount > 0);

        viewModel.AccentIntensity = -5;

        Assert.Equal(0, storage.State.Theme.Chrome.AccentIntensity);

        viewModel.AccentIntensity = 99;

        Assert.Equal(1, storage.State.Theme.Chrome.AccentIntensity);
    }

    [Fact]
    public void TrackColumnSettingsNormalizeAndChangeTileWidths()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.MusicColumns = 4;
        viewModel.EffectsColumns = 2;

        Assert.Equal(4, storage.State.Preferences.MusicColumns);
        Assert.Equal(2, storage.State.Preferences.EffectsColumns);
        Assert.Equal(204, viewModel.MusicTileWidth);
        Assert.Equal(420, viewModel.EffectTileWidth);

        viewModel.MusicColumns = 99;

        Assert.Equal(3, storage.State.Preferences.MusicColumns);
        Assert.Equal(268, viewModel.MusicTileWidth);
    }

    [Fact]
    public void ColumnOptionsExposeSupportedRange()
    {
        using var viewModel = CreateViewModel(StorageWithImageBackground());

        Assert.Equal([2, 3, 4], viewModel.ColumnCountOptions);
    }

    [Fact]
    public void BackgroundImageProcessorEncodesBlurredImageWhenRadiusIsPositive()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"dungeon-soundboard-background-{Guid.NewGuid():N}.bmp");
        File.WriteAllBytes(imagePath, CreateTestBitmapBytes(12, 12));

        try
        {
            var blurredBytes = BackgroundImageProcessor.EncodeBlurredPng(imagePath, 6);

            Assert.NotEmpty(blurredBytes);
            AssertPngSize(blurredBytes, 12, 12);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    [Fact]
    public void BackgroundImageProcessorReusesBlurredImageUntilFileChanges()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"dungeon-soundboard-background-{Guid.NewGuid():N}.bmp");
        File.WriteAllBytes(imagePath, CreateTestBitmapBytes(12, 12));
        BackgroundImageProcessor.ClearCacheForTesting();

        try
        {
            var firstBytes = BackgroundImageProcessor.EncodeBlurredPng(imagePath, 6);
            var cachedBytes = BackgroundImageProcessor.EncodeBlurredPng(imagePath, 6.004);

            Assert.Equal(1, BackgroundImageProcessor.CacheMissCountForTesting);
            Assert.Equal(firstBytes, cachedBytes);

            File.WriteAllBytes(imagePath, CreateTestBitmapBytes(14, 14));
            File.SetLastWriteTimeUtc(imagePath, DateTime.UtcNow.AddMinutes(1));

            var updatedBytes = BackgroundImageProcessor.EncodeBlurredPng(imagePath, 6);

            Assert.Equal(2, BackgroundImageProcessor.CacheMissCountForTesting);
            AssertPngSize(updatedBytes, 14, 14);
        }
        finally
        {
            BackgroundImageProcessor.ClearCacheForTesting();
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    private static MainWindowViewModel CreateViewModel(FakeStorageService storage)
    {
        return new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService());
    }

    private static byte AlphaOf(IBrush brush)
    {
        var solid = Assert.IsType<SolidColorBrush>(brush);
        return solid.Color.A;
    }

    private static Color ColorOf(IBrush brush)
    {
        var solid = Assert.IsType<SolidColorBrush>(brush);
        return solid.Color;
    }

    private static void AssertPngSize(byte[] bytes, int expectedWidth, int expectedHeight)
    {
        Assert.True(bytes.Length >= 24);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], bytes[..8]);

        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];

        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    private static FakeStorageService StorageWithImageBackground()
    {
        var musicPlaylist = new Playlist("Music");
        var effectPlaylist = new EffectPlaylist("SFX");
        var state = new AppState
        {
            MusicPlaylists = [musicPlaylist],
            EffectPlaylists = [effectPlaylist],
            Preferences = new PlayerPreferences
            {
                SelectedMusicPlaylistId = musicPlaylist.Id,
                SelectedEffectPlaylistId = effectPlaylist.Id
            },
            Theme = ThemeRenderer.ThemeFor(ThemePreset.ClassicDungeon)
        };
        state.Theme.Background.Mode = BackgroundMode.Image;
        state.Theme.Background.ImageOriginalPath = "C:\\audio\\background.png";
        state.Theme.Background.Opacity = 0.72;
        state.Theme.Background.DimOverlay = 0.30;
        state.Theme.Background.LayoutMode = BackgroundLayoutMode.Fill;

        return new FakeStorageService(state);
    }

    private static byte[] CreateTestBitmapBytes(int width, int height)
    {
        var rowStride = ((width * 3 + 3) / 4) * 4;
        var pixelDataSize = rowStride * height;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(54 + pixelDataSize);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(pixelDataSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        var padding = new byte[rowStride - width * 3];
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var value = (byte)((x < width / 2) == (y < height / 2) ? 20 : 230);
                writer.Write(value);
                writer.Write(value);
                writer.Write(value);
            }

            writer.Write(padding);
        }

        return stream.ToArray();
    }

    private sealed class FakeStorageService(AppState state) : IStorageService
    {
        public string DataDirectory => "C:\\Temp\\DungeonSoundboardTests";
        public AppState State => state;
        public int SaveCount { get; private set; }

        public AppState Load()
        {
            state.EnsureDefaults();
            state.Preferences.Language = AppLanguage.English;
            return state;
        }

        public void Save(AppState updatedState)
        {
            SaveCount++;
        }
    }

    private sealed class FakeAudioService : IAudioService
    {
        public event EventHandler? MusicFinished
        {
            add { }
            remove { }
        }

        public event EventHandler? EffectPlaybackCountChanged
        {
            add { }
            remove { }
        }

        public int ActiveEffectCount => 0;
        public bool IsMusicPlaying => false;
        public TimeSpan MusicPosition => TimeSpan.Zero;
        public TimeSpan MusicDuration => TimeSpan.Zero;

        public void Dispose()
        {
        }

        public void PlayMusic(Track track, double volume)
        {
        }

        public void PauseMusic()
        {
        }

        public void FadeOutAndPauseMusic(TimeSpan duration, double restoreVolume)
        {
        }

        public void ResumeMusic()
        {
        }

        public void SeekMusic(TimeSpan position)
        {
        }

        public void StopMusic()
        {
        }

        public void StopAll()
        {
        }

        public void StopEffects()
        {
        }

        public void PlayEffect(Track track, double volume)
        {
        }

        public void SetMusicVolume(double volume)
        {
        }

        public void SetEffectsVolume(double masterVolume)
        {
        }
    }
}
