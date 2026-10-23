using Backend.DTOs;
using Backend.Models;
using Backend.Services.Interfaces;
using LiteDB;

namespace Backend.Services;

// [EDUCATIONAL NOTE - Lab 2 S2 & Lab 6 S2]:
// PlaylistService demonstrates LiteDB's document referencing via .Include().
// Because playlists reference tracks via [BsonRef("tracks")], calling:
//   col.Include(p => p.Tracks).Include(p => p.Owner)
// performs an automatic join-like resolution at query time.
// Furthermore, we enforce strict multi-tenant authorization so students can only update
// or delete their own custom playlists.
public class PlaylistService : IPlaylistService
{
    private readonly ILiteDatabase _db;

    public PlaylistService(ILiteDatabase db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<PlaylistResponseDto>> GetAllPlaylistsAsync(string? userId = null, CancellationToken ct = default)
    {
        var col = _db.GetCollection<Playlist>("playlists");

        var query = col.Include(p => p.Tracks).Include(p => p.Owner);

        var all = string.IsNullOrWhiteSpace(userId)
            ? query.FindAll()
            : query.Find(p => p.UserId == userId);

        var dtos = all
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(MapToDto)
            .ToList();

        return Task.FromResult<IReadOnlyList<PlaylistResponseDto>>(dtos);
    }

    public Task<PlaylistResponseDto?> GetPlaylistByIdAsync(string id, CancellationToken ct = default)
    {
        var col = _db.GetCollection<Playlist>("playlists");
        var playlist = col.Include(p => p.Tracks).Include(p => p.Owner).FindById(id);
        return Task.FromResult(playlist == null ? null : MapToDto(playlist));
    }

    public Task<PlaylistResponseDto> CreatePlaylistAsync(CreatePlaylistDto dto, string userId, CancellationToken ct = default)
    {
        var usersCol = _db.GetCollection<User>("users");
        var user = usersCol.FindById(userId);

        var tracksCol = _db.GetCollection<Track>("tracks");
        var selectedTracks = new List<Track>();

        if (dto.TrackIds != null && dto.TrackIds.Count > 0)
        {
            var requestedSet = dto.TrackIds.ToHashSet();
            // Preserve ordering from dto.TrackIds
            var foundTracks = tracksCol.Find(t => requestedSet.Contains(t.Id)).ToDictionary(t => t.Id);
            foreach (var tid in dto.TrackIds)
            {
                if (foundTracks.TryGetValue(tid, out var trk))
                {
                    selectedTracks.Add(trk);
                }
            }
        }

        var playlist = new Playlist
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            UserId = userId,
            Owner = user,
            Tracks = selectedTracks,
            CreatedAtUtc = DateTime.UtcNow
        };

        var playlistsCol = _db.GetCollection<Playlist>("playlists");
        playlistsCol.Insert(playlist);

        return Task.FromResult(MapToDto(playlist));
    }

    public Task<PlaylistResponseDto?> UpdatePlaylistTracksAsync(string id, UpdatePlaylistTracksDto dto, string userId, CancellationToken ct = default)
    {
        var playlistsCol = _db.GetCollection<Playlist>("playlists");
        var playlist = playlistsCol.Include(p => p.Tracks).Include(p => p.Owner).FindById(id);
        if (playlist == null) return Task.FromResult<PlaylistResponseDto?>(null);

        // Security check: Only the owner may alter the playlist
        if (playlist.UserId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to modify another student's playlist.");
        }

        var tracksCol = _db.GetCollection<Track>("tracks");
        var newTracksList = new List<Track>();

        if (dto.TrackIds != null && dto.TrackIds.Count > 0)
        {
            var requestedSet = dto.TrackIds.ToHashSet();
            var foundTracks = tracksCol.Find(t => requestedSet.Contains(t.Id)).ToDictionary(t => t.Id);
            foreach (var tid in dto.TrackIds)
            {
                if (foundTracks.TryGetValue(tid, out var trk))
                {
                    newTracksList.Add(trk);
                }
            }
        }

        playlist.Tracks = newTracksList;
        playlist.UpdatedAtUtc = DateTime.UtcNow;

        playlistsCol.Update(playlist);

        return Task.FromResult<PlaylistResponseDto?>(MapToDto(playlist));
    }

    public Task<bool> DeletePlaylistAsync(string id, string userId, CancellationToken ct = default)
    {
        var playlistsCol = _db.GetCollection<Playlist>("playlists");
        var playlist = playlistsCol.FindById(id);
        if (playlist == null) return Task.FromResult(false);

        // [EDUCATIONAL NOTE - Lab 6 S2 - Authorization]:
        // Enforce resource-level ownership: Students cannot delete peer playlists.
        if (playlist.UserId != userId)
        {
            throw new UnauthorizedAccessException("Forbidden: You cannot delete another user's playlist.");
        }

        playlistsCol.Delete(id);
        return Task.FromResult(true);
    }

    private static PlaylistResponseDto MapToDto(Playlist playlist)
    {
        var trackDtos = (playlist.Tracks ?? new List<Track>())
            .Where(t => t != null)
            .Select(t => new TrackResponseDto(
                t.Id,
                t.Title,
                t.Artist,
                t.Album,
                t.FileName,
                t.FileSizeBytes,
                t.DurationSeconds,
                t.CoverArtUrl,
                t.UploadedByUserId,
                t.CreatedAtUtc,
                t.AudioMetadata?.MoodTag ?? "Chill",
                t.AudioMetadata?.Bpm ?? 120,
                t.AudioMetadata?.BitrateKbps ?? 320,
                $"/api/tracks/{t.Id}/stream"
            ))
            .ToList();

        var totalDuration = trackDtos.Sum(t => t.DurationSeconds);

        return new PlaylistResponseDto(
            playlist.Id,
            playlist.Title,
            playlist.Description,
            playlist.UserId,
            playlist.Owner?.FullName ?? "Student",
            trackDtos,
            trackDtos.Count,
            totalDuration,
            playlist.CreatedAtUtc
        );
    }
}
