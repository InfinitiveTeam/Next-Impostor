using System.IO;
using Impostor.Api.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Http;

[ApiController]
public sealed class VoiceChatController : ControllerBase
{
    private readonly HttpServerConfig _httpServerConfig;

    public VoiceChatController(IOptions<HttpServerConfig> httpServerConfig)
    {
        _httpServerConfig = httpServerConfig.Value;
    }

    [HttpGet("/voice")]
    public ContentResult Index()
    {
        if (!_httpServerConfig.EnableVoiceChatServer)
        {
            return Content("Voice server is disabled by configuration.", "text/plain");
        }

        var indexPath = Path.Combine(Directory.GetCurrentDirectory(), "Page", "voice-index.html");
        var html = System.IO.File.Exists(indexPath)
            ? System.IO.File.ReadAllText(indexPath)
            : "<html><body><h1>Interstellar voice service is running</h1><p>WebSocket endpoint: /vc</p></body></html>";

        return Content(html, "text/html");
    }

    [HttpGet("/voice/health")]
    public IActionResult Health()
    {
        if (!_httpServerConfig.EnableVoiceChatServer)
        {
            return NotFound(new { status = "disabled" });
        }

        return Ok(new { status = "ok" });
    }

    [HttpGet("/VoiceChatPlugin.dll")]
    [HttpGet("/voice/VoiceChatPlugin.dll")]
    public IActionResult DownloadPlugin()
    {
        if (!_httpServerConfig.EnableVoiceChatServer)
        {
            return NotFound("Voice server is disabled by configuration.");
        }

        var dllPath = Path.Combine(Directory.GetCurrentDirectory(), "VoiceChatPlugin.dll");
        if (!System.IO.File.Exists(dllPath))
        {
            return NotFound("VoiceChatPlugin.dll not found on server.");
        }

        var stream = System.IO.File.OpenRead(dllPath);
        return File(stream, "application/octet-stream", "VoiceChatPlugin.dll");
    }
}
