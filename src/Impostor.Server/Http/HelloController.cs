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
    <title>XtremeWave香港私服 - 低延迟稳定游戏体验</title>
    <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css"">
    <style>
        /* 优化CSS：合并通用样式，减少重复代码 */
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        }}
        
        body {{
            background-color: #0a192f;
            color: #fff;
            line-height: 1.6;
            background-image: 
                radial-gradient(circle at 10% 20%, rgba(30, 64, 175, 0.1) 0%, transparent 20%),
                radial-gradient(circle at 90% 80%, rgba(30, 64, 175, 0.1) 0%, transparent 20%);
            min-height: 100vh;
        }}
        
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }}
        
        header {{
            text-align: center;
            padding: 30px 0;
            position: relative;
        }}
        
        .logo {{
            display: flex;
            justify-content: center;
            align-items: center;
            gap: 15px;
            margin-bottom: 20px;
        }}
        
        .logo-icon {{
            font-size: 3.5rem;
            color: #3b82f6;
            filter: drop-shadow(0 0 10px rgba(59, 130, 246, 0.5));
        }}
        
        .logo-text {{
            font-size: 3rem;
            font-weight: 800;
            background: linear-gradient(90deg, #3b82f6, #1d4ed8);
            -webkit-background-clip: text;
            background-clip: text;
            color: transparent;
            text-shadow: 0 5px 15px rgba(0, 0, 0, 0.3);
        }}
        
        .subtitle {{
            font-size: 1.4rem;
            color: #cbd5e1;
            margin-bottom: 20px;
            max-width: 800px;
            margin-left: auto;
            margin-right: auto;
        }}
        
        .main-content {{
            display: grid;
            grid-template-columns: 1fr;
            gap: 40px;
            margin: 40px 0;
        }}
        
        @media (min-width: 769px) {{
            .main-content {{
                grid-template-columns: 1fr 1fr;
            }}
        }}
        
        /* 合并通用卡片样式 */
        .card {{
            background: rgba(15, 23, 42, 0.8);
            border-radius: 20px;
            padding: 30px;
            box-shadow: 0 10px 30px rgba(0, 0, 0, 0.3);
            border: 1px solid rgba(59, 130, 246, 0.2);
        }}
        
        .features {{
            composes: card;
        }}
        
        .section-title {{
            font-size: 1.8rem;
            margin-bottom: 25px;
            color: #60a5fa;
            display: flex;
            align-items: center;
            gap: 10px;
        }}
        
        .section-title i {{
            color: #3b82f6;
        }}
        
        .feature-list {{
            list-style-type: none;
        }}
        
        .feature-item {{
            padding: 15px 0;
            border-bottom: 1px solid rgba(255, 255, 255, 0.05);
            display: flex;
            align-items: flex-start;
            gap: 15px;
        }}
        
        .feature-item:last-child {{
            border-bottom: none;
        }}
        
        .feature-icon {{
            color: #3b82f6;
            font-size: 1.2rem;
            margin-top: 3px;
            flex-shrink: 0;
        }}
        
        .feature-title {{
            font-weight: 600;
            font-size: 1.2rem;
            margin-bottom: 5px;
            color: #e2e8f0;
        }}
        
        .feature-desc {{
            color: #94a3b8;
            font-size: 0.95rem;
        }}
        
        .download-section {{
            composes: card;
            text-align: center;
            display: flex;
            flex-direction: column;
            justify-content: center;
        }}
        
        .download-title {{
            font-size: 2.2rem;
            margin-bottom: 20px;
            color: #60a5fa;
        }}
        
        .download-desc {{
            color: #94a3b8;
            margin-bottom: 30px;
            font-size: 1.1rem;
        }}
        
        .download-btn {{
            display: inline-block;
            background: linear-gradient(90deg, #3b82f6, #1d4ed8);
            color: white;
            padding: 18px 40px;
            font-size: 1.3rem;
            font-weight: 700;
            text-decoration: none;
            border-radius: 12px;
            box-shadow: 0 8px 20px rgba(59, 130, 246, 0.4);
            transition: all 0.3s ease;
            margin-bottom: 20px;
            position: relative;
            overflow: hidden;
        }}
        
        .download-btn:hover {{
            transform: translateY(-5px);
            box-shadow: 0 12px 25px rgba(59, 130, 246, 0.6);
        }}
        
        .download-btn:active {{
            transform: translateY(0);
        }}
        
        .download-btn i {{
            margin-right: 10px;
        }}
        
        .server-info {{
            margin-top: 30px;
            text-align: left;
            background: rgba(30, 41, 59, 0.5);
            padding: 20px;
            border-radius: 12px;
        }}
        
        .server-info h3 {{
            color: #60a5fa;
            margin-bottom: 10px;
            font-size: 1.2rem;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }}
        
        .server-info h3 button {{
            background: rgba(59, 130, 246, 0.2);
            color: #60a5fa;
            border: 1px solid #3b82f6;
            padding: 5px 10px;
            border-radius: 5px;
            cursor: pointer;
            font-size: 0.8rem;
            transition: all 0.3s;
        }}
        
        .server-info h3 button:hover {{
            background: rgba(59, 130, 246, 0.4);
        }}
        
        .server-item {{
            margin-bottom: 8px;
            padding: 5px 0;
            display: flex;
            align-items: center;
        }}
        
        .server-name {{
            width: 150px;
            color: #cbd5e1;
        }}
        
        .server-status {{
            width: 60px;
            font-weight: bold;
        }}
        
        .server-status.online {{
            color: #10b981;
        }}
        
        .server-status.offline {{
            color: #ef4444;
        }}
        
        .server-status.testing {{
            color: #f59e0b;
        }}
        
        .latency-value {{
            width: 100px;
        }}
        
        .latency-bar {{
            flex-grow: 1;
            height: 8px;
            background: rgba(255, 255, 255, 0.1);
            border-radius: 4px;
            overflow: hidden;
            margin: 0 10px;
        }}
        
        .latency-fill {{
            height: 100%;
            border-radius: 4px;
            transition: width 0.5s ease;
        }}
        
        .latency-good {{
            background-color: #10b981;
        }}
        
        .latency-ok {{
            background-color: #f59e0b;
        }}
        
        .latency-poor {{
            background-color: #ef4444;
        }}
        
        .player-count {{
            margin-top: 15px;
            padding-top: 15px;
            border-top: 1px solid rgba(255, 255, 255, 0.1);
            color: #94a3b8;
            font-size: 0.9rem;
        }}
        
        footer {{
            text-align: center;
            padding: 30px 0;
            margin-top: 40px;
            border-top: 1px solid rgba(59, 130, 246, 0.2);
            color: #64748b;
            font-size: 0.9rem;
        }}
        
        .footer-links {{
            display: flex;
            justify-content: center;
            gap: 20px;
            margin-top: 15px;
            flex-wrap: wrap;
        }}
        
        .footer-links a {{
            color: #94a3b8;
            text-decoration: none;
            transition: all 0.3s;
            padding: 8px 15px;
            border-radius: 5px;
            background: rgba(30, 41, 59, 0.5);
        }}
        
        .footer-links a:hover {{
            color: #3b82f6;
            background: rgba(59, 130, 246, 0.1);
        }}
        
        .note {{
            background: rgba(59, 130, 246, 0.1);
            border-left: 4px solid #3b82f6;
            padding: 15px;
            margin-top: 30px;
            border-radius: 0 8px 8px 0;
        }}
        
        .location-badge {{
            display: inline-flex;
            align-items: center;
            background: rgba(59, 130, 246, 0.2);
            color: #60a5fa;
            padding: 8px 15px;
            border-radius: 20px;
            font-weight: 600;
            margin-bottom: 15px;
        }}
        
        .location-badge i {{
            margin-right: 8px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <header>
            <div class=""logo"">
                <i class=""fas fa-server logo-icon"" aria-hidden=""true""></i>
                <h1 class=""logo-text"">XtremeWave 香港私服</h1>
            </div>
            <div class=""location-badge"">
                <i class=""fas fa-map-marker-alt"" aria-hidden=""true""></i> 亚洲 · 中国香港
            </div>
            <p class=""subtitle"">享受极低延迟、稳定连接。</p>
        </header>
        
        <main class=""main-content"">
            <section class=""features"">
                <h2 class=""section-title""><i class=""fas fa-bolt"" aria-hidden=""true""></i> 私服特色</h2>
                <ul class=""feature-list"">
                    <li class=""feature-item"">
                        <i class=""fas fa-tachometer-alt feature-icon"" aria-hidden=""true""></i>
                        <div>
                            <h3 class=""feature-title"">低延迟</h3>
                            <p class=""feature-desc"">广东地区平均延迟低于30ms，即使是黑龙江地区也有130ms，远强于官方服务器</p>
                        </div>
                    </li>
                    <li class=""feature-item"">
                        <i class=""fas fa-user-shield feature-icon"" aria-hidden=""true""></i>
                        <div>
                            <h3 class=""feature-title"">反作弊</h3>
                            <p class=""feature-desc"">监控玩家游戏数据，自动封禁作弊玩家，维护公平游戏环境。</p>
                        </div>
                    </li>
                    <li class=""feature-item"">
                        <i class=""fas fa-headset feature-icon"" aria-hidden=""true""></i>
                        <div>
                            <h3 class=""feature-title"">车队姬</h3>
                            <p class=""feature-desc"">提供车队姬服务，帮助您组织玩家游玩您的房间，并且可备注房间信息。</p>
                        </div>
                    </li>
                </ul>
                
                <div class=""note"">
                    <p><i class=""fas fa-info-circle"" aria-hidden=""true""></i> 注：XtremeWave香港私服为独立服务器，与官方服务器不互通，仅限私服用户间联机游玩。</p>
                </div>
            </section>
            
            <section class=""download-section"">
                <h2 class=""download-title"">立即下载安装器</h2>
                <p class=""download-desc"">低延迟服务器，可联系申请车队姬服务。</p>
                
                <a href=""https://xtreme.net.cn/upload/XtremeServerInstaller.zip"" class=""download-btn"" target=""_blank"" id=""downloadBtn"" rel=""noopener"">
                    <i class=""fas fa-download"" aria-hidden=""true""></i> 下载 XW私服安装器
                </a>
                
                <p class=""version-info"">版本: 1.0.1 | 大小: 约 73.2 KB</p>
                
                <div class=""server-info"">
                    <h3><i class=""fas fa-database"" aria-hidden=""true""></i> 服务器状态 
                        <button id=""refreshPing"" type=""button"">重新检测延迟</button>
                    </h3>
                    
                    <div class=""server-item"">
                        <div class=""server-name"">香港主服务器</div>
                        <div class=""server-status online"" id=""hk-status"">● 在线</div>
                        <div class=""latency-value"" id=""hk-latency"">检测中...</div>
                        <div class=""latency-bar"">
                            <div class=""latency-fill"" id=""hk-bar"" style=""width: 0%""></div>
                        </div>
                    </div>
                </div>
            </section>
        </main>
        
        <footer>
            <p>XtremeWave © 2026 | 低延迟游戏平台</p>
            <div class=""footer-links"">
                <a href=""https://xtreme.net.cn/archives/xtremewave.hongkong-fu-wu-qi"" target=""_blank"" rel=""noopener""><i class=""fas fa-question-circle"" aria-hidden=""true""></i> 帮助中心</a>
                <a href=""https://xtreme.net.cn/archives/Server-PriPol"" target=""_blank"" rel=""noopener""><i class=""fas fa-shield-alt"" aria-hidden=""true""></i> 隐私政策</a>
                <a href=""https://xtreme.net.cn/connect"" target=""_blank"" rel=""noopener""><i class=""fas fa-envelope"" aria-hidden=""true""></i> 联系我们</a>
            </div>
        </footer>
    </div>

    <script>
        // 使用IIFE避免全局变量污染[7](@ref)
        (function() {{
            'use strict';
            
            // 缓存DOM引用，减少重复查询[6,7](@ref)
            const domCache = {{
                downloadBtn: document.getElementById('downloadBtn'),
                refreshPing: document.getElementById('refreshPing'),
                featureItems: document.querySelectorAll('.feature-item')
            }};
            
            // 服务器配置数据
            const servers = [
                {{ 
                    id: 'hk', 
                    name: '香港主服务器', 
                    url: 'https://imp.xtreme.net.cn', 
                    displayName: '香港主服务器',
                    statusEl: document.getElementById('hk-status'),
                    latencyEl: document.getElementById('hk-latency'),
                    barEl: document.getElementById('hk-bar')
                }}
            ];
            
            // 延迟检测相关变量
            let isTesting = false;
            let testTimeout = null;
            
            // 事件委托处理[6](@ref)
            document.addEventListener('click', function(e) {{
                const target = e.target;
                
                if (target.id === 'refreshPing' || target.closest('#refreshPing')) {{
                    e.preventDefault();
                    testAllServers();
                }}
            }});
            
            // 下载按钮事件处理
            domCache.downloadBtn.addEventListener('click', function(e) {{
                if (!confirm('您即将下载XtremeWave香港私服客户端，请确保从官方渠道下载。是否继续？')) {{
                    e.preventDefault();
                }} else {{
                    console.log('用户已确认下载XtremeWave香港私服客户端');
                    
                    // 使用setTimeout避免阻塞主线程[6](@ref)
                    setTimeout(function() {{
                        alert('下载已开始！如果下载没有自动开始，请检查浏览器下载列表。');
                    }}, 500);
                }}
            }});
            
            // 优化延迟检测函数[6](@ref)
            async function measureLatency(url) {{
                return new Promise((resolve) => {{
                    const startTime = performance.now();
                    const controller = new AbortController();
                    const timeoutId = setTimeout(() => controller.abort(), 5000);
                    
                    fetch(url, {{ 
                        method: 'HEAD',
                        signal: controller.signal,
                        cache: 'no-cache'
                    }}).then(response => {{
                        clearTimeout(timeoutId);
                        const endTime = performance.now();
                        const latency = Math.round(endTime - startTime);
                        resolve({{ latency, error: null }});
                    }}).catch(error => {{
                        clearTimeout(timeoutId);
                        // 备用方案：使用Image方法检测
                        measureLatencyFallback(url).then(resolve).catch(() => {{
                            resolve({{ latency: null, error: 'timeout' }});
                        }});
                    }});
                }});
            }}
            
            // 备用延迟检测方法
            function measureLatencyFallback(url) {{
                return new Promise((resolve) => {{
                    const startTime = performance.now();
                    const img = new Image();
                    const timeoutId = setTimeout(() => {{
                        img.onload = img.onerror = null;
                        resolve({{ latency: null, error: 'timeout' }});
                    }}, 5000);
                    
                    img.onload = img.onerror = () => {{
                        clearTimeout(timeoutId);
                        const endTime = performance.now();
                        const latency = Math.round(endTime - startTime);
                        resolve({{ latency, error: null }});
                    }};
                    
                    img.src = `${{url}}/favicon.ico?t=${{Date.now()}}`;
                }});
            }}
            
            // 优化服务器测试函数[6](@ref)
            async function testAllServers() {{
                if (isTesting) return;
                isTesting = true;
                
                console.log('开始测试服务器延迟...');
                
                for (const server of servers) {{
                    if (!server.statusEl || !server.latencyEl || !server.barEl) continue;
                    
                    // 更新状态为测试中
                    server.statusEl.textContent = '● 检测中...';
                    server.statusEl.className = 'server-status testing';
                    server.latencyEl.textContent = '检测中...';
                    server.barEl.style.width = '0%';
                    
                    try {{
                        const result = await measureLatency(server.url);
                        
                        if (result.error) {{
                            server.statusEl.textContent = '● 离线';
                            server.statusEl.className = 'server-status offline';
                            server.latencyEl.textContent = '不可用';
                            server.barEl.style.width = '0%';
                        }} else {{
                            const latency = result.latency;
                            server.statusEl.textContent = '● 在线';
                            server.statusEl.className = 'server-status online';
                            server.latencyEl.textContent = `${{latency}}ms`;
                            
                            // 优化延迟条更新[7](@ref)
                            updateLatencyBar(server.barEl, latency);
                        }}
                    }} catch (error) {{
                        console.error(`测试 ${{server.name}} 时出错:`, error);
                        server.statusEl.textContent = '● 错误';
                        server.statusEl.className = 'server-status offline';
                        server.latencyEl.textContent = '错误';
                        server.barEl.style.width = '0%';
                    }}
                    
                    // 添加延迟避免同时发起所有请求
                    await new Promise(resolve => setTimeout(resolve, 300));
                }}
                
                isTesting = false;
            }}
            
            // 优化延迟条更新函数
            function updateLatencyBar(barEl, latency) {{
                let width = 0;
                let barClass = 'latency-fill ';
                
                if (latency < 100) {{
                    width = 100 - (latency / 100) * 100;
                    barClass += 'latency-good';
                }} else if (latency < 300) {{
                    width = 100 - ((latency - 100) / 200) * 50;
                    barClass += 'latency-ok';
                }} else {{
                    width = 100 - ((latency - 300) / 700) * 50;
                    barClass += 'latency-poor';
                }}
                
                width = Math.max(5, Math.min(100, width));
                barEl.style.width = `${{width}}%`;
                barEl.className = barClass;
            }}
            
            // 添加动画效果[7](@ref)
            function initAnimations() {{
                domCache.featureItems.forEach((item, index) => {{
                    item.style.opacity = '0';
                    item.style.transform = 'translateY(20px)';
                    
                    setTimeout(() => {{
                        item.style.transition = 'opacity 0.5s ease, transform 0.5s ease';
                        item.style.opacity = '1';
                        item.style.transform = 'translateY(0)';
                    }}, 100 + (index * 100));
                }});
                
                // 下载按钮呼吸灯效果
                const downloadBtn = domCache.downloadBtn;
                setInterval(() => {{
                    downloadBtn.style.boxShadow = downloadBtn.style.boxShadow.includes('0 8px 20px')
                        ? '0 8px 20px rgba(59, 130, 246, 0.6)'
                        : '0 8px 20px rgba(59, 130, 246, 0.4)';
                }}, 2000);
            }}
            
            // 初始化函数
            function init() {{
                initAnimations();
                testAllServers();
                
                // 设置定时器但添加清理机制[5](@ref)
                const intervalId = setInterval(testAllServers, 30000);
                
                // 页面卸载时清理资源[5](@ref)
                window.addEventListener('beforeunload', () => {{
                    clearInterval(intervalId);
                    if (testTimeout) clearTimeout(testTimeout);
                }});
            }}
            
            // DOM加载完成后初始化
            if (document.readyState === 'loading') {{
                document.addEventListener('DOMContentLoaded', init);
            }} else {{
                init();
            }}
        }})();
    </script>
</body>
</html>";

        return Content(htmlContent, "text/html");
    }
}
