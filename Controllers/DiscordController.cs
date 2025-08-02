using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json;
using ShortURL.Models;
using ShortURL.Utils;

namespace ShortURL.Controllers;

public class DiscordController(IMongoDatabase database, IWebHostEnvironment env) : Controller
{
    private readonly IMongoCollection<User> _userCollection = database.GetCollection<User>("users");
    private readonly string _clientId = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID")!;
    private readonly string _clientSecret = Environment.GetEnvironmentVariable("DISCORD_CLIENT_SECRET")!;
    private readonly bool _areSignupsAllowed = Environment.GetEnvironmentVariable("ALLOW_SIGNUP").ToBoolean();

    private readonly string _redirectUri = env.EnvironmentName switch
    {
        "Development" => "https://localhost:5001/auth/callback",
        "Production" => "https://short.foxscore.dev/auth/callback",
        _ => throw new Exception($"Invalid environment ({env.EnvironmentName})")
    };

    // Update with your domain

    // Initiate login process
    [Route("/auth/login")]
    public ActionResult Login([FromQuery] string? returnUrl)
    {
        var discordUrl =
            $"https://discord.com/api/oauth2/authorize?client_id={_clientId}&redirect_uri={Uri.EscapeDataString(_redirectUri)}&response_type=code&scope=identify%20email";
        if (!string.IsNullOrEmpty(returnUrl))
            discordUrl += $"&state={Uri.EscapeDataString(returnUrl)}";
        return Redirect(discordUrl);
    }

    // Handle callback from Discord
    [Route("/auth/callback")]
    public async Task<ActionResult> Callback(string code, string state = "")
    {
        using var httpClient = new HttpClient();
        
        // Request to exchange code for access token
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", _redirectUri),
        });

        var response = await httpClient.PostAsync("https://discord.com/api/oauth2/token", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Deserialize the response
        var tokenResponse = JsonConvert.DeserializeObject<DiscordTokenResponse>(responseContent);
        if (tokenResponse?.AccessToken == null)
            return Unauthorized("Login cancelled or failed");

        // Now use the access token to get user details
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResponse.AccessToken}");
        var userResponse = await httpClient.GetAsync("https://discord.com/api/users/@me");
        if (!userResponse.IsSuccessStatusCode)
            return Unauthorized("Failed to get user information");
        
        var userContent = await userResponse.Content.ReadAsStringAsync();
        var discordUser = JsonConvert.DeserializeObject<DiscordUser>(userContent);
        
        if (discordUser is not { IsValidUser: true })
            return Unauthorized("Invalid Discord user");

        // Check if user already exists in database
        var existingUser = await _userCollection.Find(u => u.Id == discordUser.Id).FirstOrDefaultAsync();
        if (existingUser == null)
        {
            if (!_areSignupsAllowed)
                return Unauthorized("Registrations are closed");
            
            // Create user
            var user = new User
            {
                Id = discordUser.Id,
                Email = discordUser.Email!,
                CreatedAt = DateTime.UtcNow
            };
            await _userCollection.InsertOneAsync(user);
        }

        // Login user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, discordUser.Id.ToString()),
            new(ClaimTypes.Name, discordUser.Email!),
            new(ClaimTypes.Email, discordUser.Email!),
        };
        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        await HttpContext.SignInAsync(claimsPrincipal);

        return Redirect(
            !string.IsNullOrEmpty(state) && Url.IsLocalUrl(state)
                ? state
                : "/"
        );
    }

    // Logout
    [Route("/auth/logout")]
    public async Task<ActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    // Models for deserializing responses
    private class DiscordTokenResponse
    {
        [JsonProperty("access_token")] public string? AccessToken { get; set; }
    }

    private class DiscordUser
    {
        [JsonProperty("id")] public ulong Id;
        [JsonProperty("email")] public string? Email;
        [JsonProperty("verified")] public bool? Verified;

        public bool IsValidUser => Id != 0 && Email != null && Verified == true;
    }
}
