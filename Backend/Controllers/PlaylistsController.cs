using System.Security.Claims;
using Backend.DTOs;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

// [EDUCATIONAL NOTE - Lab 2 S2 & Lab 6 S2]:
// PlaylistsController provides endpoints for managing music playlists.
// It leverages LiteDB's BsonRef referencing to link tracks to playlists,
// and ensures multi-tenant security by validating the caller's JWT user ID
// before allowing modifications or deletions.
[ApiController]
[Route("api/[controller]")]
public class PlaylistsController : ControllerBase
{
    private readonly IPlaylistService _playlistService;

    public PlaylistsController(IPlaylistService playlistService)
    {
        _playlistService = playlistService;
    }

    /// <summary>
    /// Retrieves all playlists, or playlists belonging to a specific student if userId is specified.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlaylistResponseDto>>> GetAll([FromQuery] string? userId, CancellationToken ct)
    {
        var list = await _playlistService.GetAllPlaylistsAsync(userId, ct);
        return Ok(list);
    }

    /// <summary>
    /// Retrieves a specific playlist by its ID, including fully resolved Track documents.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PlaylistResponseDto>> GetById(string id, CancellationToken ct)
    {
        var playlist = await _playlistService.GetPlaylistByIdAsync(id, ct);
        if (playlist == null)
        {
            return NotFound(new { error = $"Playlist with ID '{id}' was not found." });
        }
        return Ok(playlist);
    }

    /// <summary>
    /// Creates a new custom playlist for the authenticated student.
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<PlaylistResponseDto>> Create([FromBody] CreatePlaylistDto dto, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "User identity claim not found in JWT." });
        }

        var created = await _playlistService.CreatePlaylistAsync(dto, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates the list of track IDs contained in the playlist.
    /// Only the owner of the playlist can modify it.
    /// </summary>
    [Authorize]
    [HttpPut("{id}/tracks")]
    public async Task<ActionResult<PlaylistResponseDto>> UpdateTracks(string id, [FromBody] UpdatePlaylistTracksDto dto, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "User identity claim not found in JWT." });
        }

        var updated = await _playlistService.UpdatePlaylistTracksAsync(id, dto, userId, ct);
        if (updated == null)
        {
            return NotFound(new { error = $"Playlist with ID '{id}' was not found." });
        }
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a playlist. Enforces that only the creator student can delete their playlist.
    /// </summary>
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "User identity claim not found in JWT." });
        }

        var deleted = await _playlistService.DeletePlaylistAsync(id, userId, ct);
        if (!deleted)
        {
            return NotFound(new { error = $"Playlist with ID '{id}' was not found." });
        }
        return Ok(new { message = $"Playlist '{id}' deleted successfully." });
    }
}
