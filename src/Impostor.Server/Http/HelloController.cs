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
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>NoS Impostor · 服务器状态</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            background: radial-gradient(circle at 10% 20%, #1b2b4e, #0b1622);
            color: #e9f1fc;
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            line-height: 1.5;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 1.5rem;
        }}

        .dashboard {{
            max-width: 1200px;
            width: 100%;
            background: rgba(15, 25, 40, 0.6);
            backdrop-filter: blur(12px);
            -webkit-backdrop-filter: blur(12px);
            border-radius: 2rem;
            padding: 2.5rem 2rem;
            box-shadow: 0 30px 60px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255, 255, 255, 0.05) inset;
            border: 1px solid rgba(255, 255, 255, 0.08);
        }}

        /* 头部 */
        .header {{
            text-align: center;
            margin-bottom: 2.5rem;
        }}
        .header h1 {{
            font-size: 2.8rem;
            font-weight: 500;
            letter-spacing: -0.5px;
            background: linear-gradient(135deg, #a6d0ff, #7aa9ff);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            margin-bottom: 0.4rem;
        }}
        .header .badge {{
            display: inline-block;
            background: rgba(72, 187, 120, 0.2);
            color: #9ae6b4;
            font-weight: 500;
            font-size: 0.9rem;
            padding: 0.25rem 1rem;
            border-radius: 30px;
            border: 1px solid rgba(72, 187, 120, 0.3);
            backdrop-filter: blur(4px);
            margin-top: 0.25rem;
        }}

        /* 网格区域 */
        .grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 1.2rem;
            margin-bottom: 2.5rem;
        }}

        /* 卡片通用样式 */
        .card {{
            background: rgba(255, 255, 255, 0.04);
            border-radius: 1.5rem;
            padding: 1.5rem 1rem;
            text-align: center;
            border: 1px solid rgba(255, 255, 255, 0.03);
            transition: transform 0.2s ease, background 0.2s ease, box-shadow 0.2s ease;
            backdrop-filter: blur(4px);
            box-shadow: 0 8px 20px rgba(0, 0, 0, 0.2);
        }}
        .card:hover {{
            background: rgba(255, 255, 255, 0.07);
            transform: translateY(-4px);
            box-shadow: 0 16px 30px rgba(0, 0, 0, 0.4);
            border-color: rgba(255, 255, 255, 0.1);
        }}
        .card .icon {{
            font-size: 2rem;
            line-height: 1;
            margin-bottom: 0.5rem;
            opacity: 0.8;
        }}
        .card .value {{
            font-size: 2.4rem;
            font-weight: 600;
            color: #cbd5ff;
            line-height: 1.2;
        }}
        .card .label {{
            font-size: 0.85rem;
            text-transform: uppercase;
            letter-spacing: 1px;
            color: #9aaec9;
            font-weight: 400;
        }}

        /* 系统资源区域（稍作区分） */
        .section-title {{
            display: flex;
            align-items: center;
            gap: 0.5rem;
            margin: 2rem 0 1.2rem 0;
            font-size: 1.4rem;
            font-weight: 400;
            color: #cfdfee;
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
            padding-bottom: 0.5rem;
        }}
        .section-title span {{
            background: rgba(255, 255, 255, 0.03);
            padding: 0.2rem 0.8rem;
            border-radius: 30px;
            font-size: 0.85rem;
            color: #b3c7e3;
        }}

        /* 诗歌区域 */
        .poetry {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
            gap: 1.5rem;
            margin: 2.5rem 0 1.5rem;
        }}
        .poem-card {{
            background: rgba(0, 0, 0, 0.2);
            border-radius: 1.5rem;
            padding: 1.5rem;
            border-left: 3px solid #5f9eff;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
            transition: all 0.2s;
        }}
        .poem-card:hover {{
            border-left-color: #ffb86b;
            background: rgba(255, 255, 255, 0.02);
        }}
        .poem-card p {{
            font-style: italic;
            color: #d6e3f5;
            margin: 0.3rem 0;
            font-size: 1rem;
            line-height: 1.6;
        }}
        .poem-card .quote-mark {{
            font-size: 2rem;
            color: #5f9eff;
            opacity: 0.3;
            line-height: 0.8;
        }}

        /* 页脚 */
        .footer {{
            text-align: center;
            margin-top: 2.5rem;
            padding-top: 1.5rem;
            border-top: 1px solid rgba(255, 255, 255, 0.08);
            color: #a5b9d4;
            font-size: 0.95rem;
        }}
        .footer .heart {{
            color: #ff7b9c;
            display: inline-block;
            animation: heartbeat 1.5s ease-in-out infinite;
            margin: 0 0.2rem;
        }}
        @keyframes heartbeat {{
            0%, 100% {{ transform: scale(1); }}
            30% {{ transform: scale(1.2); }}
        }}
        .footer .highlight {{
            color: #b3d0ff;
            font-weight: 500;
        }}

        /* 工具类 */
        .text-glow {{
            text-shadow: 0 0 10px rgba(103, 154, 255, 0.5);
        }}
    </style>
