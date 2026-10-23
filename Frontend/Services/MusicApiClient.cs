using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Frontend.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Frontend.Services;

// [EDUCATIONAL NOTE - Lab 4 & Lab 8 & Lab 9]:
// Typed HttpClient in Blazor WebAssembly:
// 1. In Blazor WASM, HttpClients execute within the browser sandbox using the browser's Fetch API.
// 2. OpenReadStream: For large media uploads, browserFile.OpenReadStream() streams byte chunks
//    directly from browser memory through the fetch request body without buffering the full 30MB
//    into a WebAssembly byte[] array, preventing out-of-memory crashes on mobile devices.
// 3. Cache Synchronization: Mutating endpoints (Upload, Update, Delete, Rescan) trigger immediate
//    cache invalidation on ClientCacheService and reactive notifications on PlayerStateService.

public class MusicApiClient
{
    private readonly HttpClient _http;
    private readonly AuthStateService _authState;
    private readonly ClientCacheService _cacheService;
    private readonly PlayerStateService _playerState;
    private readonly ILogger<MusicApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MusicApiClient(
        HttpClient http,
        AuthStateService authState,
        ClientCacheService cacheService,
        PlayerStateService playerState,
        ILogger<MusicApiClient> logger)
    {
        _http = http;
        _authState = authState;
        _cacheService = cacheService;
        _playerState = playerState;
        _logger = logger;
    }

