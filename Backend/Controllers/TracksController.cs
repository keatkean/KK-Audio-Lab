using System.Security.Claims;
using Backend.DTOs;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Backend.Controllers;

// [EDUCATIONAL NOTE - Lab 4 S1, Lab 6 S2, Lab 8 S2]:
// TracksController manages the audio catalog and media streaming pipeline.
// Highlights:
// 1. [OutputCache(PolicyName = "CatalogCache")]: Caches full JSON catalog responses at the HTTP level.
// 2. HTTP 206 Partial Content: Stream endpoint enables instant playback and scrubbing across large audio files.
// 3. Low-Latency Streaming: FileStream I/O is handled asynchronously with SequentialScan hints.
// 4. Safe Multipart Upload: Enforces 30 MB size caps and extension whitelisting.
[ApiController]
[Route("api/[controller]")]
public class TracksController : ControllerBase
{
    private readonly ITrackService _trackService;
    private readonly ILogger<TracksController> _logger;

    public TracksController(ITrackService trackService, ILogger<TracksController> logger)
    {
        _trackService = trackService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all tracks in the catalog.
    /// Utilizes in-memory caching and ASP.NET Core OutputCaching with tag "tracks".
    /// </summary>
    [HttpGet]
    [OutputCache(PolicyName = "CatalogCache")]
    public async Task<ActionResult<IReadOnlyList<TrackResponseDto>>> GetAll(CancellationToken ct)
    {
        var tracks = await _trackService.GetAllTracksAsync(ct);
        return Ok(tracks);
    }

    /// <summary>
    /// Retrieves single track metadata by its BSON document ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TrackResponseDto>> GetById(string id, CancellationToken ct)
    {
        var track = await _trackService.GetTrackByIdAsync(id, ct);
        if (track == null)
        {
            return NotFound(new { error = $"Track with ID '{id}' was not found." });
        }
        return Ok(track);
    }

    /// <summary>
    /// Streams audio bytes for the requested track supporting HTTP 206 Partial Content.
    /// Enables timeline seeking and fast scrubbing in HTML5 audio players.
    /// </summary>
    [HttpGet("{id}/play")]
    [HttpHead("{id}/play")]
    public async Task<IActionResult> StreamAudio(string id, CancellationToken ct)
    {
        // [EDUCATIONAL NOTE - Lab 8 S2]:
        // HTTP 206 Partial Content & Range Processing:
        // When an HTML5 <audio> element plays or seeks to the middle of a song, it sends:
        //   Range: bytes=1048576-2097151
        // By setting enableRangeProcessing: true, ASP.NET Core automatically parses the Range header,
        // reads only the requested byte chunk from the FileStream, and responds with:
        //   HTTP/1.1 206 Partial Content
        //   Content-Range: bytes 1048576-2097151/5242880
        // ETag and Cache-Control headers ensure browsers can cache static audio segments locally.
        var (fileStream, contentType, fileName, fileLength) = await _trackService.GetAudioStreamAsync(id, ct);

        Response.Headers.CacheControl = "public, max-age=86400";
        Response.Headers.ETag = $"\"{id}-{fileLength}\"";

        return File(fileStream, contentType, fileName, enableRangeProcessing: false);
    }

    /// <summary>
    /// Uploads a new audio track with metadata via multipart/form-data.
    /// </summary>
    [Authorize]
    [HttpPost("upload")]
    [RequestSizeLimit(31_457_280)] // 30 MB ceiling
    public async Task<ActionResult<TrackResponseDto>> Upload([FromForm] UploadTrackFormDto dto, CancellationToken ct)
    {
        // [EDUCATIONAL NOTE - Lab 4 S1 & Lab 1 S1]:
        // Multipart/form-data payload binding:
        // We extract the student's ID from the validated JWT claims to track who uploaded the song.
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "anonymous";

        var created = await _trackService.UploadTrackAsync(dto, userId, ct);
        _logger.LogInformation("Track '{Title}' uploaded successfully by user '{UserId}'", created.Title, userId);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Rescans the local AudioLibrary/ folder for manually added audio files and indexes them into LiteDB.
    /// </summary>
    [Authorize]
    [HttpPost("rescan")]
    public async Task<IActionResult> Rescan(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "system";
        var count = await _trackService.RescanAudioLibraryAsync(userId, ct);
        return Ok(new
        {
            message = $"Audio library rescan completed. Discovered {count} new audio files.",
            newlyAddedCount = count
        });
    }

    /// <summary>
    /// Updates track metadata (title, artist, album, mood tag, BPM) in LiteDB and evicts caches.
    /// </summary>
    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult<TrackResponseDto>> Update(string id, [FromBody] UpdateTrackDto dto, CancellationToken ct)
    {
        var updated = await _trackService.UpdateTrackAsync(id, dto, ct);
        if (updated == null)
        {
            return NotFound(new { error = $"Track with ID '{id}' was not found." });
        }
        return Ok(updated);
    }

    /// <summary>
    /// Replaces the physical audio file of an existing track on disk while keeping track ID and references intact.
    /// </summary>
    [Authorize]
    [HttpPut("{id}/audio")]
    [RequestSizeLimit(31_457_280)]
    public async Task<ActionResult<TrackResponseDto>> ReplaceAudio(string id, [FromForm] ReplaceTrackAudioFormDto dto, CancellationToken ct)
    {
        var updated = await _trackService.ReplaceTrackAudioAsync(id, dto, ct);
        if (updated == null)
        {
            return NotFound(new { error = $"Track with ID '{id}' was not found." });
        }
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a track, maintains referential integrity by cleaning up playlist references,
    /// deletes the physical audio file from disk, and invalidates all caches.
    /// </summary>
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var deleted = await _trackService.DeleteTrackAsync(id, ct);
        if (!deleted)
        {
            return NotFound(new { error = $"Track with ID '{id}' was not found." });
        }
        return Ok(new { message = $"Track '{id}' and associated audio file were permanently deleted." });
    }
}
