using Frontend.Models;

namespace Frontend.Services;

// [EDUCATIONAL NOTE - Lab 9 S1]:
// Reactive State Container Pattern in Blazor:
// Blazor components follow an event-driven lifecycle. Instead of tightly coupling child components
// (e.g., TrackCard, TrackEditModal) to the sticky bottom AudioPlayerBar, we inject a shared
// Singleton/Scoped PlayerStateService.
// When an action occurs (playing a song, updating track metadata live, or deleting a song),
// the service updates its internal state and invokes the OnChange event, triggering
// StateHasChanged() across all subscribed components.

public class PlayerStateService
{
    public TrackResponseDto? CurrentTrack { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsLoading { get; private set; }
    public double CurrentTime { get; private set; }
    public double Duration { get; private set; }
    public double Volume { get; private set; } = 0.85;
    public bool IsMuted { get; private set; }
    public double BufferPercent { get; private set; }
    public double LatencyMs { get; private set; }
    public bool IsNextPrefetched { get; private set; }
    public List<TrackResponseDto> Queue { get; private set; } = new();
    public int CurrentIndex => CurrentTrack != null ? Queue.FindIndex(t => t.Id == CurrentTrack.Id) : -1;

    public TrackResponseDto? NextTrack
    {
        get
        {
            var idx = CurrentIndex;
            return (idx >= 0 && idx + 1 < Queue.Count) ? Queue[idx + 1] : null;
        }
    }

    public TrackResponseDto? PreviousTrack
    {
        get
        {
            var idx = CurrentIndex;
            return (idx > 0 && idx < Queue.Count) ? Queue[idx - 1] : null;
        }
    }

    // State events for component subscription
    public event Action? OnChange;
    public event Action<TrackResponseDto>? OnTrackChange;
    public event Action? OnPlayRequest;
    public event Action? OnPauseRequest;
    public event Action<double>? OnSeekRequest;
    public event Action<string>? OnPrefetchRequest;

    /// <summary>
    /// Loads and begins playback of the chosen audio track.
    /// Optionally replaces or appends to the current queue.
    /// </summary>
    public void PlayTrack(TrackResponseDto track, List<TrackResponseDto>? newQueue = null)
    {
        if (newQueue != null && newQueue.Count > 0)
        {
            Queue = new List<TrackResponseDto>(newQueue);
        }
        else if (!Queue.Any(t => t.Id == track.Id))
        {
            Queue.Add(track);
        }

        CurrentTrack = track;
        IsPlaying = true;
        IsLoading = true;
        CurrentTime = 0;
        Duration = track.DurationSeconds > 0 ? track.DurationSeconds : 180;
        BufferPercent = 0;
        IsNextPrefetched = false;

        OnTrackChange?.Invoke(track);
        NotifyStateChanged();
    }

    public void TogglePlayPause()
    {
        if (CurrentTrack == null) return;

        if (IsPlaying)
        {
            IsPlaying = false;
            OnPauseRequest?.Invoke();
        }
        else
        {
            IsPlaying = true;
            OnPlayRequest?.Invoke();
        }
        NotifyStateChanged();
    }

    public void SetPlaying(bool playing)
    {
        if (IsPlaying != playing)
        {
            IsPlaying = playing;
            NotifyStateChanged();
        }
    }

    public void SetLoading(bool loading)
    {
        if (IsLoading != loading)
        {
            IsLoading = loading;
            NotifyStateChanged();
        }
    }

    public void Next()
    {
        var next = NextTrack;
        if (next != null)
        {
            PlayTrack(next);
        }
    }

    public void Previous()
    {
        var prev = PreviousTrack;
        if (prev != null)
        {
            PlayTrack(prev);
        }
        else if (CurrentTime > 3)
        {
            Seek(0);
        }
    }

    public void Seek(double seconds)
    {
        CurrentTime = seconds;
        OnSeekRequest?.Invoke(seconds);
        NotifyStateChanged();
    }

    public void SetVolume(double volume)
    {
        Volume = Math.Clamp(volume, 0.0, 1.0);
        if (Volume > 0 && IsMuted)
        {
            IsMuted = false;
        }
        NotifyStateChanged();
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        NotifyStateChanged();
    }

    /// <summary>
    /// Invoked from JS Interop on each HTML5 audio 'timeupdate' and 'progress' event.
    /// Triggers next-track HTTP 206 prefetching when playback reaches 75%.
    /// </summary>
    public void UpdatePlaybackProgress(double currentTime, double duration, double bufferPercent)
    {
        CurrentTime = currentTime;
        if (duration > 0)
        {
            Duration = duration;
        }
        BufferPercent = bufferPercent;

        // [EDUCATIONAL NOTE - Lab 1 S1]:
        // Smart Audio Prefetching:
        // When the user has listened to >= 75% of the song, we issue a byte-range prefetch
        // for the next track in the queue to prime the browser's HTTP disk cache.
        if (Duration > 0)
        {
            var progressPercent = (CurrentTime / Duration) * 100.0;
            if (progressPercent >= 75.0 && !IsNextPrefetched && NextTrack != null)
            {
                IsNextPrefetched = true;
                OnPrefetchRequest?.Invoke(NextTrack.StreamUrl);
            }
        }

        NotifyStateChanged();
    }

    public void RecordLatency(double latencyMs)
    {
        LatencyMs = latencyMs;
        IsLoading = false;
        NotifyStateChanged();
    }

    /// <summary>
    /// Synchronizes updated metadata with the currently active track live in the player bar.
    /// </summary>
    public void UpdateCurrentTrackMetadata(string trackId, UpdateTrackDto dto)
    {
        // Update in queue
        var idx = Queue.FindIndex(t => t.Id == trackId);
        if (idx >= 0)
        {
            var existing = Queue[idx];
            var updated = existing with
            {
                Title = dto.Title,
                Artist = dto.Artist,
                Album = dto.Album,
                MoodTag = dto.MoodTag,
                Bpm = dto.Bpm,
                BitrateKbps = dto.BitrateKbps,
                DurationSeconds = dto.DurationSeconds,
                CoverArtUrl = dto.CoverArtUrl ?? existing.CoverArtUrl
            };
            Queue[idx] = updated;

            if (CurrentTrack?.Id == trackId)
            {
                CurrentTrack = updated;
            }
        }
        else if (CurrentTrack?.Id == trackId)
        {
            CurrentTrack = CurrentTrack with
            {
                Title = dto.Title,
                Artist = dto.Artist,
                Album = dto.Album,
                MoodTag = dto.MoodTag,
                Bpm = dto.Bpm,
                BitrateKbps = dto.BitrateKbps,
                DurationSeconds = dto.DurationSeconds,
                CoverArtUrl = dto.CoverArtUrl ?? CurrentTrack.CoverArtUrl
            };
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// Handles track deletion: cleanly halts playback if the deleted track was playing.
    /// </summary>
    public void OnTrackDeleted(string trackId)
    {
        Queue.RemoveAll(t => t.Id == trackId);

        if (CurrentTrack?.Id == trackId)
        {
            IsPlaying = false;
            OnPauseRequest?.Invoke();
            CurrentTrack = null;
            CurrentTime = 0;
            BufferPercent = 0;
        }

        NotifyStateChanged();
    }

    public void AddToQueue(TrackResponseDto track)
    {
        if (!Queue.Any(t => t.Id == track.Id))
        {
            Queue.Add(track);
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
