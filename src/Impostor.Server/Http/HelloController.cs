using System.Linq;
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

    public HelloController(ILogger<HelloController> logger, IGameManager gameManager)
    {
        _logger = logger;
        _gameManager = gameManager;
    }

    [HttpGet]
    public IActionResult GetHello()
    {
        if (!_shownHello)
        {
            _shownHello = true;
            _logger.LogInformation("NImpostor's Http server is reachable (this message is only printed once per start)");
        }

        // 获取服务器状态数据
        var allGames = _gameManager.Games.ToList();
        var publicGames = allGames.Where(game => game.IsPublic).ToList();
        var activeGames = allGames.Where(game => game.GameState == GameStates.Started).ToList();
        var waitingGames = allGames.Where(game => game.GameState == GameStates.NotStarted && game.PlayerCount < game.Options.MaxPlayers).ToList();

        var totalPlayers = allGames.Sum(game => game.PlayerCount);
        var totalPublicPlayers = publicGames.Sum(game => game.PlayerCount);

        var htmlContent = $@"
<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>NoS Impostor - 运行中</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
            color: #e6e6e6;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}
        
        .container {{
            max-width: 900px;
            width: 100%;
            background: rgba(255, 255, 255, 0.05);
            backdrop-filter: blur(10px);
            border-radius: 20px;
            padding: 40px;
            box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
            border: 1px solid rgba(255, 255, 255, 0.1);
            text-align: center;
        }}
        
        .header {{
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 2px solid rgba(255, 255, 255, 0.1);
        }}
        
        .title {{
            font-size: 2.5em;
            font-weight: 300;
            background: linear-gradient(45deg, #4cc9f0, #4361ee);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            margin-bottom: 10px;
        }}
        
        .subtitle {{
            font-size: 1.1em;
            color: #b8b8b8;
            font-weight: 300;
        }}
        
        .status-section {{
            margin: 30px 0;
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
        }}
        
        .status-card {{
            background: rgba(255, 255, 255, 0.08);
            border-radius: 15px;
            padding: 20px;
            border: 1px solid rgba(255, 255, 255, 0.1);
            transition: transform 0.3s ease, box-shadow 0.3s ease;
        }}
        
        .status-card:hover {{
            transform: translateY(-5px);
            box-shadow: 0 10px 25px rgba(0, 0, 0, 0.2);
        }}
        
        .status-value {{
            font-size: 2em;
            font-weight: bold;
            color: #4cc9f0;
            margin: 10px 0;
        }}
        
        .status-label {{
            font-size: 0.9em;
            color: #b8b8b8;
            text-transform: uppercase;
            letter-spacing: 1px;
        }}
        
        .poetry-section {{
            margin: 40px 0;
        }}
        
        .poem {{
            font-style: italic;
            color: #d4d4d4;
            margin: 25px 0;
            padding: 20px;
            background: rgba(255, 255, 255, 0.03);
            border-radius: 15px;
            border-left: 3px solid #4361ee;
        }}
        
        .poem p {{
            margin: 10px 0;
            line-height: 1.8;
        }}
        
        .highlight {{
            color: #4cc9f0;
            font-weight: 500;
        }}
        
        .footer {{
            margin-top: 40px;
            padding-top: 20px;
            border-top: 2px solid rgba(255, 255, 255, 0.1);
            color: #a0a0a0;
            font-size: 0.9em;
        }}
        
        .heart {{
            color: #f72585;
            display: inline-block;
            animation: pulse 1.5s ease-in-out infinite;
        }}
        
        @keyframes pulse {{
            0%, 100% {{ transform: scale(1); }}
            50% {{ transform: scale(1.1); }}
        }}
        
        @media (max-width: 600px) {{
            .container {{
                padding: 20px;
            }}
            .title {{
                font-size: 2em;
            }}
            .status-section {{
                grid-template-columns: 1fr;
            }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1 class=""title"">NoS Impostor</h1>
            <p class=""subtitle"">服务器运行正常</p>
        </div>
        
        <!-- 服务器状态面板 -->
        <div class=""status-section"">
            <div class=""status-card"">
                <div class=""status-value"">{allGames.Count}</div>
                <div class=""status-label"">总游戏数</div>
            </div>
            <div class=""status-card"">
                <div class=""status-value"">{publicGames.Count}</div>
                <div class=""status-label"">公开游戏</div>
            </div>
            <div class=""status-card"">
                <div class=""status-value"">{activeGames.Count}</div>
                <div class=""status-label"">进行中游戏</div>
            </div>
            <div class=""status-card"">
                <div class=""status-value"">{waitingGames.Count}</div>
                <div class=""status-label"">等待中游戏</div>
            </div>
            <div class=""status-card"">
                <div class=""status-value"">{totalPlayers}</div>
                <div class=""status-label"">总玩家数</div>
            </div>
            <div class=""status-card"">
                <div class=""status-value"">{totalPublicPlayers}</div>
                <div class=""status-label"">公开游戏玩家</div>
            </div>
        </div>
        
        <div class=""poetry-section"">
            <div class=""poem"">
                <p>无需追逐风，因你本身便是风。</p>
            </div>
            
            <div class=""poem"">
                <p>你并非沧海一粟，</p>
                <p>而是一滴水中藏着整片海洋。</p>
            </div>
            
            <div class=""poem"">
                <p>太阳深爱着月亮，</p>
                <p>于是每晚甘愿陨落，</p>
                <p>只为让她得以闪耀。</p>
            </div>
            
            <div class=""poem"">
                <p>刹那的温柔，</p>
                <p>存于时空之中。</p>
                <p>永恒的痕迹，</p>
                <p>印在心灵之上。</p>
            </div>
            
            <div class=""poem"">
                <p>我的思绪如风暴翻涌，</p>
                <p>而你的名字，便是风暴中心的宁静。</p>
            </div>
        </div>
        
        <div class=""footer"">
            <p>由 <span class=""highlight"">FangkuaiYa</span> 倾力打造，<span class=""heart"">❤</span> 爱意满满</p>
            <p style=""margin-top: 10px;"">感谢您 <span class=""highlight"">选择并信任我们</span></p>
        </div>
    </div>
</body>
</html>";

        return Content(htmlContent, "text/html");
    }
}