</head>
<body>
    <div class=""dashboard"">
        <div class=""header"">
            <h1>NoS Impostor</h1>
            <div class=""badge"">● 运行正常 · 实时状态</div>
        </div>

        <!-- 游戏状态卡片组 -->
        <div class=""grid"">
            <div class=""card"">
                <div class=""icon"">🎮</div>
                <div class=""value"">{allGames.Count}</div>
                <div class=""label"">总游戏数</div>
            </div>
            <div class=""card"">
                <div class=""icon"">🌍</div>
                <div class=""value"">{publicGames.Count}</div>
                <div class=""label"">公开游戏</div>
            </div>
            <div class=""card"">
                <div class=""icon"">⚔️</div>
                <div class=""value"">{activeGames.Count}</div>
                <div class=""label"">进行中</div>
            </div>
            <div class=""card"">
                <div class=""icon"">⏳</div>
                <div class=""value"">{waitingGames.Count}</div>
                <div class=""label"">等待中</div>
            </div>
            <div class=""card"">
                <div class=""icon"">👥</div>
                <div class=""value"">{totalPlayers}</div>
                <div class=""label"">总玩家</div>
            </div>
            <div class=""card"">
                <div class=""icon"">📢</div>
                <div class=""value"">{totalPublicPlayers}</div>
                <div class=""label"">公开玩家</div>
            </div>
        </div>

        <!-- 系统资源标题 -->
        <div class=""section-title"">
            🖥️ 系统资源 <span>实时</span>
        </div>

        <!-- 系统资源卡片组 -->
        <div class=""grid"" style=""margin-bottom: 0.5rem;"">
            <div class=""card"">
                <div class=""icon"">⚙️</div>
                <div class=""value"">{cpuUsage:F1}%</div>
                <div class=""label"">CPU</div>
            </div>
            <div class=""card"">
                <div class=""icon"">🧠</div>
                <div class=""value"">{memoryMB} MB</div>
                <div class=""label"">内存</div>
            </div>
            <div class=""card"">
                <div class=""icon"">⏱️</div>
                <div class=""value"" style=""font-size: 1.8rem;"">{uptimeString}</div>
                <div class=""label"">运行时间</div>
            </div>
        </div>

        <!-- 诗歌区块 -->
        <div class=""section-title"" style=""margin-top: 2rem;"">
            📜 片刻诗意 <span>摘录</span>
        </div>

        <div class=""poetry"">
            <div class=""poem-card"">
                <div class=""quote-mark"">"" </div>
        
                        <p> 无需追逐风，因你本身便是风。</p>
        
                    </div>
        
                    <div class=""poem-card"">
                <div class=""quote-mark"">""</div>
                <p>你并非沧海一粟，<br>而是一滴水中藏着整片海洋。</p>
            </div>
            <div class=""poem-card"">
                <div class=""quote-mark"">""</div>
                <p>太阳深爱着月亮，<br>于是每晚甘愿陨落，<br>只为让她得以闪耀。</p>
            </div>
            <div class=""poem-card"">
                <div class=""quote-mark"">""</div>
                <p>刹那的温柔，存于时空之中。<br>永恒的痕迹，印在心灵之上。</p>
            </div>
            <div class=""poem-card"">
                <div class=""quote-mark"">""</div>
                <p>我的思绪如风暴翻涌，<br>而你的名字，便是风暴中心的宁静。</p>
            </div>
        </div>

        <!-- 页脚 -->
        <div class=""footer"">
            <p>由<span class=""highlight"">FangkuaiYa</span> 倾力打造<span class=""heart"">❤</span> 爱意满满</p>
            <p style = ""margin-top: 0.5rem; opacity: 0.7;"">感谢您<span class=""highlight"">选择并信任我们</span></p>
        </div>
    </div>
</body>
</html>";
    }
}
