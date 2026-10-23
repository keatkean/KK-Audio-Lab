using Frontend.Models;

namespace Frontend.Services;

// [EDUCATIONAL NOTE - Lab 8 S2]:
// Client-Side In-Memory Catalog Caching Architecture:
// In high-performance single-page applications (SPAs), fetching the catalog from the backend
// on every route navigation creates unnecessary HTTP overhead, increases network latency,
// and wastes server resources.
// ClientCacheService maintains an in-memory cache of TrackResponseDto instances.
// Whenever a mutating operation occurs (Create/Upload, Update metadata, Delete track, or Rescan),
// we execute an explicit cache invalidation (InvalidateTracksCache). This guarantees strict
// eventual consistency between the client-side UI and the backend LiteDB document store.

public class ClientCacheService
{
    private IReadOnlyList<TrackResponseDto>? _cachedTracks;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public DateTime? LastFetchedAt { get; private set; }
    public DateTime? LastInvalidatedAt { get; private set; }
    public int CacheHitCount { get; private set; }
    public int CacheMissCount { get; private set; }

    public bool HasValidCache =>
        _cachedTracks != null &&
        LastFetchedAt.HasValue &&
        (DateTime.UtcNow - LastFetchedAt.Value) < _cacheDuration;

    /// <summary>
    /// Event dispatched whenever the cache is populated, mutated, or invalidated.
    /// UI components subscribe to this to refresh their local state.
    /// </summary>
    public event Action? OnCacheChanged;

    /// <summary>
    /// Retrieves cached tracks if valid; otherwise records a cache miss.
    /// </summary>
    public IReadOnlyList<TrackResponseDto>? GetTracks()
    {
        if (HasValidCache)
        {
            CacheHitCount++;
            return _cachedTracks;
        }

        CacheMissCount++;
        return null;
    }

    /// <summary>
    /// Stores the fresh track list fetched from GET /api/tracks into client memory.
    /// </summary>
    public void SetTracks(IReadOnlyList<TrackResponseDto> tracks)
    {
        _cachedTracks = tracks;
        LastFetchedAt = DateTime.UtcNow;
        OnCacheChanged?.Invoke();
    }

    /// <summary>
    /// Explicitly flushes the client track catalog cache.
    /// Must be invoked on Upload, Update, Delete, or Folder Rescan.
    /// </summary>
    public void InvalidateTracksCache()
    {
        _cachedTracks = null;
        LastFetchedAt = null;
        LastInvalidatedAt = DateTime.UtcNow;
        OnCacheChanged?.Invoke();
    }

    /// <summary>
    /// In-place updates a single track inside the cache to maintain UI snappiness
    /// while invalidating to ensure subsequent re-fetches pull fresh server state.
    /// </summary>
    public void UpdateTrackInCache(TrackResponseDto updatedTrack)
    {
        if (_cachedTracks != null)
        {
            var list = _cachedTracks.Select(t => t.Id == updatedTrack.Id ? updatedTrack : t).ToList();
            _cachedTracks = list;
            OnCacheChanged?.Invoke();
        }
    }

    /// <summary>
    /// In-place removes a single track from the cache.
    /// </summary>
    public void RemoveTrackFromCache(string trackId)
    {
        if (_cachedTracks != null)
        {
            _cachedTracks = _cachedTracks.Where(t => t.Id != trackId).ToList();
            OnCacheChanged?.Invoke();
        }
    }

    /// <summary>
    /// In-place prepends a newly uploaded track to the top of the cache.
    /// </summary>
    public void AddTrackToCache(TrackResponseDto newTrack)
    {
        if (_cachedTracks != null)
        {
            var list = new List<TrackResponseDto> { newTrack };
            list.AddRange(_cachedTracks);
            _cachedTracks = list;
            OnCacheChanged?.Invoke();
        }
    }
}
