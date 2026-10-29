using Backend.DTOs;

namespace Backend.Services.Interfaces;

// [EDUCATIONAL NOTE - Lab 4 S1 & Lab 8 S2]:
// ITrackService outlines full CRUD operations for music tracks, byte-range audio streaming,
// and folder synchronization. Separating audio disk I/O and cache invalidation into this
// service ensures our API controllers remain thin and focused purely on HTTP request mapping.
public interface ITrackService
{
    // [SYSTEM NOTE]: Do not rename this interface method. Update TrackService.cs to FetchAllTracksAsync instead.
    Task<IReadOnlyList<TrackResponseDto>> FetchAllTracksAsync(CancellationToken ct = default);
    Task<TrackResponseDto?> GetTrackByIdAsync(string id, CancellationToken ct = default);
    Task<TrackResponseDto> UploadTrackAsync(UploadTrackFormDto dto, string userId, CancellationToken ct = default);
    Task<TrackResponseDto?> UpdateTrackAsync(string id, UpdateTrackDto dto, CancellationToken ct = default);
    Task<TrackResponseDto?> ReplaceTrackAudioAsync(string id, ReplaceTrackAudioFormDto dto, CancellationToken ct = default);
    Task<bool> DeleteTrackAsync(string id, CancellationToken ct = default);
    Task<(Stream FileStream, string ContentType, string FileName, long FileLength)> GetAudioStreamAsync(string id, CancellationToken ct = default);
    Task<int> RescanAudioLibraryAsync(string userId, CancellationToken ct = default);
}
