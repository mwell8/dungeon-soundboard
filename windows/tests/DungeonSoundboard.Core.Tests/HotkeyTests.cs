using System.Text.Json;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Serialization;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class HotkeyTests
{
    [Fact]
    public void HotkeyNormalizationRejectsTab()
    {
        Assert.Null(Hotkey.Normalized(
            HotkeyConfiguration.TabKeyCode,
            "\t",
            "\t",
            isShiftPressed: false,
            isControlPressed: false,
            isOptionPressed: false,
            isCommandPressed: false));
    }

    [Fact]
    public void DefaultHotkeysContainExpectedSystemActions()
    {
        var configuration = HotkeyConfiguration.Defaults;

        Assert.Equal("Delete", configuration.HotkeyFor(HotkeyAction.StopEffects)?.DisplayText);
        Assert.Null(configuration.HotkeyFor(HotkeyAction.StopAll));
        Assert.Equal("Space", configuration.HotkeyFor(HotkeyAction.PlayPause)?.DisplayText);
        Assert.Equal("Shift++", configuration.HotkeyFor(HotkeyAction.EffectsVolumeUp)?.DisplayText);
        Assert.Equal("Shift+-", configuration.HotkeyFor(HotkeyAction.EffectsVolumeDown)?.DisplayText);
    }

    [Fact]
    public void RestoreDefaultRestoresOnlyTheRequestedSystemBinding()
    {
        var customPlayPause = new Hotkey(0, "A", HotkeyModifier.None);
        var customStopEffects = new Hotkey(1, "B", HotkeyModifier.None);
        var configuration = new HotkeyConfiguration(
        [
            new HotkeyBinding(HotkeyAction.PlayPause, customPlayPause),
            new HotkeyBinding(HotkeyAction.StopEffects, customStopEffects)
        ]);

        configuration.RestoreDefault(HotkeyAction.PlayPause);

        Assert.Equal("Space", configuration.HotkeyFor(HotkeyAction.PlayPause)?.DisplayText);
        Assert.Equal(customStopEffects, configuration.HotkeyFor(HotkeyAction.StopEffects));
    }

    [Fact]
    public void RestoreDefaultSystemBindingRemovesAConflictingTrackBinding()
    {
        var playlistId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var configuration = new HotkeyConfiguration(
        [
            new HotkeyBinding(HotkeyAction.PlayMusicTrack(playlistId, trackId), new Hotkey(HotkeyConfiguration.SpaceKeyCode, "Space", HotkeyModifier.None))
        ]);

        configuration.RestoreDefault(HotkeyAction.PlayPause);

        Assert.Equal("Space", configuration.HotkeyFor(HotkeyAction.PlayPause)?.DisplayText);
        Assert.Null(configuration.HotkeyFor(HotkeyAction.PlayMusicTrack(playlistId, trackId)));
    }

    [Fact]
    public void RestoreDefaultClearsAnActionThatIsUnassignedByDefault()
    {
        var configuration = new HotkeyConfiguration(
        [
            new HotkeyBinding(HotkeyAction.StopAll, new Hotkey(0, "A", HotkeyModifier.None))
        ]);

        configuration.RestoreDefault(HotkeyAction.StopAll);

        Assert.Null(configuration.HotkeyFor(HotkeyAction.StopAll));
    }

    [Fact]
    public void AssignReportsConflictUnlessResolving()
    {
        var hotkey = new Hotkey(12, "Q", HotkeyModifier.None);
        var configuration = new HotkeyConfiguration(
        [
            new HotkeyBinding(HotkeyAction.PlayPause, hotkey)
        ]);

        var conflict = configuration.Assign(hotkey, HotkeyAction.StopAll, resolvingConflicts: false);

        Assert.Equal(HotkeyAction.PlayPause, conflict?.ExistingAction);
        Assert.Equal(hotkey, configuration.HotkeyFor(HotkeyAction.PlayPause));

        var resolved = configuration.Assign(hotkey, HotkeyAction.StopAll, resolvingConflicts: true);

        Assert.Null(resolved);
        Assert.Null(configuration.HotkeyFor(HotkeyAction.PlayPause));
        Assert.Equal(hotkey, configuration.HotkeyFor(HotkeyAction.StopAll));
    }

    [Fact]
    public void ConfigurationSurvivesCoding()
    {
        var action = HotkeyAction.PlayMusicTrack(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var configuration = new HotkeyConfiguration(
        [
            new HotkeyBinding(action, new Hotkey(0, "A", HotkeyModifier.Control))
        ]);

        var data = JsonSerializer.SerializeToUtf8Bytes(configuration, JsonDefaults.Options);
        var decoded = JsonSerializer.Deserialize<HotkeyConfiguration>(data, JsonDefaults.Options);

        Assert.Equal(configuration, decoded);
    }

    [Fact]
    public void LegacyDeleteStopAllDefaultMigratesToStopEffects()
    {
        var configuration = new HotkeyConfiguration(
        [
            new HotkeyBinding(HotkeyAction.StopAll, HotkeyConfiguration.DeleteHotkey),
            new HotkeyBinding(HotkeyAction.PlayPause, new Hotkey(49, "Space", HotkeyModifier.None))
        ]);

        configuration.MigrateLegacyDeleteStopAllDefault();

        Assert.Null(configuration.HotkeyFor(HotkeyAction.StopAll));
        Assert.Equal(HotkeyConfiguration.DeleteHotkey, configuration.HotkeyFor(HotkeyAction.StopEffects));
        Assert.Equal("Space", configuration.HotkeyFor(HotkeyAction.PlayPause)?.DisplayText);
    }

    [Fact]
    public void HotkeyNormalizationAllowsPlainShiftAndControl()
    {
        Assert.Equal(
            new Hotkey(0, "A", HotkeyModifier.None),
            Hotkey.Normalized(0, "a", "a", false, false, false, false));
        Assert.Equal(
            new Hotkey(0, "A", HotkeyModifier.Shift),
            Hotkey.Normalized(0, "a", "A", true, false, false, false));
        Assert.Equal(
            new Hotkey(0, "A", HotkeyModifier.Control),
            Hotkey.Normalized(0, "a", "a", false, true, false, false));
    }

    [Fact]
    public void HotkeyNormalizationRejectsUnsupportedModifiers()
    {
        Assert.Null(Hotkey.Normalized(0, "a", "a", false, false, true, false));
        Assert.Null(Hotkey.Normalized(0, "a", "a", false, false, false, true));
        Assert.Null(Hotkey.Normalized(0, "a", "a", true, true, false, false));
    }

    [Fact]
    public void HotkeyNormalizationTreatsForwardDeleteAsDelete()
    {
        Assert.Equal(
            new Hotkey(51, "Delete", HotkeyModifier.None),
            Hotkey.Normalized(117, null, null, false, false, false, false));
    }

    [Fact]
    public void MissingTrackBindingsAreRemoved()
    {
        var existingPlaylistId = Guid.NewGuid();
        var missingPlaylistId = Guid.NewGuid();
        var existingTrackId = Guid.NewGuid();
        var missingTrackId = Guid.NewGuid();
        var configuration = new HotkeyConfiguration(
        [
            new HotkeyBinding(
                HotkeyAction.PlayMusicTrack(existingPlaylistId, existingTrackId),
                new Hotkey(0, "A", HotkeyModifier.None)),
            new HotkeyBinding(
                HotkeyAction.PlayMusicTrack(missingPlaylistId, missingTrackId),
                new Hotkey(1, "S", HotkeyModifier.None))
        ]);

        configuration.RemoveMissingTrackBindings(
            new HashSet<Guid> { existingPlaylistId },
            new HashSet<Guid> { existingTrackId },
            new HashSet<Guid>(),
            new HashSet<Guid>());

        Assert.Single(configuration.Bindings);
        Assert.Equal(HotkeyAction.PlayMusicTrack(existingPlaylistId, existingTrackId), configuration.Bindings[0].Action);
    }
}
