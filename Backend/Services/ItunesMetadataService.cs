using System.Text.Json;
using Backend.DTOs;
using Backend.Services.Interfaces;

namespace Backend.Services;

// [EDUCATIONAL NOTE - Lab 9 S2]:
// Third-Party REST API Integration with HttpClient:
// In this lab, students learn how backend microservices communicate with external third-party
// REST web services without using any Generative AI.
// We call Apple's public iTunes Search REST endpoint to retrieve song metadata and high-res cover art.
// We use IHttpClientFactory to prevent socket exhaustion and parse the JSON payload using System.Text.Json.
public class ItunesMetadataService : IMetadataDiscoveryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ItunesMetadataService> _logger;

    public ItunesMetadataService(HttpClient httpClient, ILogger<ItunesMetadataService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExternalMusicMetadataDto>> SearchItunesMetadataAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ExternalMusicMetadataDto>();
        }

        try
        {
            var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&entity=song&limit=10";
            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("iTunes API returned non-success status: {StatusCode}", response.StatusCode);
                return Array.Empty<ExternalMusicMetadataDto>();
            }

            var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var jsonDoc = await JsonDocument.ParseAsync(contentStream, cancellationToken: ct);

            if (!jsonDoc.RootElement.TryGetProperty("results", out var resultsElem) || resultsElem.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ExternalMusicMetadataDto>();
            }

            var list = new List<ExternalMusicMetadataDto>();

            foreach (var item in resultsElem.EnumerateArray())
            {
                var title = item.TryGetProperty("trackName", out var tProp) ? tProp.GetString() ?? "" : "";
                var artist = item.TryGetProperty("artistName", out var aProp) ? aProp.GetString() ?? "" : "";
                var album = item.TryGetProperty("collectionName", out var cProp) ? cProp.GetString() ?? "" : "Single";
                var artUrl = item.TryGetProperty("artworkUrl100", out var artProp) ? artProp.GetString() ?? "" : "";
                var trackView = item.TryGetProperty("trackViewUrl", out var tvProp) ? tvProp.GetString() ?? "" : "";

                // Upgrade artwork resolution from 100x100 to 600x600 for sharp display on modern screens
                if (!string.IsNullOrEmpty(artUrl))
                {
                    artUrl = artUrl.Replace("100x100bb.jpg", "600x600bb.jpg");
                }

                var releaseYear = DateTime.UtcNow.Year;
                if (item.TryGetProperty("releaseDate", out var rdProp) && DateTime.TryParse(rdProp.GetString(), out var parsedDate))
                {
                    releaseYear = parsedDate.Year;
                }

                if (!string.IsNullOrWhiteSpace(title))
                {
                    list.Add(new ExternalMusicMetadataDto(title, artist, album, artUrl, releaseYear, trackView));
                }
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query external iTunes Search API for query '{Query}'", query);
            // Return empty list so the application degrades gracefully in offline lab environments
            return Array.Empty<ExternalMusicMetadataDto>();
        }
    }
}
