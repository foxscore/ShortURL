using Microsoft.AspNetCore.Authentication.Cookies;
using MongoDB.Driver;
using ShortURL.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Configure MongoDB
var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? "mongodb://localhost:27017";
var databaseName = Environment.GetEnvironmentVariable("MONGODB_DATABASE_NAME") ?? "urlshortener";

builder.Services.AddSingleton<IMongoClient>(provider => new MongoClient(connectionString));
builder.Services.AddScoped(provider => 
    provider.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

builder.Services.AddScoped<IUrlService, UrlService>();

// Configure Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Redirect route for shortened URLs
app.MapGet("/{shortCode}", async (string shortCode, IUrlService urlService) =>
{
    var url = await urlService.GetOriginalUrlAsync(shortCode);
    if (url != null)
    {
        await urlService.IncrementClickCountAsync(shortCode);
        return Results.Redirect(url);
    }
    return Results.NotFound("URL not found");
});

app.Run();
