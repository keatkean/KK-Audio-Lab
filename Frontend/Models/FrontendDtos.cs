using System.ComponentModel.DataAnnotations;

namespace Frontend.Models;

// [EDUCATIONAL NOTE - Lab 6 S2 & Lab 8 S1]:
// Data Transfer Objects (DTOs) and Form Models in Blazor WebAssembly:
// Blazor client-side components require strongly-typed models for data binding,
// client-side validation using DataAnnotationsValidator, and serialization to/from JSON.
// Decoupling client view models from backend database documents ensures resilient contracts.

/// <summary>
/// Audio track response returned from backend WebAPI (GET /api/tracks).
/// </summary>
public record TrackResponseDto(
    string Id,
    string Title,
    string Artist,
    string Album,
    string FileName,
    long FileSizeBytes,
    int DurationSeconds,
    string CoverArtUrl,
    string UploadedByUserId,
    DateTime CreatedAtUtc,
    string MoodTag,
    int Bpm,
    int BitrateKbps,
    string StreamUrl
);

/// <summary>
/// Mutable form model used in Blazor EditForm for track upload with DataAnnotations validation.
/// </summary>
public class UploadTrackFormModel
{
    [Required(ErrorMessage = "Track title is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Artist name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Artist must be between 1 and 100 characters.")]
    public string Artist { get; set; } = string.Empty;

    [Required(ErrorMessage = "Album name is required.")]
    [StringLength(100, ErrorMessage = "Album cannot exceed 100 characters.")]
    public string Album { get; set; } = "Single";

    [Required(ErrorMessage = "Please select a mood tag.")]
    public string MoodTag { get; set; } = "Chill";

    [Range(20, 300, ErrorMessage = "BPM must be between 20 and 300.")]
    public int Bpm { get; set; } = 120;

    [Range(32, 1411, ErrorMessage = "Bitrate must be between 32 and 1411 kbps.")]
    public int BitrateKbps { get; set; } = 320;

    [Range(1, 7200, ErrorMessage = "Duration must be between 1 and 7200 seconds.")]
    public int DurationSeconds { get; set; } = 180;

    [Url(ErrorMessage = "Please enter a valid URL for cover art.")]
    public string? CoverArtUrl { get; set; }
}

/// <summary>
/// Metadata-only update payload for PUT /api/tracks/{id}.
/// </summary>
public class UpdateTrackDto
{
    [Required(ErrorMessage = "Track title is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Artist name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Artist must be between 1 and 100 characters.")]
    public string Artist { get; set; } = string.Empty;

    [Required(ErrorMessage = "Album name is required.")]
    [StringLength(100, ErrorMessage = "Album cannot exceed 100 characters.")]
    public string Album { get; set; } = "Single";

    [Required(ErrorMessage = "Please select a mood tag.")]
    public string MoodTag { get; set; } = "Chill";

    [Range(20, 300, ErrorMessage = "BPM must be between 20 and 300.")]
    public int Bpm { get; set; } = 120;

    [Range(32, 1411, ErrorMessage = "Bitrate must be between 32 and 1411 kbps.")]
    public int BitrateKbps { get; set; } = 320;

    [Range(1, 7200, ErrorMessage = "Duration must be between 1 and 7200 seconds.")]
    public int DurationSeconds { get; set; } = 180;

    public string? CoverArtUrl { get; set; }

    public static UpdateTrackDto FromTrack(TrackResponseDto track) => new()
    {
        Title = track.Title,
        Artist = track.Artist,
        Album = track.Album,
        MoodTag = track.MoodTag,
        Bpm = track.Bpm,
        BitrateKbps = track.BitrateKbps,
        DurationSeconds = track.DurationSeconds,
        CoverArtUrl = track.CoverArtUrl
    };
}

/// <summary>
/// Third-party metadata search result from Apple iTunes Search API (Lab 9 S2).
/// </summary>
public record ExternalMusicMetadataDto(
    string Title,
    string Artist,
    string Album,
    string CoverArtUrl,
    int ReleaseYear,
    string TrackViewUrl
);

/// <summary>
/// Playlist response model containing resolved tracks.
/// </summary>
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

/// <summary>
/// Form model to create a new playlist.
/// </summary>
public class CreatePlaylistDto
{
    [Required(ErrorMessage = "Playlist title is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string Description { get; set; } = string.Empty;

    public List<string>? TrackIds { get; set; } = new();
}

/// <summary>
/// Payload to reorder or update track IDs inside a playlist.
/// </summary>
public record UpdatePlaylistTracksDto(
    [Required] List<string> TrackIds
);

/// <summary>
/// Student credentials for POST /api/auth/login.
/// </summary>
public class LoginRequestDto
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = "student@kkaudiolab.sg";

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = "Password123!";
}

/// <summary>
/// Student registration payload for POST /api/auth/register.
/// </summary>
public class RegisterRequestDto
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full Name is required.")]
    [MinLength(2, ErrorMessage = "Full Name must be at least 2 characters.")]
    public string FullName { get; set; } = string.Empty;
}

/// <summary>
/// JWT response returned upon successful login or registration.
/// </summary>
public record AuthResponseDto(
    string Token,
    string UserId,
    string Email,
    string FullName,
    string Role
);

/// <summary>
/// Result returned from POST /api/tracks/rescan.
/// </summary>
public record RescanResultDto(
    string Message,
    int NewlyAddedCount
);

/// <summary>
/// Live audio streaming telemetry captured via JS Interop and HTTP 206 events.
/// </summary>
public class AudioTelemetryDto
{
    public double TimeToFirstFrameMs { get; set; }
    public double BufferPercent { get; set; }
    public double CurrentTime { get; set; }
    public double Duration { get; set; }
    public double LatencyMs { get; set; }
    public bool IsPrefetched { get; set; }
    public long BytesLoaded { get; set; }
}
