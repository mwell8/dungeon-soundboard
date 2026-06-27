using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.App.Services;

public interface IAudioService : IDisposable
{
    event EventHandler? MusicFinished;
    event EventHandler? EffectPlaybackCountChanged;

    int ActiveEffectCount { get; }
    bool IsMusicPlaying { get; }
    TimeSpan MusicPosition { get; }
    TimeSpan MusicDuration { get; }

    void PlayMusic(Track track, double volume);
    void PauseMusic();
    void ResumeMusic();
    void SeekMusic(TimeSpan position);
    void StopMusic();
    void StopAll();
    void StopEffects();
    void PlayEffect(Track track, double volume);
    void SetMusicVolume(double volume);
    void SetEffectsVolume(double masterVolume);
}
