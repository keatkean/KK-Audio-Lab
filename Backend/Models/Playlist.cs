using LiteDB;

namespace Backend.Models;

// [EDUCATIONAL NOTE - Lab 2 S2]:
// Document Referencing vs Embedding:
// While 'AudioMetadata' was embedded inside 'Track' because it always belongs to that track,
// Playlists reference Tracks and Users using LiteDB's [BsonRef("...")] attribute.
// This implements a many-to-many relationship (a track can appear in multiple student playlists).
// Instead of copying the entire track data into every playlist (which would create data anomalies
// if the artist name changed), LiteDB stores just the track's ID in a BSON DBRef array.
// When querying, calling .Include(p => p.Tracks) automatically resolves and loads the tracks.
public class Playlist
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    [BsonRef("users")]
    public User? Owner { get; set; }

    [BsonRef("tracks")]
    public List<Track> Tracks { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
