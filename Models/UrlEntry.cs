using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ShortURL.Models;

public class UrlEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("shortCode")]
    public string ShortCode { get; set; } = string.Empty;

    [BsonElement("originalUrl")]
    public string OriginalUrl { get; set; } = string.Empty;

    [BsonElement("createdBy")]
    public ulong CreatedBy { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("clickCount")]
    public long ClickCount { get; set; } = 0;

    [BsonElement("lastAccessed")]
    public DateTime? LastAccessed { get; set; }
}
