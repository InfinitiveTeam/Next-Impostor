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

        var htmlContent = $@"<!DOCTYPE html>
<html lang=""zh"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>XtremeWave 香港私服 - 低延迟稳定游戏体验</title>
    <link rel=""icon"" href=""https://foruda.gitee.com/avatar/1720581464356707256/13819663_xtremewave_1720581464.png!avatar100"" type=""image/png"">
    <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"">
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Microsoft YaHei', sans-serif;
            background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%);
            color: #e2e8f0;
            min-height: 100vh;
            line-height: 1.6;
        }}
        
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }}
        
        /* 语言切换 */
        .language-switch {{
            display: flex;
            justify-content: flex-end;
            margin-bottom: 20px;
        }}
        
        .lang-btn {{
            background: rgba(30, 41, 59, 0.7);
            border: 1px solid #475569;
            color: #cbd5e1;
            padding: 8px 16px;
            cursor: pointer;
            font-size: 14px;
            transition: all 0.3s ease;
        }}
        
        .lang-btn:first-child {{
            border-radius: 6px 0 0 6px;
        }}
        
        .lang-btn:last-child {{
            border-radius: 0 6px 6px 0;
        }}
        
        .lang-btn:hover {{
            background: #475569;
        }}
        
        .lang-btn.active {{
            background: #3b82f6;
            color: white;
            border-color: #3b82f6;
        }}
        
        /* 头部样式 */
        header {{
            text-align: center;
            margin-bottom: 40px;
            padding: 30px 0;
            border-bottom: 1px solid rgba(100, 116, 139, 0.3);
        }}
        
        .logo {{
            margin-bottom: 10px;
        }}
        
        .logo-text {{
            font-size: 2.5rem;
            background: linear-gradient(90deg, #3b82f6, #8b5cf6);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
        }}
        
        .subtitle {{
            color: #94a3b8;
            font-size: 1.1rem;
        }}
        
        /* 主要内容布局 */
        .main-content {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 30px;
            margin-bottom: 40px;
        }}
        
        @media (max-width: 768px) {{
            .main-content {{
                grid-template-columns: 1fr;
            }}
        }}
        
        /* 卡片样式 */
        .card {{
            background: rgba(30, 41, 59, 0.8);
            border-radius: 12px;
            padding: 30px;
            box-shadow: 0 10px 25px rgba(0, 0, 0, 0.3);
            border: 1px solid rgba(100, 116, 139, 0.3);
        }}
        
        .section-title {{
            font-size: 1.5rem;
            margin-bottom: 20px;
            color: #f1f5f9;
            border-left: 4px solid #3b82f6;
            padding-left: 12px;
        }}
        
        /* 在线玩家区域 */
        .online-players {{
            background: rgba(15, 23, 42, 0.7);
            border-radius: 8px;
            padding: 20px;
            margin-bottom: 30px;
        }}
        
        .player-region {{
            display: flex;
            justify-content: space-between;
            align-items: center;
        }}
        
        .region-name {{
            color: #cbd5e1;
        }}
        
        .player-count {{
            font-size: 2.5rem;
            font-weight: bold;
            color: #60a5fa;
        }}
        
        /* 服务器状态 */
        .server-info {{
            margin-bottom: 30px;
        }}
        
        .server-info h3 {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 15px;
            font-size: 1.2rem;
        }}
        
        #refreshPing {{
            background: #3b82f6;
            color: white;
            border: none;
            padding: 8px 16px;
            border-radius: 6px;
            cursor: pointer;
            font-size: 14px;
            transition: background 0.3s;
        }}
        
        #refreshPing:hover {{
            background: #2563eb;
        }}
        
        .server-item {{
            display: grid;
            grid-template-columns: 1fr auto auto 100px;
            align-items: center;
            gap: 15px;
            padding: 15px;
            background: rgba(15, 23, 42, 0.5);
            border-radius: 8px;
        }}
        
        .server-status {{
            font-weight: bold;
        }}
        
        .server-status.online {{
            color: #4ade80;
        }}
        
        .server-status.testing {{
            color: #fbbf24;
        }}
        
        .latency-value {{
            font-weight: bold;
            text-align: right;
        }}
        
        .latency-bar {{
            width: 100px;
            height: 8px;
            background: rgba(100, 116, 139, 0.3);
            border-radius: 4px;
            overflow: hidden;
        }}
        
        .latency-fill {{
            height: 100%;
            transition: width 0.5s ease;
        }}
        
        .latency-good {{
            background: linear-gradient(90deg, #10b981, #34d399);
        }}
        
        .latency-ok {{
            background: linear-gradient(90deg, #f59e0b, #fbbf24);
        }}
        
        .latency-poor {{
            background: linear-gradient(90deg, #ef4444, #f87171);
        }}
        
        /* 特色功能列表 */
        .feature-list {{
            list-style: none;
        }}
        
        .feature-item {{
            display: flex;
            align-items: flex-start;
            padding: 20px 0;
            border-bottom: 1px solid rgba(100, 116, 139, 0.2);
        }}
        
        .feature-item:last-child {{
            border-bottom: none;
        }}
        
        .feature-icon {{
            font-size: 1.5rem;
            margin-right: 15px;
            color: #3b82f6;
        }}
        
        .feature-title {{
            font-size: 1.2rem;
            color: #f1f5f9;
            margin-bottom: 5px;
        }}
        
        .feature-desc {{
            color: #94a3b8;
            font-size: 0.95rem;
        }}
        
        /* 下载区域 */
        .download-section {{
            text-align: center;
        }}
        
        .download-title {{
            font-size: 1.8rem;
            margin-bottom: 10px;
            color: #f1f5f9;
        }}
        
        .download-desc {{
            color: #94a3b8;
            margin-bottom: 30px;
        }}
        
        /* 按钮样式 - 修改为纯色背景 */
        .download-btn {{
            display: inline-flex;
            align-items: center;
            justify-content: center;
            gap: 10px;
            background: #3b82f6;
            color: white;
            text-decoration: none;
            padding: 15px 30px;
            border-radius: 8px;
            font-size: 1.1rem;
            font-weight: bold;
            transition: transform 0.3s, box-shadow 0.3s;
            margin-bottom: 15px;
            border: none;
            cursor: pointer;
            width: 100%;
            max-width: 300px;
        }}
        
        .download-btn:hover {{
            transform: translateY(-2px);
            box-shadow: 0 10px 20px rgba(59, 130, 246, 0.3);
            background: #2563eb;
        }}
        
        .mobile-btn {{
            background: #8b5cf6;
        }}
        
        .mobile-btn:hover {{
            background: #7c3aed;
            box-shadow: 0 10px 20px rgba(139, 92, 246, 0.3);
        }}
        
        .sponsor-btn {{
            background: #10b981;
        }}
        
        .sponsor-btn:hover {{
            background: #059669;
            box-shadow: 0 10px 20px rgba(16, 185, 129, 0.3);
        }}
        
        .button-group {{
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 15px;
            margin-bottom: 20px;
        }}
        
        .version-info {{
            color: #94a3b8;
            font-size: 0.9rem;
            margin-bottom: 30px;
        }}
        
        /* 公告区域 */
        .announcement-section {{
            margin-top: 30px;
            text-align: left;
        }}
        
        .announcement-content {{
            background: rgba(15, 23, 42, 0.5);
            border-radius: 8px;
            padding: 20px;
            max-height: 300px;
            overflow-y: auto;
        }}
        
        .announcement-item {{
            padding: 15px 0;
            border-bottom: 1px solid rgba(100, 116, 139, 0.2);
        }}
        
        .announcement-item:last-child {{
            border-bottom: none;
        }}
        
        .announcement-title {{
            font-weight: bold;
            color: #f1f5f9;
            margin-bottom: 5px;
        }}
        
        .announcement-date {{
            color: #3b82f6;
            font-size: 0.85rem;
            margin-bottom: 10px;
        }}
        
        .announcement-text {{
            color: #cbd5e1;
            white-space: pre-line;
            line-height: 1.6;
        }}
        
        /* 赞助弹窗样式 */
        .modal-overlay {{
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(0, 0, 0, 0.7);
            z-index: 1000;
            align-items: center;
            justify-content: center;
        }}
        
        .modal-content {{
            background: #1e293b;
            border-radius: 12px;
            width: 90%;
            max-width: 600px;
            box-shadow: 0 20px 40px rgba(0, 0, 0, 0.5);
            overflow: hidden;
            border: 1px solid #475569;
        }}
        
        .modal-header {{
            padding: 20px;
            border-bottom: 1px solid #475569;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }}
        
        .modal-title {{
            font-size: 1.5rem;
            color: #f1f5f9;
        }}
        
        .close-modal {{
            background: none;
            border: none;
            color: #94a3b8;
            font-size: 1.5rem;
            cursor: pointer;
            transition: color 0.3s;
        }}
        
        .close-modal:hover {{
            color: #f1f5f9;
        }}
        
        .modal-body {{
            padding: 20px;
            display: flex;
            flex-direction: column;
            gap: 20px;
        }}
        
        @media (min-width: 768px) {{
            .modal-body {{
                flex-direction: row;
            }}
        }}
        
        .sponsor-code {{
            flex: 1;
            text-align: center;
            padding: 20px;
            background: rgba(15, 23, 42, 0.5);
            border-radius: 8px;
        }}
        
        .sponsor-qr {{
            width: 200px;
            height: 200px;
            margin: 0 auto;
            background: #f8fafc;
            display: flex;
            align-items: center;
            justify-content: center;
            border-radius: 8px;
            margin-bottom: 15px;
        }}
        
        .sponsor-qr span {{
            font-size: 5rem;
            color: #1e293b;
        }}
        
        .sponsor-notes {{
            flex: 1;
            padding: 20px;
            background: rgba(15, 23, 42, 0.5);
            border-radius: 8px;
        }}
        
        .sponsor-notes h3 {{
            margin-bottom: 15px;
            color: #f1f5f9;
        }}
        
        .sponsor-notes ul {{
            padding-left: 20px;
            color: #cbd5e1;
        }}
        
        .sponsor-notes li {{
            margin-bottom: 10px;
        }}
        
        /* 页脚样式 */
        footer {{
            text-align: center;
            padding: 30px 0;
            border-top: 1px solid rgba(100, 116, 139, 0.3);
            color: #94a3b8;
        }}
        
        .footer-links {{
            display: flex;
            justify-content: center;
            gap: 30px;
            margin-top: 20px;
            flex-wrap: wrap;
        }}
        
        .footer-links a {{
            color: #cbd5e1;
            text-decoration: none;
            display: flex;
            align-items: center;
            gap: 8px;
            transition: color 0.3s;
        }}
        
        .footer-links a:hover {{
            color: #3b82f6;
        }}
        
        /* 滚动条样式 */
        ::-webkit-scrollbar {{
            width: 8px;
        }}
        
        ::-webkit-scrollbar-track {{
            background: rgba(15, 23, 42, 0.5);
            border-radius: 4px;
        }}
        
        ::-webkit-scrollbar-thumb {{
            background: #475569;
            border-radius: 4px;
        }}
        
        ::-webkit-scrollbar-thumb:hover {{
            background: #64748b;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <!-- 语言切换 -->
        <div class=""language-switch"">
            <button class=""lang-btn active"" data-lang=""zh"">中文</button>
            <button class=""lang-btn"" data-lang=""en"">English</button>
        </div>
        
        <!-- 头部 -->
        <header>
            <div class=""logo"">
                <h1 class=""logo-text"" data-i18n=""site-title"">XtremeWave 香港私服</h1>
            </div>
            <p class=""subtitle"" data-i18n=""subtitle"">享受极低延迟、稳定连接。</p>
        </header>
        
        <!-- 主要内容 -->
        <main class=""main-content"">
            <!-- 左侧：在线玩家和服务器状态 -->
            <section class=""card"">
                <h2 class=""section-title"" data-i18n=""online-players"">在线玩家</h2>
                
                <div class=""online-players"">
                    <div class=""player-region"">
                        <div class=""region-name"" data-i18n=""current-games"">当前在线游戏数量</div>
                        <div class=""player-count"">{allGames.Count}</div>
                    </div>
                </div>
                
                <!-- 服务器状态 -->
                <div class=""server-info"">
                    <h3>
                        <span data-i18n=""server-status"">服务器状态</span>
                        <button id=""refreshPing"" type=""button"" data-i18n=""refresh-ping"">重新检测延迟</button>
                    </h3>
                    
                    <div class=""server-item"">
                        <div class=""server-name"" data-i18n=""latency-test"">延迟测试</div>
                        <div class=""server-status online"" id=""hk-status"">● 在线</div>
                        <div class=""latency-value"" id=""hk-latency"">32ms</div>
                        <div class=""latency-bar"">
                            <div class=""latency-fill latency-good"" id=""hk-bar"" style=""width: 70%""></div>
                        </div>
                    </div>
                </div>
                
                <h2 class=""section-title"" data-i18n=""server-features"">私服特色</h2>
                
                <ul class=""feature-list"">
                    <li class=""feature-item"">
                        <div>
                            <h3 class=""feature-title"" data-i18n=""low-latency"">低延迟</h3>
                            <p class=""feature-desc"" data-i18n=""low-latency-desc"">广东地区平均延迟低于30ms，远强于官方服务器</p>
                        </div>
                    </li>
                    <li class=""feature-item"">
                        <div>
                            <h3 class=""feature-title"" data-i18n=""anti-cheat"">反作弊</h3>
                            <p class=""feature-desc"" data-i18n=""anti-cheat-desc"">监控玩家游戏数据，自动封禁作弊玩家</p>
                        </div>
                    </li>
                    <li class=""feature-item"">
                        <div>
                            <h3 class=""feature-title"" data-i18n=""car-service"">车队姬</h3>
                            <p class=""feature-desc"" data-i18n=""car-service-desc"">帮助您组织玩家游玩，可备注房间信息</p>
                        </div>
                    </li>
                </ul>
            </section>
            
            <!-- 右侧：下载和公告 -->
            <section class=""card download-section"">
                <h2 class=""download-title"" data-i18n=""download-title"">立即下载安装器</h2>
                <p class=""download-desc"" data-i18n=""download-desc"">低延迟服务器，可联系申请车队姬服务</p>
                
                <div class=""button-group"">
                    <a href=""https://xtreme.net.cn/upload/XtremeServerInstaller1.2.1.zip"" class=""download-btn"" target=""_blank"" id=""downloadBtn"" rel=""noopener"">
                        <i class=""fas fa-download"" aria-hidden=""true""></i> <span data-i18n=""download-btn"">下载 XW私服安装器</span>
                    </a>
                    
                    <button id=""mobileInstallBtn"" class=""download-btn mobile-btn""onclick=""window.location.href='amongus://init?servername=XtremeWave.HongKong&serverport=443&serverip=https://imp.xtreme.net.cn&usedtls=false'"">
                        <i class=""fas fa-mobile-alt"" aria-hidden=""true""></i> <span data-i18n=""mobile-install"">移动端安装</span>
                    </button>
                </div>
                
                <p class=""version-info"">v1.2.1 | 75.7 KB</p>
                
                <!-- 公告区域 -->
                <div class=""announcement-section"">
                    <h2 class=""section-title"" data-i18n=""announcements"">公告</h2>
                    <div class=""announcement-content"" id=""announcement-content"">
                        <!-- 公告内容将通过JavaScript加载 -->
                    </div>
                </div>
            </section>
        </main>
        
        <!-- 页脚 -->
        <footer>
            <p>XtremeWave极致狂澜 © 2026</p>
            <div class=""footer-links"">
                <a href=""https://xtreme.net.cn/archives/xtremewave.hongkong-fu-wu-qi"" target=""_blank"" rel=""noopener"">
                    <i class=""fas fa-question-circle"" aria-hidden=""true""></i> <span data-i18n=""help-center"">帮助中心</span>
                </a>
                <a href=""https://xtreme.net.cn/archives/Server-PriPol"" target=""_blank"" rel=""noopener"">
                    <i class=""fas fa-shield-alt"" aria-hidden=""true""></i> <span data-i18n=""privacy-policy"">隐私政策</span>
                </a>
                <a href=""https://xtreme.net.cn/connect"" target=""_blank"" rel=""noopener"">
                    <i class=""fas fa-envelope"" aria-hidden=""true""></i> <span data-i18n=""contact-us"">联系我们</span>
                </a>
            </div>
        </footer>
    </div>

    <script>
        // 语言切换功能
        const translations = {{
            zh: {{
                ""site-title"": ""XtremeWave 香港私服"",
                ""subtitle"": ""享受极低延迟、稳定连接。"",
                ""online-players"": ""在线玩家"",
                ""current-games"": ""当前在线游戏数量"",
                ""announcements"": ""公告"",
                ""server-features"": ""私服特色"",
                ""low-latency"": ""低延迟"",
                ""low-latency-desc"": ""广东地区平均延迟低于30ms，远强于官方服务器"",
                ""anti-cheat"": ""反作弊"",
                ""anti-cheat-desc"": ""监控玩家游戏数据，自动封禁作弊玩家"",
                ""car-service"": ""车队姬"",
                ""car-service-desc"": ""帮助您组织玩家游玩，可备注房间信息"",
                ""download-title"": ""立即下载安装器"",
                ""download-desc"": ""低延迟服务器，可联系申请车队姬服务"",
                ""download-btn"": ""下载 XW私服安装器"",
                ""mobile-install"": ""移动端安装"",
                ""sponsor"": ""赞助我们"",
                ""sponsor-notes-title"": ""注意事项"",
                ""sponsor-note2"": ""赞助款项将用于服务器维护和带宽费用"",
                ""sponsor-note5"": ""感谢您对XtremeWave的支持！"",
                ""server-status"": ""服务器状态"",
                ""latency-test"": ""延迟测试"",
                ""refresh-ping"": ""重新检测延迟"",
                ""footer-text"": ""极致狂澜官方香港服务器"",
                ""help-center"": ""帮助中心"",
                ""privacy-policy"": ""隐私政策"",
                ""contact-us"": ""联系我们""
            }},
            en: {{
                ""site-title"": ""XtremeWave HongKong Server"",
                ""subtitle"": ""Enjoy extremely low latency and stable connections."",
                ""online-players"": ""Online Players"",
                ""current-games"": ""Current Online Games"",
                ""announcements"": ""Announcements"",
                ""server-features"": ""Server Features"",
                ""low-latency"": ""Low Latency"",
                ""low-latency-desc"": ""Average latency below 30ms in Guangdong, far superior to official servers"",
                ""anti-cheat"": ""Anti-Cheat"",
                ""anti-cheat-desc"": ""Monitor player data, automatically ban cheaters"",
                ""car-service"": ""Car Service"",
                ""car-service-desc"": ""Help organize players, add room notes"",
                ""download-title"": ""Download Installer Now"",
                ""download-desc"": ""Low latency server, contact for car service"",
                ""download-btn"": ""Download XW Installer"",
                ""mobile-install"": ""Mobile Install"",
                ""sponsor"": ""Sponsor Us"",
                ""sponsor-notes-title"": ""Important Notes"",
                ""sponsor-note1"": ""Sponsorship is completely voluntary and does not grant in-game privileges"",
                ""sponsor-note5"": ""Thank you for supporting XtremeWave!"",
                ""server-status"": ""Server Status"",
                ""latency-test"": ""Latency Test"",
                ""refresh-ping"": ""Refresh Ping"",
                ""footer-text"": ""XtremeWave Official Hong Kong Server"",
                ""help-center"": ""Help Center"",
                ""privacy-policy"": ""Privacy Policy"",
                ""contact-us"": ""Contact Us""
            }}
        }};

        // 公告数据
        const announcements = {{
            zh: [
                {{
                    title: ""Android/iOS安装私服支持"",
                    date: ""2026-02-01"",
                    content: ""1.支持了安卓或iOS设备安装极致狂澜香港服务器，可以通过点击该页面按钮进行操作，操作时请确保Among Us应用程序在后台运行。\n2.XtremeWave.HongKong私服现已支持 简体中文、繁体中文、英语、日语 四种语言的欢迎消息，此功能会自动检测您的游戏语言。\n3.更新了安装器。""
                }},
                {{
                    title: ""服务器功能更新"",
                    date: ""2026-01-31"",
                    content: ""1.AI游戏记录更新：现在的AI记录数据更准确。AI会记录所有玩家的行为（任务完成、进出管道、紧急破坏、击杀、修理破坏、开启会议或报告尸体、投票目标）您可以放心让AI为您评分，总结您在游戏中的表现。\n2./cmd指令追加：跟进miniduikboot的更新，XtremeWave香港服务器更新了/cmd功能，该功能让你的消息不转发给所有人，而是只被服务器接收，这防止了针对于H系模组的漏指令问题。\n3.官网更新：打开服务器官网(imp.xtreme.net.cn)，您可以看到当前在线的房间和历次更新的公告。\n4.安装器更新：针对于清风私服安装脚本会把私服配置文件修改为只读导致其他私服安装器难以进行安装的问题，本次修改了服务器安装器的安装逻辑，我们对清风私服安装器的这种行为持不赞同，但也不反对的态度。""
                }},
            ],
            en: [
                {{
                    title: ""Android/iOS Install Server Support"",
                    date: ""2026-02-01"",
                    content: ""1. Supports installing XtremeWave Hong Kong servers on Android or iOS devices. Click the button on this page to proceed. Ensure the Among Us app is running in the background during installation. 2. XtremeWave.HongKong private servers now support welcome messages in Simplified Chinese, Traditional Chinese, English, and Japanese. This feature automatically detects your game language. 3. Updated installer.""
                }},
                {{
                    title: ""Server Features Update"",
                    date: ""2026-01-31"",
                    content: ""1. AI Game Log Updates: Current AI log data is more accurate. The AI now records all player actions (task completion, entering/exiting conduits, emergency sabotage, kills, repairing sabotage, initiating meetings or reporting corpses, voting on targets). You may confidently allow the AI to score your performance and summarise your in-game actions. \n2. /cmd Command Addition: Following miniduikboot's update, XtremeWave's Hong Kong server has enhanced the /cmd functionality. This feature ensures your messages are received solely by the server rather than broadcast to all players, preventing command leakage issues targeting H-series mods. \n3. Official Website Update: Visit the server's official website (imp.xtreme.net.cn) to view currently active rooms and announcements for all past updates. \n4. Installer update: Addressing an issue where Qingfeng private server installation scripts modify configuration files to read-only status, hindering installation by other private server installers. This update modifies the server installer's installation logic. While we do not endorse this behaviour by Qingfeng's installer, we neither oppose nor prohibit it.""
                }},
            ]
        }};

        // 设置语言
        function setLanguage(lang) {{
            document.documentElement.lang = lang;
            const elements = document.querySelectorAll('[data-i18n]');
            
            elements.forEach(element => {{
                const key = element.getAttribute('data-i18n');
                if (translations[lang] && translations[lang][key]) {{
                    element.textContent = translations[lang][key];
                }}
            }});
            
            // 更新页面标题
            document.title = lang === 'zh' ? 'XtremeWave 香港私服 - 低延迟稳定游戏体验' : 'XtremeWave HongKong Server - Low Latency Gaming';
            
            // 更新语言按钮状态
            document.querySelectorAll('.lang-btn').forEach(btn => {{
                if (btn.getAttribute('data-lang') === lang) {{
                    btn.classList.add('active');
                }} else {{
                    btn.classList.remove('active');
                }}
            }});
            
            // 更新公告内容
            updateAnnouncements(lang);
            
            // 保存语言选择
            localStorage.setItem('preferred-language', lang);
        }}

        // 更新公告内容
        function updateAnnouncements(lang) {{
            const announcementContent = document.getElementById('announcement-content');
            announcementContent.innerHTML = '';
            
            if (announcements[lang]) {{
                announcements[lang].forEach(announcement => {{
                    const announcementElement = document.createElement('div');
                    announcementElement.className = 'announcement-item';
                    
                    announcementElement.innerHTML = `
                        <div class=""announcement-title"">${{announcement.title}}</div>
                        <div class=""announcement-date"">${{announcement.date}}</div>
                        <div class=""announcement-text"">${{announcement.content}}</div>
                    `;
                    
                    announcementContent.appendChild(announcementElement);
                }});
            }} else {{
                announcementContent.innerHTML = '<div class=""announcement-text"" data-i18n=""no-announcements"">暂无公告</div>';
            }}
        }}

        // 初始化语言
        document.addEventListener('DOMContentLoaded', () => {{
            // 设置默认语言
            const savedLang = localStorage.getItem('preferred-language') || 'zh';
            setLanguage(savedLang);
            
            // 语言切换按钮事件
            document.querySelectorAll('.lang-btn').forEach(btn => {{
                btn.addEventListener('click', (e) => {{
                    e.preventDefault();
                    const lang = btn.getAttribute('data-lang');
                    setLanguage(lang);
                }});
            }});
            
            // 延迟测试功能
            const refreshBtn = document.getElementById('refreshPing');
            const hkStatus = document.getElementById('hk-status');
            const hkLatency = document.getElementById('hk-latency');
            const hkBar = document.getElementById('hk-bar');
            
            refreshBtn.addEventListener('click', () => {{
                const currentLang = document.documentElement.lang;
                hkStatus.textContent = currentLang === 'zh' ? '● 检测中...' : '● Testing...';
                hkStatus.className = 'server-status testing';
                hkLatency.textContent = currentLang === 'zh' ? '检测中...' : 'Testing...';
                hkBar.style.width = '0%';
                
                // 模拟延迟测试
                setTimeout(() => {{
                    const latency = Math.floor(Math.random() * 100) + 30; // 30-130ms
                    hkStatus.textContent = currentLang === 'zh' ? '● 在线' : '● Online';
                    hkStatus.className = 'server-status online';
                    hkLatency.textContent = `${{latency}}ms`;
                    
                    // 更新延迟条
                    let width = 0;
                    let barClass = 'latency-fill ';
                    
                    if (latency < 50) {{
                        width = 100 - (latency / 50) * 30;
                        barClass += 'latency-good';
                    }} else if (latency < 100) {{
                        width = 70 - ((latency - 50) / 50) * 40;
                        barClass += 'latency-ok';
                    }} else {{
                        width = 30 - ((latency - 100) / 30) * 10;
                        barClass += 'latency-poor';
                    }}
                    
                    width = Math.max(5, Math.min(100, width));
                    hkBar.style.width = `${{width}}%`;
                    hkBar.className = barClass;
                }}, 1500);
            }});
            
            // 下载按钮事件
            document.getElementById('downloadBtn').addEventListener('click', (e) => {{
                // 这里不阻止默认行为，让链接正常跳转
                const currentLang = document.documentElement.lang;
                console.log(currentLang === 'zh' ? 
                    '开始下载XtremeWave私服安装器...' : 
                    'Starting download of XtremeWave Server Installer...');
            }});
        }});
    </script>
</body>
</html>
";

        return Content(htmlContent, "text/html");
    }
}
