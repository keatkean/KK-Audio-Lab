using Backend.DTOs;

namespace Backend.Services.Interfaces;

// [EDUCATIONAL NOTE - Lab 9 S2]:
// IMetadataDiscoveryService defines external public API integration for metadata enrichment.
// By abstracting HttpClient consumption behind an interface, the rest of the application
// stays decoupled from the remote API's URL, query syntax, and rate limits.
public interface IMetadataDiscoveryService
{
    Task<IReadOnlyList<ExternalMusicMetadataDto>> SearchItunesMetadataAsync(string query, CancellationToken ct = default);
}
