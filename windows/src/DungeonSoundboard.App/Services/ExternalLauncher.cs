using System.Diagnostics;

namespace DungeonSoundboard.App.Services;

public interface IExternalLauncher
{
    void OpenFolder(string path);
}

public sealed class WindowsExternalLauncher : IExternalLauncher
{
    public void OpenFolder(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