    // ---------------------------------------------------------------------------
    // TRACK CATALOG READ & STREAM
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Retrieves all tracks. Checks ClientCacheService first unless forceRefresh is true.
    /// </summary>
    public async Task<IReadOnlyList<TrackResponseDto>> GetTracksAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh)
        {
            var cached = _cacheService.GetTracks();
            if (cached != null)
            {
                return cached;
            }
        }

        try
        {
            var response = await _http.GetAsync("/api/tracks", ct);
            response.EnsureSuccessStatusCode();

            var tracks = await response.Content.ReadFromJsonAsync<List<TrackResponseDto>>(JsonOptions, ct)
                         ?? new List<TrackResponseDto>();

            _cacheService.SetTracks(tracks);
            return tracks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load music tracks from /api/tracks");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a single track by its ID.
    /// </summary>
    public async Task<TrackResponseDto?> GetTrackByIdAsync(string id, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<TrackResponseDto>($"/api/tracks/{id}", JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve track '{Id}'", id);
            return null;
        }
    }

    // ---------------------------------------------------------------------------
    // TRACK UPLOAD (CREATE) - Lab 4 & Lab 8
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Streams an audio file and metadata to POST /api/tracks/upload via MultipartFormDataContent.
    /// Uses OpenReadStream to avoid allocating a large byte array in WebAssembly memory.
    /// </summary>
    public async Task<TrackResponseDto> UploadTrackAsync(
        UploadTrackFormModel model,
        IBrowserFile audioFile,
        CancellationToken ct = default)
    {
        // [EDUCATIONAL NOTE - Lab 4 S1]:
        // Streaming upload via MultipartFormDataContent:
        // We use OpenReadStream(maxAllowedSize: 31_457_280) to allow files up to 30MB.
        // StreamContent wraps the browser's readable stream directly.
        using var content = new MultipartFormDataContent();

        content.Add(new StringContent(model.Title), "Title");
        content.Add(new StringContent(model.Artist), "Artist");
        content.Add(new StringContent(model.Album), "Album");
        content.Add(new StringContent(model.MoodTag), "MoodTag");
        content.Add(new StringContent(model.Bpm.ToString()), "Bpm");
        content.Add(new StringContent(model.BitrateKbps.ToString()), "BitrateKbps");
        content.Add(new StringContent(model.DurationSeconds.ToString()), "DurationSeconds");

        if (!string.IsNullOrWhiteSpace(model.CoverArtUrl))
        {
            content.Add(new StringContent(model.CoverArtUrl), "CoverArtUrl");
        }

        // Stream the file directly without byte[] allocation in WebAssembly
        var fileStream = audioFile.OpenReadStream(maxAllowedSize: 31_457_280);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(audioFile.ContentType) ? "audio/mpeg" : audioFile.ContentType);

        content.Add(fileContent, "AudioFile", audioFile.Name);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tracks/upload")
        {
            Content = content
        };
        await AttachAuthHeaderAsync(request);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Upload failed ({response.StatusCode}): {errorBody}");
        }

        var created = await response.Content.ReadFromJsonAsync<TrackResponseDto>(JsonOptions, ct)
                      ?? throw new InvalidOperationException("Failed to deserialize uploaded track response.");

        // Invalidate cache and update state
        _cacheService.InvalidateTracksCache();
        _cacheService.AddTrackToCache(created);

        return created;
    }

    // ---------------------------------------------------------------------------
    // RESCAN LOCAL FOLDER (POST /api/tracks/rescan)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Triggers the backend server to scan Backend/AudioLibrary/ for newly added audio files.
    /// </summary>
    public async Task<RescanResultDto> RescanLocalFolderAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tracks/rescan");
        await AttachAuthHeaderAsync(request);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RescanResultDto>(JsonOptions, ct)
                     ?? new RescanResultDto("Rescan completed", 0);

        _cacheService.InvalidateTracksCache();
        return result;
    }

    // ---------------------------------------------------------------------------
    // TRACK UPDATE (PUT /api/tracks/{id}) - Lab 6 & Lab 8
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Updates track metadata in LiteDB and updates client cache and active player live.
    /// </summary>
    public async Task<TrackResponseDto> UpdateTrackAsync(
        string id,
        UpdateTrackDto dto,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/tracks/{id}")
        {
            Content = JsonContent.Create(dto)
        };
        await AttachAuthHeaderAsync(request);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Update failed ({response.StatusCode}): {err}");
        }

        var updated = await response.Content.ReadFromJsonAsync<TrackResponseDto>(JsonOptions, ct)
                      ?? throw new InvalidOperationException("Failed to deserialize updated track response.");

        // Invalidate cache, update in memory, and update active player if currently playing
        _cacheService.InvalidateTracksCache();
        _cacheService.UpdateTrackInCache(updated);
        _playerState.UpdateCurrentTrackMetadata(id, dto);

        return updated;
    }

    // ---------------------------------------------------------------------------
    // REPLACE TRACK AUDIO (PUT /api/tracks/{id}/audio)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Replaces physical audio file for an existing track without changing its ID.
    /// </summary>
    public async Task<TrackResponseDto> ReplaceTrackAudioAsync(
        string id,
        IBrowserFile newAudioFile,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var fileStream = newAudioFile.OpenReadStream(maxAllowedSize: 31_457_280);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(newAudioFile.ContentType) ? "audio/mpeg" : newAudioFile.ContentType);

        content.Add(fileContent, "NewAudioFile", newAudioFile.Name);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/tracks/{id}/audio")
        {
            Content = content
        };
        await AttachAuthHeaderAsync(request);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<TrackResponseDto>(JsonOptions, ct)
                      ?? throw new InvalidOperationException("Failed to deserialize response.");

        _cacheService.InvalidateTracksCache();
        _cacheService.UpdateTrackInCache(updated);

        return updated;
    }

    // ---------------------------------------------------------------------------
    // TRACK DELETE (DELETE /api/tracks/{id}) - Lab 6 & Lab 8
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Permanently deletes track metadata, physical audio file on server,
    /// invalidates client cache, and cleanly halts player if track was playing.
    /// </summary>
    public async Task<bool> DeleteTrackAsync(string id, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/tracks/{id}");
        await AttachAuthHeaderAsync(request);

        var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            _cacheService.InvalidateTracksCache();
            _cacheService.RemoveTrackFromCache(id);
            _playerState.OnTrackDeleted(id);
            return true;
        }

        return false;
    }

    // ---------------------------------------------------------------------------
    // PLAYLIST CRUD - Lab 2 & Lab 6
    // ---------------------------------------------------------------------------

    public async Task<IReadOnlyList<PlaylistResponseDto>> GetPlaylistsAsync(string? userId = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(userId) ? "/api/playlists" : $"/api/playlists?userId={Uri.EscapeDataString(userId)}";
        return await _http.GetFromJsonAsync<List<PlaylistResponseDto>>(url, JsonOptions, ct) ?? new List<PlaylistResponseDto>();
    }

    public async Task<PlaylistResponseDto?> GetPlaylistByIdAsync(string id, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<PlaylistResponseDto>($"/api/playlists/{id}", JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<PlaylistResponseDto> CreatePlaylistAsync(CreatePlaylistDto dto, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/playlists")
        {
            Content = JsonContent.Create(dto)
        };
        await AttachAuthHeaderAsync(request);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PlaylistResponseDto>(JsonOptions, ct)
               ?? throw new InvalidOperationException("Failed to deserialize created playlist.");
    }

    public async Task<PlaylistResponseDto> UpdatePlaylistTracksAsync(string playlistId, List<string> trackIds, CancellationToken ct = default)
    {
        var payload = new UpdatePlaylistTracksDto(trackIds);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/playlists/{playlistId}/tracks")
        {
            Content = JsonContent.Create(payload)
        };
        await AttachAuthHeaderAsync(request);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PlaylistResponseDto>(JsonOptions, ct)
               ?? throw new InvalidOperationException("Failed to deserialize updated playlist.");
    }

    public async Task<bool> DeletePlaylistAsync(string playlistId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/playlists/{playlistId}");
        await AttachAuthHeaderAsync(request);

        var response = await _http.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    // ---------------------------------------------------------------------------
    // EXTERNAL ITUNES METADATA DISCOVERY (Non-AI) - Lab 9 S2
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Searches Apple's public iTunes API through our backend proxy (GET /api/discovery/search?query=...).
    /// Allows students to auto-fill track titles, album names, and high-res cover art.
    /// </summary>
    public async Task<IReadOnlyList<ExternalMusicMetadataDto>> SearchExternalMetadataAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<ExternalMusicMetadataDto>();

        try
        {
            var url = $"/api/discovery/search?query={Uri.EscapeDataString(query)}";
            return await _http.GetFromJsonAsync<List<ExternalMusicMetadataDto>>(url, JsonOptions, ct)
                   ?? new List<ExternalMusicMetadataDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Metadata search failed for query: {Query}", query);
            return Array.Empty<ExternalMusicMetadataDto>();
        }
    }

    // ---------------------------------------------------------------------------
    // AUTHENTICATION CLIENT (POST /api/auth/login & /register) - Lab 8 S1
    // ---------------------------------------------------------------------------

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Login failed: {err}");
        }

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions, ct)
                           ?? throw new InvalidOperationException("Failed to parse login response.");

        await _authState.LoginAsync(authResponse);
        return authResponse;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Registration failed: {err}");
        }

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions, ct)
                           ?? throw new InvalidOperationException("Failed to parse register response.");

        await _authState.LoginAsync(authResponse);
        return authResponse;
    }

    // ---------------------------------------------------------------------------
    // HELPER: JWT ATTACHMENT
    // ---------------------------------------------------------------------------
    private async Task AttachAuthHeaderAsync(HttpRequestMessage request)
    {
        var token = await _authState.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
