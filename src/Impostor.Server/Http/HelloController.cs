using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Games;
using Impostor.Api.Games.Managers;
using Impostor.Api.Innersloth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Http;

/// <summary>
/// Generate a diagnostic page to show that the Impostor HTTP server is working.
/// </summary>
[Route("/")]
public sealed class HelloController : ControllerBase
{
    private static bool _shownHello = false;
    private readonly ILogger<HelloController> _logger;
    private readonly IGameManager _gameManager;
    private static readonly object _fileLock = new object();

    public HelloController(ILogger<HelloController> logger, IGameManager gameManager)
    {
        _logger = logger;
        _gameManager = gameManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetHello()
    {
        if (!_shownHello)
        {
            _shownHello = true;
            _logger.LogInformation("NImpostor's Http server is reachable (this message is only printed once per start)");
        }

        var allGames = _gameManager.Games.ToList();
        var publicGames = allGames.Where(game => game.IsPublic).ToList();
        var activeGames = allGames.Where(game => game.GameState == GameStates.Started).ToList();
        var waitingGames = allGames.Where(game => game.GameState == GameStates.NotStarted && game.PlayerCount < game.Options.MaxPlayers).ToList();

        var totalPlayers = allGames.Sum(game => game.PlayerCount);
        var totalPublicPlayers = publicGames.Sum(game => game.PlayerCount);

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Page", "index.html");
        var directoryPath = Path.GetDirectoryName(filePath);

        try
        {
            lock (_fileLock)
            {
                if (!Directory.Exists(directoryPath))
                {
                    _logger.LogInformation("Creating directory: {DirectoryPath}", directoryPath);
                    Directory.CreateDirectory(directoryPath);
                }
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to create directory: {DirectoryPath}", directoryPath);
            return StatusCode(500, "Failed to create page directory");
        }

        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("HTML file not found, creating default: {FilePath}", filePath);
            try
            {
                lock (_fileLock)
                {
                    var defaultHtmlContent = GetDefaultHtmlContent();
                    System.IO.File.WriteAllText(filePath, defaultHtmlContent, Encoding.UTF8);
                    _logger.LogInformation("Default HTML file created: {FilePath}", filePath);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to create default HTML file: {FilePath}", filePath);
                return await ReturnDefaultContent(allGames.Count);
            }
        }

        string htmlContent;
        try
        {
            htmlContent = await System.IO.File.ReadAllTextAsync(filePath, Encoding.UTF8);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to read HTML file: {FilePath}", filePath);
            return await ReturnDefaultContent(allGames.Count);
        }
        htmlContent = htmlContent.Replace("{allGames.Count}", allGames.Count.ToString());
        return Content(htmlContent, "text/html");
    }

    private async Task<ContentResult> ReturnDefaultContent(int gameCount)
    {
        var defaultHtml = GetDefaultHtmlContent();
        defaultHtml = defaultHtml.Replace("[gameCount]", gameCount.ToString());
        return Content(defaultHtml, "text/html");
    }

    private string GetDefaultHtmlContent()
    {
        return @"<!DOCTYPE html>
<html lang=""zh"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>NImpostor Server Index</title>
</head>
<body>
The NImpostor server is running...
Now Games : [gameCount]
</body>
</html>";
    }
}
