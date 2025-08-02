using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShortURL.Services;

namespace ShortURL.Controllers;

public class HomeController : Controller
{
    private readonly IUrlService _urlService;

    public HomeController(IUrlService urlService)
    {
        _urlService = urlService;
    }

    public async Task<IActionResult> Index()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return View("Login");
        }

        var userId = ulong.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var urls = await _urlService.GetUserUrlsAsync(userId);
        
        return View(urls);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateShortUrl(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            TempData["Error"] = "Please enter a valid URL";
            return RedirectToAction("Index");
        }

        try
        {
            var userId = ulong.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var shortCode = await _urlService.CreateShortUrlAsync(originalUrl, userId);
            
            var shortUrl = $"{Request.Scheme}://{Request.Host}/{shortCode}";
            TempData["Success"] = $"Short URL created: {shortUrl}";
            TempData["ShortUrl"] = shortUrl;
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while creating the short URL";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> DeleteUrl(string shortCode)
    {
        var userId = ulong.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var success = await _urlService.DeleteUrlAsync(shortCode, userId);
        
        if (success)
        {
            TempData["Success"] = "URL deleted successfully";
        }
        else
        {
            TempData["Error"] = "Failed to delete URL";
        }

        return RedirectToAction("Index");
    }
}