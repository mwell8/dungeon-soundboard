using System.Runtime.InteropServices;
using DungeonSoundboard.Core.Models;
using NAudio.Wave;

namespace DungeonSoundboard.App.Services;

public sealed class NAudioAudioService : IAudioService
{
    private readonly object _gate = new();
    private readonly List<EffectPlayback> _effects = [];
    private WaveOutEvent? _musicOutput;
    private AudioFileReader? _musicReader;
    private Track? _musicTrack;
    private bool _musicStopRequested;
    private double _lastEffectsMasterVolume = 0.8;
    private CancellationTokenSource? _musicFadeCancellation;

    public event EventHandler? MusicFinished;
    public event EventHandler? EffectPlaybackCountChanged;

    public int ActiveEffectCount
    {
        get
        {
            lock (_gate)
            {
                return _effects.Count;
            }
        }
    }

    public bool IsMusicPlaying => _musicOutput?.PlaybackState == PlaybackState.Playing;

    public TimeSpan MusicPosition => _musicReader?.CurrentTime ?? TimeSpan.Zero;

    public TimeSpan MusicDuration => _musicReader?.TotalTime ?? TimeSpan.Zero;

    public void PlayMusic(Track track, double volume)
    {
        if (!File.Exists(track.Path))
        {
            throw new FileNotFoundException("Audio file was not found.", track.Path);
        }

        StopMusic();

        _musicStopRequested = false;
        _musicTrack = track;
        try
        {
            _musicReader = CreateReader(track.Path, volume);
            _musicOutput = CreateOutput(_musicReader);
            _musicOutput.PlaybackStopped += HandleMusicStopped;
            StartOutput(_musicOutput);
        }
        catch
        {
            _musicOutput?.Dispose();
            _musicReader?.Dispose();
            _musicOutput = null;
            _musicReader = null;
            _musicTrack = null;
            throw;
        }
    }

    public void PauseMusic()
    {
        CancelMusicFade();
        _musicOutput?.Pause();
    }

