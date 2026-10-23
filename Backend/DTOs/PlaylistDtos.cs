using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

// [EDUCATIONAL NOTE - Lab 2 S2]:
// In a document-oriented database, when returning a Playlist, we can project the resolved
// BsonRef 'Tracks' into strongly-typed TrackResponseDtos. This encapsulates the database
// DBRefs and provides the frontend with immediate track counts and total playtime durations.

public record PlaylistResponseDto(
    string Id,
    string Title,
    string Description,
    string UserId,
    string? OwnerName,
    List<TrackResponseDto> Tracks,
    int TrackCount,
    int TotalDurationSeconds,
    DateTime CreatedAtUtc
);

// [EDUCATIONAL NOTE - Lab 6 S2]:
// CreatePlaylistDto accepts the initial metadata and an optional list of existing track IDs
// to link to this new playlist.
public record CreatePlaylistDto(
    [Required, MinLength(1), MaxLength(100)] string Title,
    string Description,
    List<string>? TrackIds
);

// [EDUCATIONAL NOTE - Lab 6 S2]:
// UpdatePlaylistTracksDto allows reordering, adding, or removing tracks in a playlist
// by supplying an ordered array of Track IDs.
public record UpdatePlaylistTracksDto(
    [Required] List<string> TrackIds
);
