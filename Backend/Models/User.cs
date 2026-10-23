using LiteDB;

namespace Backend.Models;

// [EDUCATIONAL NOTE - Lab 2 S2]:
// In NoSQL document stores like LiteDB, entities are stored directly as BSON documents.
// The [BsonId] attribute marks the primary key. Using a string ID (such as a UUID) avoids
// cross-platform auto-increment sequence collisions and makes distributed identifiers trivial.
//
// [EDUCATIONAL NOTE - Lab 8 S1]:
// Notice that we store 'PasswordHash' rather than plaintext passwords. In modern cybersecurity,
// passwords must be salted and hashed using adaptive algorithms like BCrypt before touching
// any persistent storage.
public class User
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = "Student";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
