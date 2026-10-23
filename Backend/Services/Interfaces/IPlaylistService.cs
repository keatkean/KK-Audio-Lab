using Backend.DTOs;

namespace Backend.Services.Interfaces;

// [EDUCATIONAL NOTE - Lab 2 S2 & Lab 6 S2]:
// IPlaylistService manages user playlists. Because playlists store DBRefs to tracks and users,
// this interface abstracts playlist hydration (.Include(p => p.Tracks)) and ensures that
// ownership authorization checks (e.g. only the owner can modify or delete a playlist)
// are enforced cleanly at the business logic layer.
public interface IPlaylistService
{
    Task<IReadOnlyList<PlaylistResponseDto>> GetAllPlaylistsAsync(string? userId = null, CancellationToken ct = default);
    Task<PlaylistResponseDto?> GetPlaylistByIdAsync(string id, CancellationToken ct = default);
    Task<PlaylistResponseDto> CreatePlaylistAsync(CreatePlaylistDto dto, string userId, CancellationToken ct = default);
    Task<PlaylistResponseDto?> UpdatePlaylistTracksAsync(string id, UpdatePlaylistTracksDto dto, string userId, CancellationToken ct = default);
    Task<bool> DeletePlaylistAsync(string id, string userId, CancellationToken ct = default);
}
