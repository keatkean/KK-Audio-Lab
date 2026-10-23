using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Backend.DTOs;

// [EDUCATIONAL NOTE - Lab 4 S1]:
// UploadTrackFormDto binds to 'multipart/form-data' HTTP requests using ASP.NET Core's [FromForm].
// Audio files can be tens of megabytes; IFormFile gives us a stream-backed wrapper around the
// incoming HTTP request body without needing to buffer the whole binary file into a byte[] in memory.
public class UploadTrackFormDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Artist { get; set; } = string.Empty;

    public string Album { get; set; } = "Single";

    public string MoodTag { get; set; } = "Chill";

    [Range(20, 300)]
    public int Bpm { get; set; } = 120;

    [Range(32, 1411)]
    public int BitrateKbps { get; set; } = 320;

    [Range(1, 7200)]
    public int DurationSeconds { get; set; } = 180;

    public string? CoverArtUrl { get; set; }

    [Required]
    public IFormFile AudioFile { get; set; } = null!;
}

// [EDUCATIONAL NOTE - Lab 6 S2]:
// UpdateTrackDto represents metadata-only modifications (PUT /api/tracks/{id}).
// It avoids re-uploading large audio binaries when the user simply fixes a typo in
// the track's title, artist, or mood.
public class UpdateTrackDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Artist { get; set; } = string.Empty;

    public string Album { get; set; } = "Single";

    public string MoodTag { get; set; } = "Chill";

    [Range(20, 300)]
    public int Bpm { get; set; } = 120;

    [Range(32, 1411)]
    public int BitrateKbps { get; set; } = 320;

    [Range(1, 7200)]
    public int DurationSeconds { get; set; } = 180;

    public string? CoverArtUrl { get; set; }
}

// [EDUCATIONAL NOTE - Lab 1 S1]:
// ReplaceTrackAudioFormDto handles physical audio file replacement without changing
// the track's BSON ID or breaking playlist references.
public class ReplaceTrackAudioFormDto
{
    [Required]
    public IFormFile NewAudioFile { get; set; } = null!;
}

// [EDUCATIONAL NOTE - Lab 6 S2]:
// TrackResponseDto flattens the embedded BSON sub-document 'AudioMetadata' into a clean,
// consumer-friendly shape and adds a computed 'StreamUrl' pointing directly to our
// HTTP 206 byte-range streaming controller endpoint.
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

// [EDUCATIONAL NOTE - Lab 9 S2]:
// DTO mapping external JSON responses from the public iTunes Search API.
// No AI is involved; we parse standard third-party public metadata to auto-populate album art.
public record ExternalMusicMetadataDto(
    string Title,
    string Artist,
    string Album,
    string CoverArtUrl,
    int ReleaseYear,
    string TrackViewUrl
);
