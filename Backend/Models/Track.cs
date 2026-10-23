using LiteDB;

namespace Backend.Models;

// [EDUCATIONAL NOTE - Lab 2 S2]:
// In NoSQL databases like LiteDB, nested objects (such as AudioMetadata below) are stored
// directly inside the parent document as an embedded BSON sub-document. Unlike traditional
// SQL relational databases, we don't need a separate 'audio_metadata' table with foreign key
// joins, which drastically reduces disk I/O and query latency for audio catalog lookups.
public class AudioMetadata
{
    public string MoodTag { get; set; } = "Chill";
    public int Bpm { get; set; } = 120;
    public int BitrateKbps { get; set; } = 320;
    public string Format { get; set; } = "mp3";
}

// [EDUCATIONAL NOTE - Lab 6 S2]:
// Track represents a music entity in our local-first streaming architecture.
// Physical media files are stored on local disk inside the 'AudioLibrary/' folder,
// while LiteDB stores the metadata, file reference ('FileName'), size, and playback duration.
public class Track
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string Album { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int DurationSeconds { get; set; }

    public string CoverArtUrl { get; set; } = string.Empty;

    public string UploadedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Embedded sub-document:
    public AudioMetadata AudioMetadata { get; set; } = new();
}
