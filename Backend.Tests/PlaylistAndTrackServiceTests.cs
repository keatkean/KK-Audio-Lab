using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Backend.Tests;

// [EDUCATIONAL NOTE - Lab 14 S1]:
// In-Memory Integration & Unit Testing with xUnit and LiteDB:
// Instead of mocking dozens of database calls or running a heavy external Docker SQL container,
// LiteDB can run entirely in RAM by passing 'new MemoryStream()' into the LiteDatabase constructor.
// Combined with a localized temporary directory on disk, tests run in milliseconds with 100%
// real NoSQL BSON serialization, true file streaming I/O, and full cache behavior.
public class PlaylistAndTrackServiceTests : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly MemoryCache _memoryCache;
    private readonly string _tempAudioDir;
    private readonly TrackService _trackService;
    private readonly PlaylistService _playlistService;

    public PlaylistAndTrackServiceTests()
    {
        // 1. In-memory LiteDB instance
        _db = new LiteDatabase(new MemoryStream());

        // 2. Real in-memory cache instance
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        // 3. Isolated temp directory for disk file I/O tests
        _tempAudioDir = Path.Combine(Path.GetTempPath(), "KkAudioLabTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempAudioDir);

        // 4. Instantiate services
        _trackService = new TrackService(_db, _memoryCache, outputCacheStore: null, _tempAudioDir);
        _playlistService = new PlaylistService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _memoryCache.Dispose();

        try
        {
            if (Directory.Exists(_tempAudioDir))
            {
                Directory.Delete(_tempAudioDir, recursive: true);
            }
        }
        catch
        {
            // Suppress cleanup file lock exceptions in test teardown
        }
    }

    private static IFormFile CreateMockAudioFile(string fileName, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "AudioFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = fileName.EndsWith(".mp3") ? "audio/mpeg" : "audio/wav"
        };
    }

    // ---------------------------------------------------------------------------
    // TEST 1: Uploading/Creating a track writes the physical file and BSON document
    // ---------------------------------------------------------------------------
    // [EDUCATIONAL NOTE - Lab 14 S1]:
    // Verifies that the multipart form upload endpoint streams the audio bytes onto disk
    // and saves the corresponding BSON metadata document into the LiteDB 'tracks' collection.
    [Fact]
    public async Task UploadTrack_WritesPhysicalFile_AndInsertsBsonDocument()
    {
        // Arrange
        var fakeBytes = "ID3-MOCK-AUDIO-PAYLOAD"u8.ToArray();
        var formFile = CreateMockAudioFile("marinabay.mp3", fakeBytes);

        var dto = new UploadTrackFormDto
        {
            Title = "Marina Bay Chill",
            Artist = "KK Audio Lab Collective",
            Album = "Singapore Nights",
            MoodTag = "Chill",
            Bpm = 115,
            BitrateKbps = 320,
            DurationSeconds = 190,
            AudioFile = formFile
        };

        // Act
        var result = await _trackService.UploadTrackAsync(dto, "user_student_1");

        // Assert: BSON document exists in LiteDB
        var tracksCol = _db.GetCollection<Track>("tracks");
        var savedTrack = tracksCol.FindById(result.Id);

        Assert.NotNull(savedTrack);
        Assert.Equal("Marina Bay Chill", savedTrack.Title);
        Assert.Equal("KK Audio Lab Collective", savedTrack.Artist);
        Assert.Equal("user_student_1", savedTrack.UploadedByUserId);
        Assert.Equal("Chill", savedTrack.AudioMetadata.MoodTag);

        // Assert: Physical audio file exists in the audio storage directory
        var physicalFilePath = Path.Combine(_tempAudioDir, savedTrack.FileName);
        Assert.True(File.Exists(physicalFilePath), "Expected physical file to be created in AudioLibrary.");

        var writtenBytes = await File.ReadAllBytesAsync(physicalFilePath);
        Assert.Equal(fakeBytes, writtenBytes);
    }

    // ---------------------------------------------------------------------------
    // TEST 2: Reading tracks populates IMemoryCache
    // ---------------------------------------------------------------------------
    // [EDUCATIONAL NOTE - Lab 14 S1]:
    // Verifies that calling GetAllTracksAsync queries LiteDB on cold start,
    // formats the results, and sets the 'tracks_catalog' key in IMemoryCache.
    [Fact]
    public async Task GetAllTracks_Populates_IMemoryCache()
    {
        // Arrange
        var tracksCol = _db.GetCollection<Track>("tracks");
        tracksCol.Insert(new Track
        {
            Id = "trk_cache_1",
            Title = "Changi Ambient",
            Artist = "Jewel Audio",
            FileName = "changi.mp3",
            FileSizeBytes = 1024,
            DurationSeconds = 180,
            UploadedByUserId = "user1",
            AudioMetadata = new AudioMetadata { MoodTag = "Ambient", Bpm = 80 }
        });

        const string cacheKey = "tracks_catalog";
        Assert.False(_memoryCache.TryGetValue(cacheKey, out _), "Cache must be empty initially.");

        // Act
        var tracks = await _trackService.GetAllTracksAsync();

        // Assert
        Assert.Single(tracks);
        Assert.Equal("Changi Ambient", tracks[0].Title);

        // Verify cache entry is now populated
        Assert.True(_memoryCache.TryGetValue(cacheKey, out IReadOnlyList<TrackResponseDto>? cachedTracks));
        Assert.NotNull(cachedTracks);
        Assert.Single(cachedTracks);
        Assert.Equal("Changi Ambient", cachedTracks[0].Title);
    }

    // ---------------------------------------------------------------------------
    // TEST 3: Updating a track modifies BSON fields and invalidates cache
    // ---------------------------------------------------------------------------
    // [EDUCATIONAL NOTE - Lab 14 S1]:
    // Tests that updating metadata modifies the underlying BSON document and
    // immediately removes the stale catalog from IMemoryCache so users don't see old data.
    [Fact]
    public async Task UpdateTrack_ModifiesBsonFields_AndInvalidatesMemoryCache()
    {
        // Arrange: Seed track and warm up cache
        var tracksCol = _db.GetCollection<Track>("tracks");
        var track = new Track
        {
            Id = "trk_update_1",
            Title = "Original Orchard Title",
            Artist = "DJ Clark",
            FileName = "orchard.mp3",
            FileSizeBytes = 2048,
            DurationSeconds = 120,
            UploadedByUserId = "user_poly",
            AudioMetadata = new AudioMetadata { MoodTag = "Study", Bpm = 100 }
        };
        tracksCol.Insert(track);

        // Warm up cache
        await _trackService.GetAllTracksAsync();
        const string cacheKey = "tracks_catalog";
        Assert.True(_memoryCache.TryGetValue(cacheKey, out _), "Cache must be populated before update.");

        var updateDto = new UpdateTrackDto
        {
            Title = "Updated Orchard Sunrise",
            Artist = "DJ Clark (Remix)",
            Album = "Somerset Beats",
            MoodTag = "Energetic",
            Bpm = 128,
            BitrateKbps = 320,
            DurationSeconds = 135
        };

        // Act
        var updated = await _trackService.UpdateTrackAsync("trk_update_1", updateDto);

        // Assert: Returned DTO reflects changes
        Assert.NotNull(updated);
        Assert.Equal("Updated Orchard Sunrise", updated.Title);
        Assert.Equal("DJ Clark (Remix)", updated.Artist);
        Assert.Equal("Energetic", updated.MoodTag);
        Assert.Equal(128, updated.Bpm);

        // Assert: BSON document in LiteDB was updated
        var dbTrack = tracksCol.FindById("trk_update_1");
        Assert.NotNull(dbTrack);
        Assert.Equal("Updated Orchard Sunrise", dbTrack.Title);
        Assert.Equal("DJ Clark (Remix)", dbTrack.Artist);

        // Assert: Cache was evicted
        Assert.False(_memoryCache.TryGetValue(cacheKey, out _), "Cache must be evicted after an update.");
    }

    // ---------------------------------------------------------------------------
    // TEST 4: Deleting a track cleans BSON record, playlist links, and disk file
    // ---------------------------------------------------------------------------
    // [EDUCATIONAL NOTE - Lab 14 S1]:
    // Tests referential integrity in NoSQL:
    // Deleting a track must prune the track's [BsonRef] entry from all playlists,
    // remove the Track document from LiteDB, and delete the physical file on disk.
    [Fact]
    public async Task DeleteTrack_RemovesBson_CleansPlaylistBsonRefs_AndDeletesPhysicalFile()
    {
        // Arrange
        var testFileName = "tiong_bahru_lofi.mp3";
        var fullFilePath = Path.Combine(_tempAudioDir, testFileName);
        await File.WriteAllBytesAsync(fullFilePath, "SAMPLE-AUDIO-BYTES"u8.ToArray());

        var track = new Track
        {
            Id = "trk_del_1",
            Title = "Tiong Bahru Lo-Fi",
            Artist = "Red Dot Beats",
            FileName = testFileName,
            FileSizeBytes = 18,
            DurationSeconds = 150,
            UploadedByUserId = "user_student",
            AudioMetadata = new AudioMetadata { MoodTag = "Chill", Bpm = 85 }
        };
        _db.GetCollection<Track>("tracks").Insert(track);

        // Create playlist referencing the track via [BsonRef]
        var playlist = new Playlist
        {
            Id = "pl_del_1",
            Title = "Polytechnic Study List",
            UserId = "user_student",
            Tracks = new List<Track> { track },
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.GetCollection<Playlist>("playlists").Insert(playlist);

        // Pre-condition check
        Assert.True(File.Exists(fullFilePath), "Physical file must exist before delete.");
        var preCheckPlaylist = _db.GetCollection<Playlist>("playlists").Include(p => p.Tracks).FindById("pl_del_1");
        Assert.Single(preCheckPlaylist.Tracks);

        // Act
        var deleted = await _trackService.DeleteTrackAsync("trk_del_1");

        // Assert
        Assert.True(deleted);

        // 1. LiteDB track record is gone
        var trackInDb = _db.GetCollection<Track>("tracks").FindById("trk_del_1");
        Assert.Null(trackInDb);

        // 2. Physical file was deleted from disk
        Assert.False(File.Exists(fullFilePath), "Physical audio file should be deleted from disk.");

        // 3. Playlist had the deleted track ref removed (Referential Integrity)
        var postCheckPlaylist = _db.GetCollection<Playlist>("playlists").Include(p => p.Tracks).FindById("pl_del_1");
        Assert.Empty(postCheckPlaylist.Tracks);
    }

    // ---------------------------------------------------------------------------
    // TEST 5: Unauthorized user cannot delete another user's playlist
    // ---------------------------------------------------------------------------
    // [EDUCATIONAL NOTE - Lab 14 S1]:
    // Verifies multi-tenant resource authorization:
    // If student 'alice' owns a playlist, student 'bob' attempting to delete it
    // must be rejected with an UnauthorizedAccessException.
    [Fact]
    public async Task DeletePlaylist_ByUnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var playlist = new Playlist
        {
            Id = "pl_secret_1",
            Title = "Alice's Private Study Mix",
            UserId = "user_student_alice",
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.GetCollection<Playlist>("playlists").Insert(playlist);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _playlistService.DeletePlaylistAsync("pl_secret_1", "user_student_bob")
        );

        Assert.Contains("cannot delete another user's playlist", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Verify playlist still exists in LiteDB
        var survivingPlaylist = _db.GetCollection<Playlist>("playlists").FindById("pl_secret_1");
        Assert.NotNull(survivingPlaylist);
        Assert.Equal("Alice's Private Study Mix", survivingPlaylist.Title);
    }

    // ---------------------------------------------------------------------------
    // TEST 6: HealthController returns Healthy status and system checks
    // ---------------------------------------------------------------------------
    // [EDUCATIONAL NOTE - Lab 14 S1]:
    // Tests that the health check endpoint correctly interrogates LiteDB collections
    // and physical audio storage, returning an HTTP 200 OK with 'Healthy' status.
    [Fact]
    public void HealthCheck_ReturnsHealthyStatus_AndSystemChecks()
    {
        // Arrange
        var config = new Microsoft.Extensions.Configuration.ConfigurationManager();
        config["AudioLibraryPath"] = _tempAudioDir;

        var controller = new Backend.Controllers.HealthController(_db, config);

        // Act
        var result = controller.GetHealth() as Microsoft.AspNetCore.Mvc.OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);

        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("KK Audio Lab WebAPI", doc.RootElement.GetProperty("service").GetString());
    }
}
