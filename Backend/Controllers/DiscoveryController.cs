using Backend.DTOs;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

// [EDUCATIONAL NOTE - Lab 9 S2]:
// DiscoveryController demonstrates third-party REST integration without any AI APIs.
// Students can search Apple's public iTunes catalog to find matching music metadata,
// album names, release years, and high-resolution cover artwork.
// This metadata can be used in the Blazor frontend to pre-fill the track upload and edit forms.
[ApiController]
[Route("api/[controller]")]
public class DiscoveryController : ControllerBase
{
    private readonly IMetadataDiscoveryService _discoveryService;

    public DiscoveryController(IMetadataDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    /// <summary>
    /// Searches Apple's public iTunes music directory for song metadata and album artwork.
    /// Pure non-AI REST API consumption using System.Text.Json.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<ExternalMusicMetadataDto>>> Search([FromQuery] string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Search query parameter cannot be empty." });
        }

        var results = await _discoveryService.SearchItunesMetadataAsync(query, ct);
        return Ok(results);
    }
}
