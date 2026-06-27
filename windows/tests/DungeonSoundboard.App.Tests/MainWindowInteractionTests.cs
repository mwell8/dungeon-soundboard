using Avalonia.Controls;
using Xunit;

namespace DungeonSoundboard.App.Tests;

public sealed class MainWindowInteractionTests
{
    [Theory]
    [InlineData("tileAction")]
    [InlineData("tileIcon")]
    public void TileActionSourceRecognizesTrackTileActionClasses(string actionClass)
    {
        var button = new Button();
        button.Classes.Add(actionClass);

        Assert.True(MainWindow.IsTileActionSource(button));
    }
}
