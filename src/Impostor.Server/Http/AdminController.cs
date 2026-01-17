using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Http;
using Impostor.Api.Config;
using Impostor.Api.Games;
using Impostor.Api.Games.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Impostor.Server.Http;

[Route("/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly ILogger<AdminController> _logger;
    private readonly IGameManager _gameManager;
    private readonly BanService _banService;
    private readonly HostInfoConfig _hostInfoConfig;
    private readonly HttpClient _httpClient;

    public AdminController(ILogger<AdminController> logger, IGameManager gameManager,
                         BanService banService, IOptions<HostInfoConfig> hostInfoConfig,
                         IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _gameManager = gameManager;
        _banService = banService;
        _hostInfoConfig = hostInfoConfig.Value;
        _httpClient = httpClientFactory.CreateClient();
    }

    // 聊天消息存储结构
    public class RoomChat
    {
        public string RoomCode { get; set; } = string.Empty;

        public List<ChatMessage> Messages { get; } = new List<ChatMessage>();

        public DateTime? ClosedTime { get; set; }
    }

    public class ChatMessage
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string PlayerName { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public bool IsStrikethrough { get; set; }
    }

    // 房间聊天存储
    public static class RoomChatStore
    {
        private static readonly ConcurrentDictionary<string, RoomChat> _roomChats = new();

        // 添加消息到指定房间
        public static void AddMessage(string roomCode, string playerName, string content, bool isStrikethrough = false)
        {
            if (!_roomChats.TryGetValue(roomCode, out var roomChat))
            {
                roomChat = new RoomChat { RoomCode = roomCode };
                _roomChats[roomCode] = roomChat;
            }

            roomChat.Messages.Add(new ChatMessage
            {
                PlayerName = playerName,
                Content = content,
                IsStrikethrough = isStrikethrough
            });
        }

        // 获取房间聊天记录
        public static RoomChat GetRoomChat(string roomCode)
        {
            return _roomChats.TryGetValue(roomCode, out var roomChat)
                ? roomChat
                : new RoomChat { RoomCode = roomCode };
        }

        // 标记房间关闭并保存聊天记录
        public static void CloseRoom(string roomCode)
        {
            if (_roomChats.TryGetValue(roomCode, out var roomChat))
            {
                roomChat.ClosedTime = DateTime.Now;
                SaveRoomChatToFile(roomChat);
                _roomChats.TryRemove(roomCode, out _);
            }
        }

        // 保存聊天记录到文件
        private static void SaveRoomChatToFile(RoomChat roomChat)
        {
            try
            {
                var chatDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Text", "Chat");
                if (!Directory.Exists(chatDirectory))
                {
                    Directory.CreateDirectory(chatDirectory);
                }

                var fileName = $"{roomChat.RoomCode}_{roomChat.ClosedTime:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(chatDirectory, fileName);

                var lines = new List<string>
                {
                    $"房间代码: {roomChat.RoomCode}",
                    $"关闭时间: {roomChat.ClosedTime:yyyy-MM-dd HH:mm:ss}",
                    $"消息数量: {roomChat.Messages.Count}",
                    "=".PadRight(50, '=')
                };

                foreach (var message in roomChat.Messages)
                {
                    var strikethrough = message.IsStrikethrough ? "[已离开]" : "";
                    lines.Add($"[{message.Timestamp:HH:mm:ss}] {message.PlayerName}{strikethrough}: {message.Content}");
                }

                System.IO.File.WriteAllLines(filePath, lines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 记录错误但不要抛出，避免影响主流程
                Console.WriteLine($"保存聊天记录失败: {ex.Message}");
            }
        }
    }

    // 预留的玩家消息处理函数
    public static void OnPlayerChatMessage(string roomCode, IClientPlayer player, string content, bool isPlayerLeft = false)
    {
        RoomChatStore.AddMessage(roomCode, player.Client.Name, content, isPlayerLeft);
    }

    // 当房间关闭时调用此方法
    public static void OnRoomClosed(string roomCode)
    {
        RoomChatStore.CloseRoom(roomCode);
    }

    [HttpGet]
    public IActionResult AdminDashboard()
    {
        // 基本身份验证
        var authResult = CheckAuthentication();
        if (authResult != null)
            return authResult;

        // 获取服务器统计数据
        var allGames = _gameManager.Games.ToList();
        var publicGames = allGames.Where(game => game.IsPublic).ToList();
        var activeGames = allGames.Where(game => game.GameState == GameStates.Started).ToList();
        var waitingGames = allGames.Where(game => game.GameState == GameStates.NotStarted && game.PlayerCount < game.Options.MaxPlayers).ToList();
        var totalPlayers = allGames.Sum(game => game.PlayerCount);

        // 获取玩家信息
        var players = GetPlayerInfo(allGames);

        // 获取封禁列表
        var bannedPlayers = _banService.GetBannedPlayers();

        var html = $@"
<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>NImpostor星云管理面板</title>
    <style>
        .ban-form {{
            background: rgba(0,0,0,0.03);
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 20px;
        }}

        .form-group {{
            margin-bottom: 15px;
        }}

        .form-label {{
            display: block;
            margin-bottom: 5px;
            font-weight: 600;
            color: var(--text);
        }}

        .form-input {{
            width: 100%;
            padding: 10px;
            border: 1px solid var(--border);
            border-radius: 6px;
            font-size: 0.9rem;
        }}

        .form-select {{
            width: 100%;
            padding: 10px;
            border: 1px solid var(--border);
            border-radius: 6px;
            font-size: 0.9rem;
            background: white;
        }}

        .form-actions {{
            display: flex;
            gap: 10px;
            justify-content: flex-end;
        }}

        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        :root {{
            --primary: #3498db;
            --primary-dark: #2980b9;
            --success: #2ecc71;
            --warning: #f39c12;
            --danger: #e74c3c;
            --dark: #2c3e50;
            --light: #ecf0f1;
            --gray: #95a5a6;
            --text: #2c3e50;
            --text-light: #7f8c8d;
            --border: #bdc3c7;
            --shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        
        body {{
            background: linear-gradient(135deg, #74b9ff 0%, #0984e3 50%, #6c5ce7 100%);
            color: var(--text);
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            min-height: 100vh;
            padding: 20px;
        }}
        
        .container {{
            max-width: 1400px;
            margin: 0 auto;
        }}
        
        .header {{
            background: rgba(255, 255, 255, 0.95);
            backdrop-filter: blur(10px);
            border-radius: 15px;
            padding: 30px;
            margin-bottom: 30px;
            box-shadow: var(--shadow);
            text-align: center;
        }}
        
        .title {{
            font-size: 2.5em;
            font-weight: 300;
            color: var(--dark);
            margin-bottom: 10px;
        }}
        
        .subtitle {{
            color: var(--text-light);
            font-size: 1.1em;
        }}
        
        .stats-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }}
        
        .stat-card {{
            background: rgba(255, 255, 255, 0.95);
            border-radius: 12px;
            padding: 25px;
            text-align: center;
            box-shadow: var(--shadow);
            transition: transform 0.3s ease;
        }}
        
        .stat-card:hover {{
            transform: translateY(-5px);
        }}
        
        .stat-value {{
            font-size: 2.5em;
            font-weight: bold;
            color: var(--primary);
            margin: 10px 0;
        }}
        
        .stat-label {{
            color: var(--text-light);
            font-size: 0.9em;
            text-transform: uppercase;
            letter-spacing: 1px;
        }}
        
        .section {{
            background: rgba(255, 255, 255, 0.95);
            border-radius: 15px;
            padding: 30px;
            margin-bottom: 30px;
            box-shadow: var(--shadow);
        }}
        
        .section-title {{
            font-size: 1.5em;
            color: var(--dark);
            margin-bottom: 20px;
            padding-bottom: 15px;
            border-bottom: 2px solid var(--light);
            display: flex;
            align-items: center;
            gap: 10px;
        }}
        
        .table-container {{
            overflow-x: auto;
            border-radius: 10px;
            border: 1px solid var(--border);
        }}
        
        table {{
            width: 100%;
            border-collapse: collapse;
            background: white;
        }}
        
        th {{
            background: var(--primary);
            color: white;
            padding: 15px;
            text-align: left;
            font-weight: 600;
        }}
        
        td {{
            padding: 15px;
            border-bottom: 1px solid var(--border);
        }}
        
        tr:hover {{
            background: var(--light);
        }}
        
        .game-code {{
            font-family: monospace;
            font-weight: bold;
            color: var(--primary-dark);
        }}
        
        .status-badge {{
            display: inline-block;
            padding: 5px 12px;
            border-radius: 20px;
            font-size: 0.8em;
            font-weight: 600;
            text-transform: uppercase;
        }}
        
        .status-lobby {{
            background: rgba(46, 204, 113, 0.2);
            color: var(--success);
        }}
        
        .status-ingame {{
            background: rgba(231, 76, 60, 0.2);
            color: var(--danger);
        }}
        
        .player-avatar {{
            width: 40px;
            height: 40px;
            border-radius: 50%;
            background: linear-gradient(135deg, var(--primary), var(--primary-dark));
            display: inline-flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-weight: bold;
            margin-right: 10px;
        }}
        
        .empty-state {{
            text-align: center;
            padding: 50px;
            color: var(--text-light);
        }}
        
        .empty-state i {{
            font-size: 3em;
            margin-bottom: 20px;
            opacity: 0.5;
        }}
        
        .footer {{
            text-align: center;
            margin-top: 40px;
            color: rgba(255, 255, 255, 0.9);
            font-size: 0.9em;
        }}
        
        /* 按钮样式 */
        .btn {{
            padding: 8px 16px;
            border-radius: 6px;
            border: none;
            cursor: pointer;
            transition: all 0.3s ease;
            font-size: 0.85em;
            font-weight: 500;
            display: inline-flex;
            align-items: center;
            gap: 5px;
        }}
        
        .btn:hover {{
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(0,0,0,0.2);
        }}
        
        .btn-primary {{
            background: var(--primary);
            color: white;
        }}
        
        .btn-info {{
            background: #17a2b8;
            color: white;
        }}
        
        .btn-warning {{
            background: var(--warning);
            color: white;
        }}
        
        .btn-danger {{
            background: var(--danger);
            color: white;
        }}
        
        .btn-success {{
            background: var(--success);
            color: white;
        }}
        
        .modal {{
            display: none;
            position: fixed;
            z-index: 1000;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(0,0,0,0.5);
        }}
        
        .modal-content {{
            background-color: white;
            margin: 5% auto;
            padding: 0;
            border-radius: 12px;
            width: 90%;
            max-width: 600px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.3);
            animation: modalSlideIn 0.3s ease;
        }}
        
        @keyframes modalSlideIn {{
            from {{ transform: translateY(-50px); opacity: 0; }}
            to {{ transform: translateY(0); opacity: 1; }}
        }}
        
        .modal-header {{
            background: linear-gradient(135deg, var(--primary), var(--primary-dark));
            color: white;
            padding: 20px;
            border-radius: 12px 12px 0 0;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }}
        
        .modal-header h2 {{
            margin: 0;
            font-size: 1.5em;
        }}
        
        .close {{
            color: white;
            font-size: 28px;
            font-weight: bold;
            cursor: pointer;
            background: none;
            border: none;
        }}
        
        .close:hover {{
            opacity: 0.7;
        }}
        
        .modal-body {{
            padding: 25px;
        }}
        
        .player-details {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 15px;
            margin-bottom: 20px;
        }}
        
        .detail-item {{
            display: flex;
            flex-direction: column;
            gap: 5px;
        }}
        
        .detail-label {{
            font-size: 0.85em;
            color: var(--text-light);
            font-weight: 600;
        }}
        
        .detail-value {{
            font-size: 1em;
            color: var(--text);
            font-weight: 500;
        }}
        
        .modal-actions {{
            display: flex;
            gap: 10px;
            justify-content: flex-end;
            margin-top: 25px;
            padding-top: 20px;
            border-top: 1px solid var(--border);
        }}
        
        .room-link {{
            color: var(--primary);
            text-decoration: none;
            font-weight: 600;
            display: inline-flex;
            align-items: center;
            gap: 5px;
        }}
        
        .room-link:hover {{
            text-decoration: underline;
        }}

        /* 封禁管理相关样式 */
        .ban-modal {{
            display: none;
            position: fixed;
            z-index: 1000;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(0,0,0,0.5);
        }}

        .ban-modal-content {{
            background-color: white;
            margin: 5% auto;
            padding: 0;
            border-radius: 12px;
            width: 90%;
            max-width: 800px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.3);
            animation: modalSlideIn 0.3s ease;
        }}

        .ban-list {{
            max-height: 400px;
            overflow-y: auto;
            margin: 20px 0;
        }}

        .ban-item {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 15px;
            border-bottom: 1px solid var(--border);
        }}

        .ban-info {{
            flex: 1;
        }}

        .ban-reason {{
            color: var(--text-light);
            font-size: 0.9em;
            margin-top: 5px;
        }}

        .ban-actions {{
            display: flex;
            gap: 10px;
        }}

        .ban-type-badge {{
            display: inline-block;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 0.7em;
            font-weight: 600;
            margin-left: 8px;
        }}

        .ban-type-ip {{
            background: rgba(231, 76, 60, 0.2);
            color: var(--danger);
        }}

        .ban-type-friendcode {{
            background: rgba(155, 89, 182, 0.2);
            color: #9b59b6;
        }}

        .message-strikethrough {{
            text-decoration: line-through;
            opacity: 0.7;
        }}

        @media (max-width: 768px) {{
            .stats-grid {{
                grid-template-columns: 1fr;
            }}
            
            .header {{
                padding: 20px;
            }}
            
            .title {{
                font-size: 2em;
            }}
            
            .player-details {{
                grid-template-columns: 1fr;
            }}
            
            .modal-actions {{
                flex-direction: column;
            }}
            
            .error-container {{
                padding: 30px;
                margin: 50px 20px;
            }}
        }}        
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1 class=""title"">服务器管理后台</h1>
            <p class=""subtitle"">实时监控服务器状态和玩家信息</p>
        </div>
        
        <div class=""stats-grid"">
            <div class=""stat-card"">
                <div class=""stat-value"">{allGames.Count}</div>
                <div class=""stat-label"">总游戏房间</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-value"">{totalPlayers}</div>
                <div class=""stat-label"">在线玩家</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-value"">{activeGames.Count}</div>
                <div class=""stat-label"">进行中游戏</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-value"">{waitingGames.Count}</div>
                <div class=""stat-label"">等待中房间</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-value"">{bannedPlayers.Count}</div>
                <div class=""stat-label"">已封禁项目</div>
            </div>
        </div>

        <div class=""section"">
            <h2 class=""section-title"">
                <i class=""fas fa-bullhorn""></i>
                全局消息广播
            </h2>
            <div style=""display: flex; gap: 10px; align-items: center;"">
                <input type=""text"" id=""globalMessage"" placeholder=""输入要发送到所有房间的消息..."" 
                       style=""flex: 1; padding: 10px; border: 1px solid var(--border); border-radius: 6px;"">
                <button class=""btn btn-primary"" onclick=""sendGlobalMessage()"">
                    <i class=""fas fa-broadcast-tower""></i> 广播消息
                </button>
            </div>
        </div>

        <div class=""section"">
            <h2 class=""section-title"">
                <i class=""fas fa-ban""></i>
                封禁管理
            </h2>
            
            <div class=""ban-form"">
                <h3 style=""margin-bottom: 15px; color: var(--dark);"">手动添加封禁</h3>
                <div class=""form-group"">
                    <label class=""form-label"">封禁类型</label>
                    <select class=""form-select"" id=""banType"">
                        <option value=""IP"">IP地址</option>
                        <option value=""FriendCode"">好友代码</option>
                    </select>
                </div>
                <div class=""form-group"">
                    <label class=""form-label"">标识符</label>
                    <input type=""text"" class=""form-input"" id=""banIdentifier"" placeholder=""输入IP地址或好友代码"">
                </div>
                <div class=""form-group"">
                    <label class=""form-label"">玩家名称（可选）</label>
                    <input type=""text"" class=""form-input"" id=""banPlayerName"" placeholder=""输入玩家名称"">
                </div>
                <div class=""form-group"">
                    <label class=""form-label"">封禁原因</label>
                    <input type=""text"" class=""form-input"" id=""banReason"" placeholder=""输入封禁原因"">
                </div>
                <div class=""form-actions"">
                    <button class=""btn btn-danger"" onclick=""addManualBan()"">
                        <i class=""fas fa-ban""></i> 添加封禁
                    </button>
                </div>
            </div>

            <div style=""display: flex; gap: 10px; margin-bottom: 20px;"">
                <button class=""btn btn-danger"" onclick=""showBanModal()"">
                    <i class=""fas fa-list""></i> 查看封禁列表
                </button>
            </div>
            <p style=""color: var(--text-light); font-size: 0.9em;"">
                当前共有 <strong>{bannedPlayers.Count}</strong> 个封禁项目
            </p>
        </div>
        
        <div class=""section"">
            <h2 class=""section-title"">
                <i class=""fas fa-users""></i>
                玩家信息
            </h2>
            <div class=""table-container"">
                {(players.Any() ?
                    $@"<table>
                        <thead>
                            <tr>
                                <th>玩家</th>
                                <th>Friend Code</th>
                                <th>IP地址</th>
                                <th>地理位置</th>
                                <th>所在房间</th>
                                <th>状态</th>
                                <th>操作</th>
                            </tr>
                        </thead>
                        <tbody>
                            {string.Join("", players.Select(PlayerToHtmlRow))}
                        </tbody>
                    </table>"
                    :
                    @"<div class=""empty-state"">
                        <i class=""fas fa-user-slash""></i>
                        <h3>暂无在线玩家</h3>
                        <p>当前没有玩家在线</p>
                    </div>"
                )}
            </div>
        </div>
        
        <div class=""section"">
            <h2 class=""section-title"">
                <i class=""fas fa-door-open""></i>
                游戏房间
            </h2>
            <div class=""table-container"">
                {(allGames.Any() ?
                    $@"<table>
                        <thead>
                            <tr>
                                <th>房间代码</th>
                                <th>玩家</th>
                                <th>地图</th>
                                <th>内鬼数量</th>
                                <th>状态</th>
                                <th>房主</th>
                                <th>操作</th>
                            </tr>
                        </thead>
                        <tbody>
                            {string.Join("", allGames.Select(GameToHtmlRow))}
                        </tbody>
                    </table>"
                    :
                    @"<div class=""empty-state"">
                        <i class=""fas fa-door-closed""></i>
                        <h3>暂无游戏房间</h3>
                        <p>当前没有活跃的游戏房间</p>
                    </div>"
                )}
            </div>
        </div>

        <div class=""footer"">
            <p>服务器管理后台 &copy; {DateTime.Now.Year} - 请妥善保管管理员凭据</p>
            <p style=""margin-top: 10px; font-size: 0.8em; opacity: 0.7;"">
                提示：请修改默认的管理员用户名和密码
            </p>
        </div>
    </div>
    
    <div id=""playerModal"" class=""modal"">
        <div class=""modal-content"">
            <div class=""modal-header"">
                <h2>玩家详细信息</h2>
                <button class=""close"">&times;</button>
            </div>
            <div class=""modal-body"">
                <div class=""player-details"" id=""playerDetails"">
                    <!-- 动态内容将通过JavaScript填充 -->
                </div>
                <div class=""modal-actions"">
                    <button class=""btn btn-warning"" id=""kickPlayerBtn"">
                        <i class=""fas fa-user-minus""></i> 踢出玩家
                    </button>
                    <button class=""btn btn-danger"" id=""banPlayerBtn"">
                        <i class=""fas fa-ban""></i> 封禁玩家
                    </button>
                    <button class=""btn btn-danger"" id=""serverBanBtn"">
                        <i class=""fas fa-gavel""></i> 服务器封禁
                    </button>
                    <button class=""btn btn-info"" id=""disconnectPlayerBtn"">
                        <i class=""fas fa-plug""></i> 断开连接
                    </button>
                </div>
            </div>
        </div>
    </div>

    <div id=""banModal"" class=""ban-modal"">
        <div class=""ban-modal-content"">
            <div class=""modal-header"">
                <h2>封禁列表</h2>
                <button class=""close"" onclick=""closeBanModal()"">&times;</button>
            </div>
            <div class=""modal-body"">
                <div class=""ban-list"" id=""banList"">
                    {string.Join("", bannedPlayers.Select(BannedPlayerToHtml))}
                </div>
                {(bannedPlayers.Count == 0 ?
                    @"<div class=""empty-state"">
                        <i class=""fas fa-check-circle""></i>
                        <h3>暂无封禁项目</h3>
                        <p>当前没有封禁记录</p>
                    </div>" : "")}
            </div>
        </div>
    </div>
    
    <!-- Font Awesome for icons -->
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/js/all.min.js""></script>
    
    <script>
        let currentPlayerId = null;
        let currentRoomCode = null;
        
        function showPlayerDetails(playerId, roomCode) {{
            currentPlayerId = playerId;
            currentRoomCode = roomCode;
            
            fetch(`/admin/player/${{playerId}}?roomCode=${{roomCode}}`, {{
                headers: {{
                    'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                }}
            }})
            .then(response => response.json())
            .then(data => {{
                if (data.success) {{
                    const player = data.player;
                    const detailsHtml = `
                        <div class=""detail-item"">
                            <span class=""detail-label"">玩家名称</span>
                            <span class=""detail-value"">${{player.name}}</span>
                        </div>
                        <div class=""detail-item"">
                            <span class=""detail-label"">玩家ID</span>
                            <span class=""detail-value"">${{player.clientId}}</span>
                        </div>
                        <div class=""detail-item"">
                            <span class=""detail-label"">Friend Code</span>
                            <span class=""detail-value"">${{player.friendCode || 'N/A'}}</span>
                        </div>
                        <div class=""detail-item"">
                            <span class=""detail-label"">IP地址</span>
                            <span class=""detail-value"">${{player.ipAddress || '未知'}}</span>
                        </div>
                        <div class=""detail-item"">
                            <span class=""detail-label"">地理位置</span>
                            <span class=""detail-value"">${{player.location || '未知'}}</span>
                        </div>
                        <div class=""detail-item"">
                            <span class=""detail-label"">所在房间</span>
                            <span class=""detail-value"">${{player.gameCode}}</span>
                        </div>
                        <div class=""detail-item"">
                            <span class=""detail-label"">游戏版本</span>
                            <span class=""detail-value"">${{player.clientVersion || '未知'}}</span>
                        </div>
                        <div class=""detail-item"">
                            <span class=""detail-label"">平台</span>
                            <span class=""detail-value"">${{player.platform || '未知'}}</span>
                        </div>
                        <div class=""detail-item"">
                            <span class=""detail-label"">聊天模式</span>
                            <span class=""detail-value"">${{player.chatMode || '未知'}}</span>
                        </div>
                    `;
                    document.getElementById('playerDetails').innerHTML = detailsHtml;
                    document.getElementById('playerModal').style.display = 'block';
                }} else {{
                    alert('获取玩家信息失败: ' + data.message);
                }}
            }})
            .catch(error => {{
                console.error('Error:', error);
                alert('获取玩家信息时发生错误');
            }});
        }}

        function showBanModal() {{
            document.getElementById('banModal').style.display = 'block';
        }}

        function closeBanModal() {{
            document.getElementById('banModal').style.display = 'none';
        }}
        
        function closeModal() {{
            document.getElementById('playerModal').style.display = 'none';
            currentPlayerId = null;
            currentRoomCode = null;
        }}
        
        function performPlayerAction(action) {{
            if (!currentPlayerId || !currentRoomCode) {{
                alert('未选择玩家');
                return;
            }}
            
            const actionText = {{
                'kick': '踢出',
                'ban': '封禁',
                'serverban': '服务器封禁',
                'disconnect': '断开连接'
            }}[action];
            
            const reason = prompt(`请输入${{actionText}}原因（可选）:`);
            if (reason === null) return; // 用户取消
            
            fetch(`/admin/${{action}}`, {{
                method: 'POST',
                headers: {{
                    'Content-Type': 'application/json',
                    'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                }},
                body: JSON.stringify({{
                    roomCode: currentRoomCode,
                    clientId: currentPlayerId,
                    reason: reason || `管理员${{actionText}}`
                }})
            }})
            .then(response => response.json())
            .then(data => {{
                if (data.success) {{
                    alert(data.message);
                    closeModal();
                    setTimeout(() => location.reload(), 1000);
                }} else {{
                    alert('操作失败: ' + data.message);
                }}
            }})
            .catch(error => {{
                console.error('Error:', error);
                alert('操作时发生错误');
            }});
        }}

        function unbanPlayer(identifier, type) {{
            if (!confirm('确定要解封此项目吗？')) {{
                return;
            }}
            
            fetch('/admin/unban', {{
                method: 'POST',
                headers: {{
                    'Content-Type': 'application/json',
                    'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                }},
                body: JSON.stringify({{
                    identifier: identifier,
                    type: type
                }})
            }})
            .then(response => response.json())
            .then(data => {{
                if (data.success) {{
                    alert(data.message);
                    setTimeout(() => location.reload(), 1000);
                }} else {{
                    alert('解封失败: ' + data.message);
                }}
            }})
            .catch(error => {{
                console.error('Error:', error);
                alert('解封时发生错误');
            }});
        }}

        function addManualBan() {{
            const banType = document.getElementById('banType').value;
            const identifier = document.getElementById('banIdentifier').value.trim();
            const playerName = document.getElementById('banPlayerName').value.trim();
            const reason = document.getElementById('banReason').value.trim();
            
            if (!identifier) {{
                alert('请输入标识符');
                return;
            }}
            
            if (!reason) {{
                alert('请输入封禁原因');
                return;
            }}
            
            if (!confirm(`确定要封禁${{banType === 'IP' ? 'IP地址' : '好友代码'}}：${{identifier}} 吗？`)) {{
                return;
            }}
            
            fetch('/admin/manual-ban', {{
                method: 'POST',
                headers: {{
                    'Content-Type': 'application/json',
                    'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                }},
                body: JSON.stringify({{
                    type: banType,
                    identifier: identifier,
                    playerName: playerName || '手动封禁',
                    reason: reason
                }})
            }})
            .then(response => response.json())
            .then(data => {{
                if (data.success) {{
                    alert('封禁添加成功！');
                    document.getElementById('banIdentifier').value = '';
                    document.getElementById('banPlayerName').value = '';
                    document.getElementById('banReason').value = '';
                    // 刷新页面
                    setTimeout(() => location.reload(), 1000);
                }} else {{
                    alert('封禁失败: ' + data.message);
                }}
            }})
            .catch(error => {{
                console.error('Error:', error);
                alert('添加封禁时发生错误');
            }});
        }}

        function sendGlobalMessage() {{
            const message = document.getElementById('globalMessage').value.trim();
            
            if (!message) {{
                alert('请输入要发送的消息');
                return;
            }}
            
            if (!confirm('确定要向所有房间发送这条消息吗？')) {{
                return;
            }}
            
            fetch('/admin/global-chat', {{
                method: 'POST',
                headers: {{
                    'Content-Type': 'application/json',
                    'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                }},
                body: JSON.stringify({{
                    message: message
                }})
            }})
            .then(response => response.json())
            .then(data => {{
                if (data.success) {{
                    alert('消息发送成功！');
                    document.getElementById('globalMessage').value = '';
                }} else {{
                    alert('发送失败: ' + data.message);
                }}
            }})
            .catch(error => {{
                console.error('Error:', error);
                alert('发送消息时发生错误');
            }});
        }}
        
        document.addEventListener('DOMContentLoaded', function() {{
            document.querySelector('.close').addEventListener('click', closeModal);
            document.getElementById('playerModal').addEventListener('click', function(e) {{
                if (e.target === this) closeModal();
            }});
            
            document.getElementById('kickPlayerBtn').addEventListener('click', () => performPlayerAction('kick'));
            document.getElementById('banPlayerBtn').addEventListener('click', () => performPlayerAction('ban'));
            document.getElementById('serverBanBtn').addEventListener('click', () => performPlayerAction('serverban'));
            document.getElementById('disconnectPlayerBtn').addEventListener('click', () => performPlayerAction('disconnect'));
           }});
    </script>
</body>
</html>";

        return Content(html, "text/html");
    }

    // 生成封禁玩家HTML
    private string BannedPlayerToHtml(BanService.BannedPlayer bannedPlayer)
    {
        var banType = string.IsNullOrEmpty(bannedPlayer.FriendCode) ? "IP" : "FriendCode";
        var identifier = banType == "IP" ? bannedPlayer.IPAddress : bannedPlayer.FriendCode;
        var typeBadge = banType == "IP" ? "ban-type-ip" : "ban-type-friendcode";
        var typeText = banType == "IP" ? "IP封禁" : "好友代码封禁";

        return $@"
        <div class=""ban-item"">
            <div class=""ban-info"">
                <div>
                    <strong>{bannedPlayer.PlayerName}</strong>
                    <span class=""ban-type-badge {typeBadge}"">{typeText}</span>
                </div>
                <div class=""ban-reason"">
                    标识符: {identifier} | 
                    封禁时间: {bannedPlayer.BanTime:yyyy-MM-dd HH:mm:ss}
                </div>
                <div class=""ban-reason"">封禁原因: {bannedPlayer.Reason}</div>
            </div>
            <div class=""ban-actions"">
                <button class=""btn btn-success"" onclick=""unbanPlayer('{identifier}', '{banType}')"">
                    <i class=""fas fa-unlock""></i> 解封
                </button>
            </div>
        </div>";
    }

    [HttpPost("manual-ban")]
    public IActionResult ManualBan([FromBody] ManualBanRequest request)
    {
        // 身份验证
        var authResult = CheckAuthentication();
        if (authResult != null)
            return authResult;

        try
        {
            if (request.Type == "IP")
            {
                _banService.BanPlayer(
                    request.PlayerName,
                    null, // FriendCode为null
                    request.Identifier,
                    request.Reason
                );
            }
            else if (request.Type == "FriendCode")
            {
                _banService.BanPlayer(
                    request.PlayerName,
                    request.Identifier, // FriendCode
                    null, // IP为null
                    request.Reason
                );
            }
            else
            {
                return BadRequest(new { success = false, message = "无效的封禁类型" });
            }

            return Ok(new { success = true, message = "封禁添加成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "手动封禁时出错");
            return StatusCode(500, new { success = false, message = $"封禁失败: {ex.Message}" });
        }
    }

    // 修改解封API以支持类型
    [HttpPost("unban")]
    public IActionResult UnbanPlayer([FromBody] UnbanRequest request)
    {
        // 身份验证
        var authResult = CheckAuthentication();
        if (authResult != null)
            return authResult;

        try
        {
            var success = _banService.UnbanPlayer(request.Identifier, request.Type);
            if (success)
            {
                return Ok(new { success = true, message = "解封成功" });
            }
            else
            {
                return NotFound(new { success = false, message = "未找到对应的封禁记录" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解封玩家时出错");
            return StatusCode(500, new { success = false, message = $"解封失败: {ex.Message}" });
        }
    }

    // 修改获取玩家详细信息API以包含地理位置
    [HttpGet("player/{clientId}")]
    public async Task<IActionResult> GetPlayerDetails(int clientId, [FromQuery] string roomCode)
    {
        // 身份验证
        var authResult = CheckAuthentication();
        if (authResult != null)
            return authResult;

        // 验证房间代码
        if (!TryParseGameCode(roomCode, out var gameCode))
        {
            return BadRequest(new { success = false, message = "房间代码无效" });
        }

        // 查找游戏
        var game = _gameManager.Find(gameCode);
        if (game == null)
        {
            return NotFound(new { success = false, message = $"未找到代码为 {roomCode} 的游戏" });
        }

        // 查找玩家
        var player = game.GetClientPlayer(clientId);
        if (player == null)
        {
            return NotFound(new { success = false, message = $"未找到玩家" });
        }

        // 获取玩家IP地址
        string ipAddress = "未知";
        string location = "未知";

        try
        {
            var endPoint = player.Client.Connection.EndPoint;
            if (endPoint is IPEndPoint ipEndPoint)
            {
                ipAddress = ipEndPoint.Address.ToString();

                // 获取地理位置
                var locationService = new IpLocationService(_httpClient, _logger);
                location = await locationService.GetLocationAsync(ipAddress);
            }
        }
        catch
        {
            // 忽略错误，使用默认值
        }

        var playerInfo = new
        {
            name = player.Client.Name,
            clientId = player.Client.Id,
            friendCode = player.Client.FriendCode ?? "N/A",
            gameCode = roomCode,
            ipAddress = ipAddress,
            location = location,
            clientVersion = player.Client.GameVersion.ToString(),
            platform = player.Client.PlatformSpecificData?.Platform.ToString() ?? "未知",
            chatMode = player.Client.ChatMode.ToString()
        };

        return Ok(new { success = true, player = playerInfo });
    }

    // 修改生成玩家信息行HTML以包含IP和地理位置
    private string PlayerToHtmlRow(PlayerInfo player)
    {
        var statusClass = player.IsInGame ? "status-ingame" : "status-lobby";
        var statusText = player.IsInGame ? "游戏中" : "大厅中";

        return $@"
        <tr>
            <td>
                <div style=""display: flex; align-items: center;"">
                    <div class=""player-avatar"">
                        {player.Name[0].ToString().ToUpper()}
                    </div>
                    <div>
                        <div style=""font-weight: 600;"">{player.Name}</div>
                        <div style=""font-size: 0.8em; color: var(--text-light);"">ID: {player.ClientId}</div>
                    </div>
                </div>
            </td>
            <td style=""font-family: monospace; font-weight: 600; color: var(--primary-dark);"">
                {player.FriendCode}
            </td>
            <td style=""font-family: monospace; font-size: 0.9em;"">{player.IPAddress}</td>
            <td style=""font-size: 0.9em;"">{player.Location}</td>
            <td class=""game-code"">{player.GameCode}</td>
            <td>
                <span class=""status-badge {statusClass}"">{statusText}</span>
            </td>
            <td>
                <button class=""btn btn-info"" onclick=""showPlayerDetails({player.ClientId}, '{player.GameCode}')"">
                    <i class=""fas fa-info-circle""></i> 详细信息
                </button>
            </td>
        </tr>";
    }

    // 修改玩家信息类以包含IP和地理位置
    private class PlayerInfo
    {
        public string Name { get; set; } = string.Empty;

        public int ClientId { get; set; }

        public string FriendCode { get; set; } = string.Empty;

        public string GameCode { get; set; } = string.Empty;

        public bool IsInGame { get; set; }

        public string IPAddress { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;
    }

    // 修改获取玩家信息方法以包含IP和地理位置
    private List<PlayerInfo> GetPlayerInfo(List<IGame> games)
    {
        var players = new List<PlayerInfo>();
        var locationService = new IpLocationService(_httpClient, _logger);

        foreach (var game in games)
        {
            foreach (var player in game.Players)
            {
                string ipAddress = "未知";
                string location = "未知";

                try
                {
                    var endPoint = player.Client.Connection.EndPoint;
                    if (endPoint is IPEndPoint ipEndPoint)
                    {
                        ipAddress = ipEndPoint.Address.ToString();
                        // 注意：这里同步调用异步方法，在实际使用中可能需要优化
                        location = locationService.GetLocationAsync(ipAddress).GetAwaiter().GetResult();
                    }
                }
                catch
                {
                    // 忽略错误
                }

                players.Add(new PlayerInfo
                {
                    Name = player.Client.Name,
                    ClientId = player.Client.Id,
                    FriendCode = player.Client.FriendCode ?? "N/A",
                    GameCode = game.Code.ToString(),
                    IsInGame = game.GameState == GameStates.Started,
                    IPAddress = ipAddress,
                    Location = location
                });
            }
        }

        return players;
    }

    // 房间详细视图
    [HttpGet("room/{roomCode}")]
    public IActionResult GetRoomDetail(string roomCode)
    {
        // 身份验证
        var authResult = CheckAuthentication();
        if (authResult != null)
            return authResult;

        // 验证房间代码
        if (!TryParseGameCode(roomCode, out var gameCode))
        {
            return InvalidRoomCodePage(roomCode);
        }

        // 查找游戏
        var game = _gameManager.Find(gameCode);
        if (game == null)
        {
            return InvalidRoomCodePage(roomCode);
        }

        // 获取聊天记录
        var roomChat = RoomChatStore.GetRoomChat(roomCode);

        var html = $@"
<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>房间管理 - {roomCode}</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        :root {{
            --primary: #3498db;
            --primary-dark: #2980b9;
            --success: #2ecc71;
            --warning: #f39c12;
            --danger: #e74c3c;
            --dark: #2c3e50;
            --light: #ecf0f1;
            --text: #2c3e50;
            --text-light: #7f8c8d;
            --border: #bdc3c7;
            --shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        
        body {{
            background: linear-gradient(135deg, #74b9ff 0%, #0984e3 50%, #6c5ce7 100%);
            color: var(--text);
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            min-height: 100vh;
            padding: 20px;
        }}
        
        .container {{
            max-width: 1200px;
            margin: 0 auto;
        }}
        
        .room-header {{
            background: rgba(255, 255, 255, 0.95);
            backdrop-filter: blur(10px);
            border-radius: 15px;
            padding: 25px;
            margin-bottom: 25px;
            box-shadow: var(--shadow);
        }}
        
        .room-header h1 {{
            display: flex;
            align-items: center;
            gap: 15px;
            font-size: 2.2rem;
            color: var(--dark);
            margin-bottom: 10px;
        }}
        
        .room-info-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }}
        
        .room-info-card {{
            background: rgba(255, 255, 255, 0.95);
            border-radius: 12px;
            padding: 20px;
            text-align: center;
            box-shadow: var(--shadow);
        }}
        
        .room-info-icon {{
            width: 50px;
            height: 50px;
            border-radius: 50%;
            background: linear-gradient(135deg, var(--primary), var(--primary-dark));
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 15px;
            font-size: 1.5rem;
            color: white;
        }}
        
        .room-info-value {{
            font-size: 1.8rem;
            font-weight: bold;
            color: var(--primary);
            margin: 5px 0;
        }}
        
        .room-info-label {{
            color: var(--text-light);
            font-size: 0.9em;
        }}
        
        .section {{
            background: rgba(255, 255, 255, 0.95);
            border-radius: 15px;
            padding: 25px;
            margin-bottom: 25px;
            box-shadow: var(--shadow);
        }}
        
        .section-title {{
            font-size: 1.3em;
            color: var(--dark);
            margin-bottom: 20px;
            display: flex;
            align-items: center;
            gap: 10px;
        }}
        
        .players-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 20px;
        }}
        
        .player-card {{
            background: white;
            border-radius: 10px;
            padding: 20px;
            box-shadow: var(--shadow);
            border-left: 4px solid var(--primary);
        }}
        
        .player-header {{
            display: flex;
            align-items: center;
            gap: 15px;
            margin-bottom: 15px;
        }}
        
        .player-avatar {{
            width: 50px;
            height: 50px;
            border-radius: 50%;
            background: linear-gradient(135deg, var(--primary), var(--primary-dark));
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-weight: bold;
            font-size: 1.2em;
        }}
        
        .player-info {{
            flex: 1;
        }}
        
        .player-name {{
            font-weight: 600;
            font-size: 1.1em;
            margin-bottom: 5px;
        }}
        
        .player-id {{
            font-size: 0.85em;
            color: var(--text-light);
        }}
        
        .player-details {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 10px;
            margin-bottom: 15px;
            font-size: 0.9em;
        }}
        
        .detail-label {{
            color: var(--text-light);
        }}
        
        .player-actions {{
            display: flex;
            gap: 8px;
            flex-wrap: wrap;
        }}
        
        .btn {{
            padding: 6px 12px;
            border-radius: 6px;
            border: none;
            cursor: pointer;
            transition: all 0.3s ease;
            font-size: 0.8em;
            font-weight: 500;
            display: inline-flex;
            align-items: center;
            gap: 4px;
        }}
        
        .btn:hover {{
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(0,0,0,0.2);
        }}
        
        .btn-info {{
            background: #17a2b8;
            color: white;
        }}
        
        .btn-warning {{
            background: var(--warning);
            color: white;
        }}
        
        .btn-danger {{
            background: var(--danger);
            color: white;
        }}
        
        .back-button {{
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 10px 20px;
            background: var(--primary);
            color: white;
            border: none;
            border-radius: 8px;
            text-decoration: none;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.3s ease;
        }}
        
        .back-button:hover {{
            background: var(--primary-dark);
            transform: translateY(-2px);
        }}

        /* 聊天相关样式 */
        .chat-container {{
            display: flex;
            flex-direction: column;
            height: 400px;
            border: 1px solid var(--border);
            border-radius: 10px;
            overflow: hidden;
            box-shadow: var(--shadow);
        }}

        .chat-messages {{
            flex: 1;
            overflow-y: auto;
            padding: 15px;
            background: white;
        }}

        .chat-input-area {{
            display: flex;
            padding: 15px;
            background: rgba(0,0,0,0.03);
            border-top: 1px solid var(--border);
        }}

        .chat-input {{
            flex: 1;
            padding: 10px 15px;
            border: 1px solid var(--border);
            border-radius: 6px;
            font-size: 0.9rem;
            transition: all 0.3s ease;
        }}

        .chat-input:focus {{
            border-color: var(--primary);
            box-shadow: 0 0 0 2px rgba(52, 152, 219, 0.2);
            outline: none;
        }}

        .send-button {{
            margin-left: 10px;
            padding: 10px 20px;
            background: var(--primary);
            color: white;
            border: none;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.3s ease;
        }}

        .send-button:hover {{
            background: var(--primary-dark);
            transform: translateY(-1px);
        }}

        .message {{
            margin-bottom: 15px;
            padding: 10px;
            border-radius: 8px;
            background: rgba(0,0,0,0.02);
        }}

        .message-header {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 5px;
            font-size: 0.8rem;
        }}

        .message-player {{
            font-weight: 600;
            color: var(--primary);
        }}

        .message-time {{
            color: var(--text-light);
        }}

        .message-content {{
            line-height: 1.4;
        }}

        .admin-message {{
            background: rgba(254, 153, 0, 0.1);
            border-left: 3px solid #FE9900;
        }}

        .message-strikethrough {{
            text-decoration: line-through;
            opacity: 0.7;
        }}
        
        @media (max-width: 768px) {{
            .players-grid {{
                grid-template-columns: 1fr;
            }}
            
            .player-details {{
                grid-template-columns: 1fr;
            }}
            
            .player-actions {{
                flex-direction: column;
            }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""room-header"">
            <h1>
                <i class=""fas fa-door-open""></i>
                房间管理 - {roomCode}
            </h1>
            <a href=""/admin"" class=""back-button"">
                <i class=""fas fa-arrow-left""></i> 返回管理后台
            </a>
        </div>
        
        <div class=""room-info-grid"">
            <div class=""room-info-card"">
                <div class=""room-info-icon"">
                    <i class=""fas fa-users""></i>
                </div>
                <div class=""room-info-value"">{game.PlayerCount}/{game.Options.MaxPlayers}</div>
                <div class=""room-info-label"">玩家数量</div>
            </div>
            
            <div class=""room-info-card"">
                <div class=""room-info-icon"">
                    <i class=""fas fa-map""></i>
                </div>
                <div class=""room-info-value"">{game.Options.Map}</div>
                <div class=""room-info-label"">地图</div>
            </div>
            
            <div class=""room-info-card"">
                <div class=""room-info-icon"">
                    <i class=""fas fa-mask""></i>
                </div>
                <div class=""room-info-value"">{game.Options.NumImpostors}</div>
                <div class=""room-info-label"">内鬼数量</div>
            </div>
            
            <div class=""room-info-card"">
                <div class=""room-info-icon"">
                    <i class=""fas fa-crown""></i>
                </div>
                <div class=""room-info-value"">{game.Host?.Client.Name ?? "未知"}</div>
                <div class=""room-info-label"">房主</div>
            </div>
        </div>
        
        <div class=""section"">
            <h2 class=""section-title"">
                <i class=""fas fa-users""></i>
                玩家列表
            </h2>
            
            <div class=""players-grid"">
                {string.Join("", game.Players.Select(p => PlayerToHtmlCard(p, roomCode)))}
            </div>
        </div>

        <div class=""section"">
            <h2 class=""section-title"">
                <i class=""fas fa-comments""></i>
                房间聊天
            </h2>
            
            <div class=""chat-container"">
                <div class=""chat-messages"" id=""chatMessages"">
                    {string.Join("", roomChat.Messages.Select(MessageToHtml))}
                </div>
                <div class=""chat-input-area"">
                    <input type=""text"" class=""chat-input"" id=""chatInput"" placeholder=""输入消息..."">
                    <button class=""send-button"" id=""sendChatBtn"">
                        <i class=""fas fa-paper-plane""></i> 发送
                    </button>
                </div>
            </div>
        </div>
    </div>
    
    <!-- Font Awesome for icons -->
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/js/all.min.js""></script>
    
    <script>
        // 存储当前房间代码
        const roomCode = '{roomCode}';
        
        // 发送聊天消息
        function sendChatMessage() {{
            const input = document.getElementById('chatInput');
            const message = input.value.trim();
            
            if (message) {{
                fetch('/admin/chat', {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json',
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }},
                    body: JSON.stringify({{
                        roomCode: roomCode,
                        message: message
                    }})
                }})
                .then(response => response.json())
                .then(data => {{
                    if (data.success) {{
                        input.value = '';
                        // 刷新页面以显示新消息
                        setTimeout(() => location.reload(), 500);
                    }} else {{
                        alert('发送失败: ' + data.message);
                    }}
                }});
            }}
        }}

        // 执行玩家操作
        function performPlayerAction(action, playerId, roomCode) {{
            const actionText = {{
                'kick': '踢出',
                'ban': '封禁',
                'disconnect': '断开连接'
            }}[action];
            
            const reason = prompt(`请输入${{actionText}}原因（可选）:`);
            if (reason === null) return; // 用户取消
            
            fetch(`/admin/${{action}}`, {{
                method: 'POST',
                headers: {{
                    'Content-Type': 'application/json',
                    'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                }},
                body: JSON.stringify({{
                    roomCode: roomCode,
                    clientId: playerId,
                    reason: reason || `管理员${{actionText}}`
                }})
            }})
            .then(response => response.json())
            .then(data => {{
                if (data.success) {{
                    alert(data.message);
                    // 刷新页面
                    setTimeout(() => location.reload(), 1000);
                }} else {{
                    alert('操作失败: ' + data.message);
                }}
            }})
            .catch(error => {{
                console.error('Error:', error);
                alert('操作时发生错误');
            }});
        }}
        
        document.addEventListener('DOMContentLoaded', () => {{
            // 绑定发送按钮事件
            document.getElementById('sendChatBtn').addEventListener('click', sendChatMessage);
            
            // 按Enter键发送消息
            document.getElementById('chatInput').addEventListener('keypress', e => {{
                if (e.key === 'Enter') sendChatMessage();
            }});
            
            // 滚动到聊天底部
            const chatMessages = document.getElementById('chatMessages');
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }});
    </script>
</body>
</html>";

        return Content(html, "text/html");
    }

    // 聊天消息HTML生成
    private string MessageToHtml(ChatMessage message)
    {
        var isAdmin = message.PlayerName == "Admin";
        var messageClass = isAdmin ? "message admin-message" : "message";
        var contentClass = message.IsStrikethrough ? "message-content message-strikethrough" : "message-content";

        return $@"
        <div class=""{messageClass}"">
            <div class=""message-header"">
                <span class=""message-player"">{message.PlayerName}</span>
                <span class=""message-time"">{message.Timestamp:HH:mm:ss}</span>
            </div>
            <div class=""{contentClass}"">{message.Content}</div>
        </div>";
    }

    // 生成玩家卡片HTML（房间详情页）
    private string PlayerToHtmlCard(IClientPlayer player, string roomCode)
    {
        // 获取玩家IP地址
        string ipAddress = "未知";
        string location = "未知";

        try
        {
            var endPoint = player.Client.Connection.EndPoint;
            if (endPoint is IPEndPoint ipEndPoint)
            {
                ipAddress = ipEndPoint.Address.ToString();

                // 获取地理位置
                var locationService = new IpLocationService(_httpClient, _logger);
                location = locationService.GetLocationAsync(ipAddress).GetAwaiter().GetResult();
            }
        }
        catch
        {
            // 忽略错误，使用默认值
        }

        return $@"
        <div class=""player-card"">
            <div class=""player-header"">
                <div class=""player-avatar"">
                    {player.Client.Name[0].ToString().ToUpper()}
                </div>
                <div class=""player-info"">
                    <div class=""player-name"">{player.Client.Name}</div>
                    <div class=""player-id"">ID: {player.Client.Id}</div>
                </div>
            </div>
            <div class=""player-details"">
                <div>
                    <span class=""detail-label"">Friend Code:</span>
                    <span>{player.Client.FriendCode ?? "N/A"}</span>
                </div>
                <div>
                    <span class=""detail-label"">IP地址:</span>
                    <span>{ipAddress}</span>
                </div>
                <div>
                    <span class=""detail-label"">地理位置:</span>
                    <span>{location}</span>
                </div>
                <div>
                    <span class=""detail-label"">游戏版本:</span>
                    <span>{player.Client.GameVersion}</span>
                </div>
                <div>
                    <span class=""detail-label"">平台:</span>
                    <span>{player.Client.PlatformSpecificData?.Platform.ToString() ?? "未知"}</span>
                </div>
            </div>
            <div class=""player-actions"">
                <button class=""btn btn-warning"" onclick=""performPlayerAction('kick', {player.Client.Id}, '{roomCode}')"">
                    <i class=""fas fa-user-minus""></i> 踢出
                </button>
                <button class=""btn btn-danger"" onclick=""performPlayerAction('ban', {player.Client.Id}, '{roomCode}')"">
                    <i class=""fas fa-ban""></i> 封禁
                </button>
                <button class=""btn btn-danger"" onclick=""performPlayerAction('serverban', {player.Client.Id}, '{roomCode}')"">
                    <i class=""fas fa-gavel""></i> 服务器封禁
                </button>
                <button class=""btn btn-info"" onclick=""performPlayerAction('disconnect', {player.Client.Id}, '{roomCode}')"">
                    <i class=""fas fa-plug""></i> 断开
                </button>
            </div>
        </div>";
    }

    // 生成游戏房间行HTML
    private string GameToHtmlRow(IGame game)
    {
        var statusClass = game.GameState == GameStates.Started ? "status-ingame" : "status-lobby";
        var statusText = game.GameState == GameStates.Started ? "游戏中" : "等待中";
        var hostName = game.Host?.Client.Name ?? "未知";

        return $@"
        <tr>
            <td class=""game-code"">
                <a href=""/admin/room/{game.Code}"" class=""room-link"">
                    <i class=""fas fa-external-link-alt""></i>
                    {game.Code}
                </a>
            </td>
            <td>{game.PlayerCount}/{game.Options.MaxPlayers}</td>
            <td>{game.Options.Map}</td>
            <td>{game.Options.NumImpostors}</td>
            <td>
                <span class=""status-badge {statusClass}"">{statusText}</span>
            </td>
            <td>{hostName}</td>
            <td>
                <a href=""/admin/room/{game.Code}"" class=""btn btn-primary"">
                    <i class=""fas fa-cog""></i> 管理
                </a>
            </td>
        </tr>";
    }

    // 无效房间代码页面
    private IActionResult InvalidRoomCodePage(string roomCode)
    {
        var html = $@"
<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>房间不存在</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            background: linear-gradient(135deg, #74b9ff 0%, #0984e3 50%, #6c5ce7 100%);
            color: #2c3e50;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}
        
        .error-container {{
            background: rgba(255, 255, 255, 0.95);
            border-radius: 15px;
            padding: 50px;
            text-align: center;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
            max-width: 500px;
            width: 100%;
        }}
        
        .error-icon {{
            font-size: 4em;
            color: #e74c3c;
            margin-bottom: 20px;
        }}
        
        .error-title {{
            font-size: 2em;
            color: #2c3e50;
            margin-bottom: 15px;
        }}
        
        .error-message {{
            color: #7f8c8d;
            font-size: 1.1em;
            margin-bottom: 30px;
        }}
        
        .countdown {{
            font-size: 1.2em;
            color: #3498db;
            font-weight: bold;
            margin: 20px 0;
        }}
        
        .btn {{
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 12px 24px;
            background: #3498db;
            color: white;
            border: none;
            border-radius: 8px;
            text-decoration: none;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.3s ease;
            font-size: 1em;
        }}
        
        .btn:hover {{
            background: #2980b9;
            transform: translateY(-2px);
        }}
        
        @media (max-width: 768px) {{
            .error-container {{
                padding: 30px;
            }}
            
            .error-title {{
                font-size: 1.5em;
            }}
        }}
    </style>
</head>
<body>
    <div class=""error-container"">
        <div class=""error-icon"">
            <i class=""fas fa-exclamation-triangle""></i>
        </div>
        <h1 class=""error-title"">房间不存在</h1>
        <p class=""error-message"">房间代码 <strong>{roomCode}</strong> 无效或房间已不存在。</p>
        <div class=""countdown"" id=""countdown"">10秒后自动返回管理后台...</div>
        <a href=""/admin"" class=""btn"">
            <i class=""fas fa-arrow-left""></i> 立即返回管理后台
        </a>
    </div>
    
    <!-- Font Awesome for icons -->
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/js/all.min.js""></script>
    
    <script>
        let countdown = 10;
        const countdownElement = document.getElementById('countdown');
        
        function updateCountdown() {{
            countdown--;
            countdownElement.textContent = countdown + '秒后自动返回管理后台...';
            
            if (countdown <= 0) {{
                window.location.href = '/admin';
            }} else {{
                setTimeout(updateCountdown, 1000);
            }}
        }}
        
        // 开始倒计时
        setTimeout(updateCountdown, 1000);
    </script>
</body>
</html>";

        return Content(html, "text/html");
    }

    // 玩家操作API
    [HttpPost("kick")]
    public async Task<IActionResult> KickPlayer([FromBody] PlayerActionRequest request)
    {
        return await HandlePlayerAction(request, "kick", async player =>
        {
            await player.KickAsync();
            return $"成功将玩家 {player.Client.Name} 从游戏 {request.RoomCode} 中踢出";
        });
    }

    [HttpPost("ban")]
    public async Task<IActionResult> BanPlayer([FromBody] PlayerActionRequest request)
    {
        return await HandlePlayerAction(request, "ban", async player =>
        {
            await player.BanAsync();
            return $"成功将玩家 {player.Client.Name} 从游戏 {request.RoomCode} 中封禁";
        });
    }

    // 服务器封禁API
    [HttpPost("serverban")]
    public async Task<IActionResult> ServerBanPlayer([FromBody] PlayerActionRequest request)
    {
        // 身份验证
        var authResult = CheckAuthentication();
        if (authResult != null)
            return authResult;

        // 验证房间代码
        if (!TryParseGameCode(request.RoomCode, out var gameCode))
        {
            return BadRequest(new { success = false, message = "房间代码无效" });
        }

        // 查找游戏
        var game = _gameManager.Find(gameCode);
        if (game == null)
        {
            return NotFound(new { success = false, message = $"未找到代码为 {request.RoomCode} 的游戏" });
        }

        // 查找玩家
        var player = game.GetClientPlayer(request.ClientId);
        if (player == null)
        {
            return NotFound(new { success = false, message = $"未找到玩家" });
        }

        try
        {
            // 获取玩家IP地址
            string ipAddress = "未知";
            try
            {
                var endPoint = player.Client.Connection.EndPoint;
                if (endPoint is IPEndPoint ipEndPoint)
                {
                    ipAddress = ipEndPoint.Address.ToString();
                }
            }
            catch
            {
                // 忽略错误，使用默认值
            }

            // 执行服务器封禁 - 同时封禁IP和FriendCode
            var reason = string.IsNullOrEmpty(request.Reason) ? "服务器封禁" : request.Reason;

            // 封禁IP
            _banService.BanPlayer(
                player.Client.Name,
                null, // FriendCode为null，只封禁IP
                ipAddress,
                reason
            );

            // 封禁FriendCode
            if (!string.IsNullOrEmpty(player.Client.FriendCode))
            {
                _banService.BanPlayer(
                    player.Client.Name,
                    player.Client.FriendCode, // FriendCode
                    null, // IP为null，只封禁FriendCode
                    reason
                );
            }

            // 断开玩家连接
            await player.Client.DisconnectAsync(DisconnectReason.Custom, "账号数据异常，你被迫下线");

            _logger.LogInformation("服务器封禁玩家: {PlayerName} (FriendCode: {FriendCode}, IP: {IP})",
                player.Client.Name, player.Client.FriendCode, ipAddress);

            return Ok(new
            {
                success = true,
                message = $"成功封禁玩家 {player.Client.Name}，该玩家已被断开连接"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "服务器封禁玩家时出错");
            return StatusCode(500, new { success = false, message = $"封禁失败: {ex.Message}" });
        }
    }

    [HttpPost("disconnect")]
    public async Task<IActionResult> DisconnectPlayer([FromBody] PlayerActionRequest request)
    {
        return await HandlePlayerAction(request, "disconnect", async player =>
        {
            var reason = string.IsNullOrEmpty(request.Reason) ? "你被管理员强制断开连接" : request.Reason;
            await player.Client.DisconnectAsync(DisconnectReason.Custom, reason);
            return $"已断开玩家 {player.Client.Name} 的连接.原因: {reason}";
        });
    }

    // 发送聊天消息API
    [HttpPost("chat")]
    public async Task<IActionResult> SendChat([FromBody] ChatRequest request)
    {
        // 身份验证
        var authResult = CheckAuthentication();
        if (authResult != null)
            return authResult;

        // 验证房间代码
        if (!TryParseGameCode(request.RoomCode, out var gameCode))
        {
            return BadRequest(new { success = false, message = "房间代码无效" });
        }

        // 查找游戏
        var game = _gameManager.Find(gameCode);
        if (game == null)
        {
            return NotFound(new { success = false, message = $"未找到代码为 {request.RoomCode} 的游戏" });
        }

        try
        {
            // 添加到聊天记录
            using var writer = game.StartRpc(game.Host.Character.NetId, Api.Net.Inner.RpcCalls.SendChat);
            writer.Write($"<color=#FE9900>服务器管理员消息:</color>\n{request.Message}");
            await game.FinishRpcAsync(writer);

            RoomChatStore.AddMessage(request.RoomCode, "Admin", request.Message);

            return Ok(new
            {
                success = true,
                message = $"成功向房间 {request.RoomCode} 发送消息：{request.Message}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息时出错");
            return StatusCode(500, new
            {
                success = false,
                message = $"发送消息时出错：{ex.Message}"
            });
        }
    }

    // 全局消息发送API
    [HttpPost("global-chat")]
    public async Task<IActionResult> SendGlobalChat([FromBody] GlobalChatRequest request)
    {
        // 身份验证
        var authResult = CheckAuthentication();
        if (authResult != null)
            return authResult;

        try
        {
            var allGames = _gameManager.Games.ToList();
            var successCount = 0;

            foreach (var game in allGames)
            {
                try
                {
                    // 添加到每个房间的聊天记录
                    using var writer = game.StartRpc(game.Host.Character.NetId, Api.Net.Inner.RpcCalls.SendChat);
                    writer.Write($"<color=#FE9900>服务器广播通知:</color>\n{request.Message}");
                    await game.FinishRpcAsync(writer);

                    RoomChatStore.AddMessage(game.Code.ToString(), "Admin", request.Message);

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "向房间 {RoomCode} 发送消息失败", game.Code);
                }
            }

            return Ok(new
            {
                success = true,
                message = $"成功向 {successCount}/{allGames.Count} 个房间发送全局消息：{request.Message}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送全局消息时出错");
            return StatusCode(500, new
            {
                success = false,
                message = $"发送全局消息时出错：{ex.Message}"
            });
        }
    }

    // 辅助方法：处理玩家操作
    private async Task<IActionResult> HandlePlayerAction(PlayerActionRequest request, string actionType, Func<IClientPlayer, Task<string>> action)
    {
        // 身份验证
        var authResult = CheckAuthentication();
        if (authResult != null)
            return authResult;

        // 验证房间代码
        if (!TryParseGameCode(request.RoomCode, out var gameCode))
        {
            return BadRequest(new { success = false, message = "房间代码无效" });
        }

        // 查找游戏
        var game = _gameManager.Find(gameCode);
        if (game == null)
        {
            return NotFound(new { success = false, message = $"未找到代码为 {request.RoomCode} 的游戏" });
        }

        // 查找玩家
        var player = game.GetClientPlayer(request.ClientId);
        if (player == null)
        {
            return NotFound(new { success = false, message = $"未找到玩家" });
        }

        try
        {
            var resultMessage = await action(player);
            _logger.LogInformation(resultMessage);
            return Ok(new { success = true, message = resultMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"处理{actionType}命令时出错");
            return StatusCode(500, new { success = false, message = $"操作失败: {ex.Message}" });
        }
    }

    // 基本身份验证
    private IActionResult CheckAuthentication()
    {
        string authHeader = Request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic "))
        {
            Response.Headers.Add("WWW-Authenticate", "Basic realm=\"Server Admin Console\"");
            return Unauthorized();
        }

        string encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
        string decodedCredentials;
        try
        {
            decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
        }
        catch
        {
            return Unauthorized("无效的凭证格式");
        }

        var parts = decodedCredentials.Split(':', 2);
        if (parts.Length != 2 || parts[0] != _hostInfoConfig.AdminUser || parts[1] != _hostInfoConfig.AdminPassword)
        {
            return Unauthorized("无效的用户名或密码");
        }

        return null;
    }

    // 辅助方法：解析游戏代码
    private bool TryParseGameCode(string roomCodeStr, out GameCode gameCode)
    {
        gameCode = default;
        try
        {
            var roomCodeInt = GameCodeParser.GameNameToInt(roomCodeStr);
            gameCode = new GameCode(roomCodeInt);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // 请求模型
    public class PlayerActionRequest
    {
        public string RoomCode { get; set; } = string.Empty;
        public int ClientId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ManualBanRequest
    {
        public string Type { get; set; } = string.Empty; // "IP" 或 "FriendCode"
        public string Identifier { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class UnbanRequest
    {
        public string Identifier { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "IP" 或 "FriendCode"
    }

    public class ChatRequest
    {
        public string RoomCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class GlobalChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}

// 封禁服务类
public class BanService
{
    private readonly ILogger<BanService> _logger;
    private readonly List<BannedPlayer> _bannedPlayers = new List<BannedPlayer>();
    private readonly string _banFilePath;

    public BanService(ILogger<BanService> logger)
    {
        _logger = logger;
        _banFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Text", "bans.txt");
        LoadBans();
    }

    public class BannedPlayer
    {
        public string PlayerName { get; set; } = string.Empty;
        public string FriendCode { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime BanTime { get; set; } = DateTime.Now;
    }

    // 封禁玩家 - 现在支持单独封禁IP或FriendCode
    public void BanPlayer(string playerName, string friendCode, string ipAddress, string reason)
    {
        // 检查是否已经封禁
        if ((!string.IsNullOrEmpty(friendCode) && _bannedPlayers.Any(p => p.FriendCode == friendCode)) ||
            (!string.IsNullOrEmpty(ipAddress) && _bannedPlayers.Any(p => p.IPAddress == ipAddress)))
        {
            return;
        }

        var bannedPlayer = new BannedPlayer
        {
            PlayerName = playerName,
            FriendCode = friendCode ?? string.Empty,
            IPAddress = ipAddress ?? string.Empty,
            Reason = reason,
            BanTime = DateTime.Now
        };

        _bannedPlayers.Add(bannedPlayer);
        SaveBans();

        _logger.LogInformation("封禁玩家: {PlayerName} (FriendCode: {FriendCode}, IP: {IP})",
            playerName, friendCode, ipAddress);
    }

    // 解封玩家 - 现在支持按类型解封
    public bool UnbanPlayer(string identifier, string type = null)
    {
        BannedPlayer playerToRemove = null;

        if (!string.IsNullOrEmpty(type))
        {
            if (type == "IP")
            {
                playerToRemove = _bannedPlayers.FirstOrDefault(p => p.IPAddress == identifier);
            }
            else if (type == "FriendCode")
            {
                playerToRemove = _bannedPlayers.FirstOrDefault(p => p.FriendCode == identifier);
            }
        }
        else
        {
            // 向后兼容：如果没有指定类型，按原来的逻辑处理
            playerToRemove = _bannedPlayers.FirstOrDefault(p =>
                p.FriendCode == identifier || p.IPAddress == identifier);
        }

        if (playerToRemove != null)
        {
            _bannedPlayers.Remove(playerToRemove);
            SaveBans();

            _logger.LogInformation("解封玩家: {PlayerName} (标识符: {Identifier})",
                playerToRemove.PlayerName, identifier);

            return true;
        }

        return false;
    }

    // 检查玩家是否被封禁
    public bool IsPlayerBanned(string friendCode, string ipAddress)
    {
        return _bannedPlayers.Any(p =>
            (!string.IsNullOrEmpty(p.FriendCode) && p.FriendCode == friendCode) ||
            (!string.IsNullOrEmpty(p.IPAddress) && p.IPAddress == ipAddress));
    }

    // 获取封禁列表
    public List<BannedPlayer> GetBannedPlayers()
    {
        return _bannedPlayers.ToList();
    }

    // 加载封禁列表
    private void LoadBans()
    {
        try
        {
            var directory = Path.GetDirectoryName(_banFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(_banFilePath))
            {
                return;
            }

            var lines = File.ReadAllLines(_banFilePath);
            foreach (var line in lines)
            {
                try
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 5)
                    {
                        _bannedPlayers.Add(new BannedPlayer
                        {
                            PlayerName = parts[0],
                            FriendCode = parts[1],
                            IPAddress = parts[2],
                            Reason = parts[3],
                            BanTime = DateTime.Parse(parts[4])
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析封禁记录时出错: {Line}", line);
                }
            }

            _logger.LogInformation("加载了 {Count} 条封禁记录", _bannedPlayers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载封禁列表时出错");
        }
    }

    // 保存封禁列表
    private void SaveBans()
    {
        try
        {
            var directory = Path.GetDirectoryName(_banFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var lines = _bannedPlayers.Select(p =>
                $"{p.PlayerName}|{p.FriendCode}|{p.IPAddress}|{p.Reason}|{p.BanTime:yyyy-MM-dd HH:mm:ss}");

            File.WriteAllLines(_banFilePath, lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存封禁列表时出错");
        }
    }
}
