using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    private static bool _shownHello;
    private static readonly object FileLock = new();
    private static readonly object CpuLock = new();
    private static DateTime _lastCpuTimeCheck = DateTime.UtcNow;
    private static TimeSpan _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;

    private readonly ILogger<HelloController> _logger;
    private readonly IGameManager _gameManager;

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

        var process = Process.GetCurrentProcess();
        var cpuUsage = GetCpuUsage(process);
        var memoryMb = process.WorkingSet64 / 1024 / 1024;
        var uptime = DateTime.Now - process.StartTime;
        var uptimeString = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";

        var values = new Dictionary<string, string>
        {
            ["allGames.Count"] = allGames.Count.ToString(),
            ["publicGames.Count"] = publicGames.Count.ToString(),
            ["activeGames.Count"] = activeGames.Count.ToString(),
            ["waitingGames.Count"] = waitingGames.Count.ToString(),
            ["totalPlayers"] = totalPlayers.ToString(),
            ["totalPublicPlayers"] = totalPublicPlayers.ToString(),
            ["cpuUsage"] = cpuUsage.ToString("F1"),
            ["memoryMB"] = memoryMb.ToString(),
            ["uptimeString"] = uptimeString,
        };

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Page", "index.html");
        EnsurePageDirectory(filePath);

        if (!System.IO.File.Exists(filePath))
        {
            lock (FileLock)
            {
                if (!System.IO.File.Exists(filePath))
                {
                    System.IO.File.WriteAllText(filePath, BuildDefaultHtml(values), Encoding.UTF8);
                }
            }
        }

        try
        {
            var htmlContent = await System.IO.File.ReadAllTextAsync(filePath, Encoding.UTF8);
            return Content(ApplyTemplate(htmlContent, values), "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read home page template: {FilePath}", filePath);
            return Content(BuildDefaultHtml(values), "text/html");
        }
    }

    private static void EnsurePageDirectory(string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath);
        if (directoryPath == null)
        {
            return;
        }

        lock (FileLock)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }

    private static string ApplyTemplate(string htmlTemplate, IDictionary<string, string> values)
    {
        var result = htmlTemplate;
        foreach (var pair in values)
        {
            result = result.Replace($"{{{pair.Key}}}", pair.Value, StringComparison.Ordinal);
            result = result.Replace($"[{pair.Key}]", pair.Value, StringComparison.Ordinal);
        }

        return result;
    }

    private static string BuildDefaultHtml(IDictionary<string, string> values)
    {
        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1.0"" />
  <title>NoS Impostor · 服务器状态</title>
  <style>
    body {{ font-family: -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif; background:#0f172a; color:#e2e8f0; margin:0; }}
    .wrap {{ max-width:980px; margin:30px auto; padding:0 16px; }}
    .card {{ background:#111827; border:1px solid #334155; border-radius:12px; padding:16px; margin-bottom:12px; }}
    .grid {{ display:grid; grid-template-columns: repeat(auto-fit,minmax(160px,1fr)); gap:12px; }}
    .k {{ color:#94a3b8; font-size:12px; text-transform:uppercase; letter-spacing:.06em; }}
    .v {{ font-size:26px; font-weight:700; color:#c7d2fe; }}
    h1 {{ margin:0 0 14px 0; font-size:28px; }}
  </style>
</head>
<body>
  <div class=""wrap"">
    <h1>NoS Impostor · Server Online</h1>
    <div class=""grid"">
      <div class=""card""><div class=""k"">总游戏数</div><div class=""v"">{values["allGames.Count"]}</div></div>
      <div class=""card""><div class=""k"">公开游戏</div><div class=""v"">{values["publicGames.Count"]}</div></div>
      <div class=""card""><div class=""k"">进行中</div><div class=""v"">{values["activeGames.Count"]}</div></div>
      <div class=""card""><div class=""k"">等待中</div><div class=""v"">{values["waitingGames.Count"]}</div></div>
      <div class=""card""><div class=""k"">总玩家</div><div class=""v"">{values["totalPlayers"]}</div></div>
      <div class=""card""><div class=""k"">公开玩家</div><div class=""v"">{values["totalPublicPlayers"]}</div></div>
      <div class=""card""><div class=""k"">CPU</div><div class=""v"">{values["cpuUsage"]}%</div></div>
      <div class=""card""><div class=""k"">内存</div><div class=""v"">{values["memoryMB"]} MB</div></div>
      <div class=""card""><div class=""k"">运行时间</div><div class=""v"" style=""font-size:18px"">{values["uptimeString"]}</div></div>
    </div>
  </div>
</body>
</html>";
    }

    private static double GetCpuUsage(Process process)
    {
        lock (CpuLock)
        {
            var currentCpuTime = process.TotalProcessorTime;
            var currentTime = DateTime.UtcNow;
            var cpuUsedMs = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            var totalMsPassed = (currentTime - _lastCpuTimeCheck).TotalMilliseconds;

            _lastCpuTime = currentCpuTime;
            _lastCpuTimeCheck = currentTime;

            if (totalMsPassed <= 0)
            {
                return 0;
            }

            var usage = (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;
            return usage > 100 ? 100 : usage;
        }
    }
}