    public void FadeOutAndPauseMusic(TimeSpan duration, double restoreVolume)
    {
        CancelMusicFade();

        var output = _musicOutput;
        var reader = _musicReader;
        if (output is null || reader is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _musicFadeCancellation = cancellation;
        _ = FadeOutAndPauseMusicAsync(output, reader, duration, ClampUnit(restoreVolume), cancellation);
    }

    public void ResumeMusic()
    {
        CancelMusicFade();
        _musicOutput?.Play();
    }

    public void SeekMusic(TimeSpan position)
    {
        if (_musicReader is null)
        {
            return;
        }

        var duration = _musicReader.TotalTime;
        var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (duration > TimeSpan.Zero && clamped > duration)
        {
            clamped = duration;
        }

        _musicReader.CurrentTime = clamped;
    }

    public void StopMusic()
    {
        CancelMusicFade();
        _musicStopRequested = true;
        if (_musicOutput is not null)
        {
            _musicOutput.PlaybackStopped -= HandleMusicStopped;
            _musicOutput.Stop();
            _musicOutput.Dispose();
            _musicOutput = null;
        }

        _musicReader?.Dispose();
        _musicReader = null;
        _musicTrack = null;
    }

    public void StopAll()
    {
        StopMusic();
        StopEffects();
    }

    public void StopEffects()
    {
        List<EffectPlayback> effects;
        lock (_gate)
        {
            effects = _effects.ToList();
            _effects.Clear();
        }

        foreach (var effect in effects)
        {
            effect.Dispose();
        }

        EffectPlaybackCountChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PlayEffect(Track track, double volume)
    {
        if (!File.Exists(track.Path))
        {
            throw new FileNotFoundException("Effect file was not found.", track.Path);
        }

        AudioFileReader? reader = null;
        WaveOutEvent? output = null;
        EffectPlayback? playback = null;
        try
        {
            reader = CreateReader(track.Path, volume);
            output = CreateOutput(reader);
            playback = new EffectPlayback(output, reader, track.VolumeMultiplier);
            output.PlaybackStopped += (_, _) => RemoveEffect(playback);

            lock (_gate)
            {
                _effects.Add(playback);
            }

            EffectPlaybackCountChanged?.Invoke(this, EventArgs.Empty);
            StartOutput(output);
        }
        catch
        {
            if (playback is not null)
            {
                RemoveEffect(playback);
            }
            else
            {
                output?.Dispose();
                reader?.Dispose();
            }

            throw;
        }
    }

    public void SetMusicVolume(double volume)
    {
        if (_musicReader is not null)
        {
            _musicReader.Volume = (float)ClampUnit(volume);
        }
    }

    public void SetEffectsVolume(double masterVolume)
    {
        _lastEffectsMasterVolume = masterVolume;
        lock (_gate)
        {
            foreach (var effect in _effects)
            {
                effect.Reader.Volume = (float)Track.OutputVolume(masterVolume, effect.VolumeMultiplier);
            }
        }
    }

    public void Dispose()
    {
        StopAll();
    }

    private void HandleMusicStopped(object? sender, StoppedEventArgs e)
    {
        CancelMusicFade();
        if (_musicStopRequested)
        {
            return;
        }

        _musicOutput?.Dispose();
        _musicReader?.Dispose();
        _musicOutput = null;
        _musicReader = null;
        MusicFinished?.Invoke(this, EventArgs.Empty);
    }

    private async Task FadeOutAndPauseMusicAsync(
        WaveOutEvent output,
        AudioFileReader reader,
        TimeSpan duration,
        double restoreVolume,
        CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        var initialVolume = ClampUnit(reader.Volume);
        var stepCount = Math.Max(1, (int)Math.Ceiling(Math.Max(0, duration.TotalMilliseconds) / 50));
        var delay = duration <= TimeSpan.Zero
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(duration.TotalMilliseconds / stepCount);

        try
        {
            for (var step = 1; step <= stepCount; step++)
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }

                if (!IsCurrentMusic(output, reader))
                {
                    return;
                }

                var progress = (double)step / stepCount;
                reader.Volume = (float)(initialVolume * (1 - progress));
            }

            if (!IsCurrentMusic(output, reader))
            {
                return;
            }

            output.Pause();
            reader.Volume = (float)restoreVolume;
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentMusic(output, reader))
            {
                reader.Volume = (float)restoreVolume;
            }
        }
        finally
        {
            if (ReferenceEquals(_musicFadeCancellation, cancellation))
            {
                _musicFadeCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private bool IsCurrentMusic(WaveOutEvent output, AudioFileReader reader)
    {
        return ReferenceEquals(_musicOutput, output) && ReferenceEquals(_musicReader, reader);
    }

    private void CancelMusicFade()
    {
        var cancellation = _musicFadeCancellation;
        if (cancellation is null)
        {
            return;
        }

        _musicFadeCancellation = null;
        cancellation.Cancel();
    }

    private static double ClampUnit(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return Math.Min(Math.Max(value, 0), 1);
    }

    private static AudioFileReader CreateReader(string path, double volume)
    {
        try
        {
            return new AudioFileReader(path) { Volume = (float)volume };
        }
        catch (Exception ex) when (IsRecoverableAudioException(ex))
        {
            throw new AudioPlaybackException(
                AudioPlaybackFailureKind.UnsupportedCodec,
                ex.Message,
                ex);
        }
    }

    private static WaveOutEvent CreateOutput(IWaveProvider provider)
    {
        WaveOutEvent? output = null;
        try
        {
            output = new WaveOutEvent();
            output.Init(provider);
            return output;
        }
        catch (Exception ex) when (IsRecoverableAudioException(ex))
        {
            output?.Dispose();
            throw new AudioPlaybackException(
                AudioPlaybackFailureKind.OutputDevice,
                ex.Message,
                ex);
        }
    }

    private static void StartOutput(WaveOutEvent output)
    {
        try
        {
            output.Play();
        }
        catch (Exception ex) when (IsRecoverableAudioException(ex))
        {
            throw new AudioPlaybackException(
                AudioPlaybackFailureKind.OutputDevice,
                ex.Message,
                ex);
        }
    }

    private static bool IsRecoverableAudioException(Exception ex)
    {
        return ex is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or NotSupportedException
            or InvalidOperationException
            or COMException
            or NAudio.MmException;
    }

    private void RemoveEffect(EffectPlayback playback)
    {
        var removed = false;
        lock (_gate)
        {
            removed = _effects.Remove(playback);
        }

        if (!removed)
        {
            return;
        }

        playback.Dispose();
        SetEffectsVolume(_lastEffectsMasterVolume);
        EffectPlaybackCountChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class EffectPlayback : IDisposable
    {
        public EffectPlayback(WaveOutEvent output, AudioFileReader reader, double volumeMultiplier)
        {
            Output = output;
            Reader = reader;
            VolumeMultiplier = volumeMultiplier;
        }

        public WaveOutEvent Output { get; }
        public AudioFileReader Reader { get; }
        public double VolumeMultiplier { get; }

        public void Dispose()
        {
            Output.Stop();
            Output.Dispose();
            Reader.Dispose();
        }
    }
}
