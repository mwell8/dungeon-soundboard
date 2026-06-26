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

    public void PlayMusic(Track track, double volume)
    {
        if (!File.Exists(track.Path))
        {
            throw new FileNotFoundException("Audio file was not found.", track.Path);
        }

        StopMusic();

        _musicStopRequested = false;
        _musicTrack = track;
        _musicReader = new AudioFileReader(track.Path) { Volume = (float)volume };
        _musicOutput = new WaveOutEvent();
        _musicOutput.Init(_musicReader);
        _musicOutput.PlaybackStopped += HandleMusicStopped;
        _musicOutput.Play();
    }

    public void PauseMusic()
    {
        _musicOutput?.Pause();
    }

    public void ResumeMusic()
    {
        _musicOutput?.Play();
    }

    public void StopMusic()
    {
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

        var reader = new AudioFileReader(track.Path) { Volume = (float)volume };
        var output = new WaveOutEvent();
        var playback = new EffectPlayback(output, reader, track.VolumeMultiplier);
        output.Init(reader);
        output.PlaybackStopped += (_, _) => RemoveEffect(playback);

        lock (_gate)
        {
            _effects.Add(playback);
        }

        EffectPlaybackCountChanged?.Invoke(this, EventArgs.Empty);
        output.Play();
    }

    public void SetMusicVolume(double volume)
    {
        if (_musicReader is not null)
        {
            _musicReader.Volume = (float)Math.Min(Math.Max(volume, 0), 1);
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
