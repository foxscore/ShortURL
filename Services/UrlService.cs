using System.Text;
using MongoDB.Driver;
using ShortURL.Models;

namespace ShortURL.Services;

public interface IUrlService
{
    Task<string> CreateShortUrlAsync(string originalUrl, ulong userId);
    Task<string?> GetOriginalUrlAsync(string shortCode);
    Task<IEnumerable<UrlEntry>> GetUserUrlsAsync(ulong userId);
    Task<bool> DeleteUrlAsync(string shortCode, ulong userId);
    Task IncrementClickCountAsync(string shortCode);
}

public class UrlService : IUrlService
{
    private static readonly string[] ValidSchemes = Environment.GetEnvironmentVariable("VALID_SCHEMES")!.ToLower().Split(',');
    private readonly IMongoCollection<UrlEntry> _urlCollection;
    private const string Characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int ShortCodeLength = 6;

    public UrlService(IMongoDatabase database)
    {
        _urlCollection = database.GetCollection<UrlEntry>("urls");
        
        // Create index on shortCode for better performance
        var indexKeysDefinition = Builders<UrlEntry>.IndexKeys.Ascending(x => x.ShortCode);
        var indexModel = new CreateIndexModel<UrlEntry>(indexKeysDefinition, new CreateIndexOptions { Unique = true });
        _urlCollection.Indexes.CreateOneAsync(indexModel);
    }

    public async Task<string> CreateShortUrlAsync(string originalUrl, ulong userId)
    {
        // Validate URL
        if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri) || 
            !ValidSchemes.Contains(uri.Scheme.ToLower()))
        {
            throw new ArgumentException("Invalid URL format");
        }

        // Check if URL already exists for this user
        var existingUrl = await _urlCollection
            .Find(x => x.OriginalUrl == originalUrl && x.CreatedBy == userId)
            .FirstOrDefaultAsync();

        if (existingUrl != null)
        {
            return existingUrl.ShortCode;
        }

        // Generate unique short code
        string shortCode;
        do
        {
            shortCode = GenerateShortCode();
            // ReSharper disable once AccessToModifiedClosure
        } while (await _urlCollection.Find(x => x.ShortCode == shortCode).AnyAsync());

        var urlEntry = new UrlEntry
        {
            ShortCode = shortCode,
            OriginalUrl = originalUrl,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _urlCollection.InsertOneAsync(urlEntry);
        return shortCode;
    }

    public async Task<string?> GetOriginalUrlAsync(string shortCode)
    {
        var urlEntry = await _urlCollection
            .Find(x => x.ShortCode == shortCode)
            .FirstOrDefaultAsync();

        return urlEntry?.OriginalUrl;
    }

    public async Task<IEnumerable<UrlEntry>> GetUserUrlsAsync(ulong userId)
    {
        return await _urlCollection
            .Find(x => x.CreatedBy == userId)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteUrlAsync(string shortCode, ulong userId)
    {
        var result = await _urlCollection.DeleteOneAsync(x => x.ShortCode == shortCode && x.CreatedBy == userId);
        return result.DeletedCount > 0;
    }

    public async Task IncrementClickCountAsync(string shortCode)
    {
        var update = Builders<UrlEntry>.Update
            .Inc(x => x.ClickCount, 1)
            .Set(x => x.LastAccessed, DateTime.UtcNow);

        await _urlCollection.UpdateOneAsync(x => x.ShortCode == shortCode, update);
    }

    private static string GenerateShortCode()
    {
        var random = new Random();
        var stringBuilder = new StringBuilder(ShortCodeLength);

        for (int i = 0; i < ShortCodeLength; i++)
        {
            stringBuilder.Append(Characters[random.Next(Characters.Length)]);
        }

        return stringBuilder.ToString();
    }
}
