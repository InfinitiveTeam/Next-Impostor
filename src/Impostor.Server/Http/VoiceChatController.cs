using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;

namespace Impostor.Server.Http;

[ApiController]
public sealed class VoiceChatController : ControllerBase
{
    [HttpGet("/voice")]
    public ContentResult Index()
    {
        var indexPath = Path.Combine(Directory.GetCurrentDirectory(), "index.html");
        var html = System.IO.File.Exists(indexPath)
            ? System.IO.File.ReadAllText(indexPath)
            : "<html><body><h1>Interstellar voice service is running</h1><p>WebSocket endpoint: /vc</p></body></html>";

        return Content(html, "text/html");
    }

    [HttpGet("/voice/health")]
    public IActionResult Health()
    {
        return Ok(new { status = "ok" });
    }

    [HttpGet("/VoiceChatPlugin.dll")]
    [HttpGet("/voice/VoiceChatPlugin.dll")]
    public IActionResult DownloadPlugin()
    {
        var dllPath = Path.Combine(Directory.GetCurrentDirectory(), "VoiceChatPlugin.dll");
        if (!System.IO.File.Exists(dllPath))
        {
            return NotFound("VoiceChatPlugin.dll not found on server.");
        }

        var stream = System.IO.File.OpenRead(dllPath);
        return File(stream, "application/octet-stream", "VoiceChatPlugin.dll");
    }
}
