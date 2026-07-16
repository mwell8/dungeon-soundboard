using System.Text.Json;

namespace DungeonSoundboard.App.Services;

public interface IErrorLogService
{
    string LogDirectory { get; }

    void Write(string category, string message, Exception? exception = null);
}

public sealed class LocalErrorLogService : IErrorLogService
{
    private const long MaximumLogBytes = 1024 * 1024;
    private const int BackupCount = 3;
    private readonly object _gate = new();
    private readonly string _logPath;

    public LocalErrorLogService(string? dataDirectory = null)
    {
        var root = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DungeonSoundboard");
        LogDirectory = Path.Combine(root, "Logs");
        _logPath = Path.Combine(LogDirectory, "errors.log");
    }

    public string LogDirectory { get; }

    public void Write(string category, string message, Exception? exception = null)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded();
                var entry = JsonSerializer.Serialize(new
                {
                    timestampUtc = DateTimeOffset.UtcNow,
                    category,
                    message,
                    exception = exception?.ToString()
                });
                File.AppendAllText(_logPath, entry + Environment.NewLine);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine($"Dungeon Soundboard error log failed: {ex}");
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_logPath) || new FileInfo(_logPath).Length < MaximumLogBytes)
        {
            return;
        }

        var oldest = $"{_logPath}.{BackupCount}";
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = BackupCount - 1; index >= 1; index--)
        {
            var source = $"{_logPath}.{index}";
            if (File.Exists(source))
            {
                File.Move(source, $"{_logPath}.{index + 1}");
            }
        }

        File.Move(_logPath, $"{_logPath}.1");
    }
}

public sealed class NullErrorLogService : IErrorLogService
{
    public static NullErrorLogService Instance { get; } = new();

    private NullErrorLogService()
    {
    }

    public string LogDirectory => "";

    public void Write(string category, string message, Exception? exception = null)
    {
    }
}
