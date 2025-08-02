using MongoDB.Bson.Serialization.Attributes;

namespace ShortURL.Models;

public class User
{
    [BsonId]
    public ulong Id { get; set; }

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
