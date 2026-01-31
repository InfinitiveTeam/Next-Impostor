using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Impostor.Api.Config;
using Impostor.Api.Games;
using Impostor.Api.Games.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;

namespace Impostor.Server.Http;

[Route("/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly ILogger<AdminController> _logger;
    private readonly IGameManager _gameManager;
    private readonly BanService _banService;
    private readonly HostInfoConfig _hostInfoConfig;
    private readonly HttpClient _httpClient;
    private readonly LanguageService _languageService;

    public AdminController(
        ILogger<AdminController> logger,
        IGameManager gameManager,
        BanService banService,
        IOptions<HostInfoConfig> hostInfoConfig,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _gameManager = gameManager;
        _banService = banService;
        _hostInfoConfig = hostInfoConfig.Value;
        _httpClient = httpClientFactory.CreateClient();
        _languageService = new LanguageService();
    }

    #region 语言支持系统

    // 语言服务类
    public class LanguageService
    {
        private readonly Dictionary<string, Dictionary<string, string>> _translations;
        private string _currentLanguage = "zh-CN";

        public LanguageService()
        {
            _translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["zh-CN"] = CreateChineseTranslations(),
                ["en-US"] = CreateEnglishTranslations()
            };
        }

        private Dictionary<string, string> CreateChineseTranslations()
        {
            return new Dictionary<string, string>
            {
                ["admin_dashboard_title"] = "服务器管理后台",
                ["admin_dashboard_subtitle"] = "实时监控服务器状态和玩家信息",
                ["total_rooms"] = "总游戏房间",
                ["online_players"] = "在线玩家",
                ["active_games"] = "进行中游戏",
                ["waiting_rooms"] = "等待中房间",
                ["banned_items"] = "已封禁项目",
                ["refresh"] = "刷新",
                ["details"] = "详细信息",
                ["manage"] = "管理",
                ["kick"] = "踢出",
                ["ban"] = "封禁",
                ["server_ban"] = "服务器封禁",
                ["disconnect"] = "断开连接",
                ["unban"] = "解封",
                ["broadcast"] = "广播消息",
                ["send"] = "发送",
                ["player_name"] = "玩家",
                ["friend_code"] = "好友代码",
                ["ip_address"] = "IP地址",
                ["location"] = "地理位置",
                ["room_code"] = "房间代码",
                ["status"] = "状态",
                ["actions"] = "操作",
                ["map"] = "地图",
                ["impostors"] = "内鬼数量",
                ["host"] = "房主",
                ["players"] = "玩家",
                ["status_lobby"] = "大厅中",
                ["status_ingame"] = "游戏中",
                ["status_waiting"] = "等待中",
                ["ban_management"] = "封禁管理",
                ["manual_ban"] = "手动添加封禁",
                ["ban_type"] = "封禁类型",
                ["ban_identifier"] = "标识符",
                ["ban_player_name"] = "玩家名称（可选）",
                ["ban_reason"] = "封禁原因",
                ["ip_address_ban"] = "IP地址",
                ["friend_code_ban"] = "好友代码",
                ["view_ban_list"] = "查看封禁列表",
                ["confirm_ban"] = "确定要封禁{0}：{1}吗？",
                ["ban_added"] = "封禁添加成功！",
                ["global_message"] = "全局消息广播",
                ["global_message_placeholder"] = "输入要发送到所有房间的消息...",
                ["confirm_global_message"] = "确定要向所有房间发送这条消息吗？",
                ["message_sent"] = "消息发送成功！",
                ["player_details"] = "玩家详细信息",
                ["player_id"] = "玩家ID",
                ["game_version"] = "游戏版本",
                ["platform"] = "平台",
                ["chat_mode"] = "聊天模式",
                ["room_management"] = "房间管理",
                ["room_code_title"] = "房间代码",
                ["players_count"] = "玩家数量",
                ["host_name"] = "房主",
                ["room_chat"] = "房间聊天",
                ["enter_message"] = "输入消息...",
                ["room_not_found"] = "房间不存在",
                ["room_invalid"] = "房间代码无效或房间已不存在",
                ["auto_redirect"] = "秒后自动返回管理后台...",
                ["return_now"] = "立即返回管理后台",
                ["enter_reason"] = "请输入{0}原因（可选）",
                ["operation_success"] = "操作成功",
                ["operation_failed"] = "操作失败",
                ["no_players"] = "暂无在线玩家",
                ["no_players_desc"] = "当前没有玩家在线",
                ["no_rooms"] = "暂无游戏房间",
                ["no_rooms_desc"] = "当前没有活跃的游戏房间",
                ["no_bans"] = "暂无封禁项目",
                ["no_bans_desc"] = "当前没有封禁记录",
                ["tip_change_credentials"] = "提示：请修改默认的管理员用户名和密码",
                ["loading"] = "加载中...",
                ["error"] = "错误",
                ["success"] = "成功",
                ["close"] = "关闭",
                ["view_all_bans"] = "查看全部 {0} 个封禁",
                ["ban_list_refreshed"] = "封禁列表已刷新",
                ["back_to_dashboard"] = "返回管理后台",
                ["enter_message_prompt"] = "请输入要发送到房间的消息:",
                ["feature_development"] = "消息发送功能正在开发中",
                ["dark_mode"] = "深色模式",
                ["light_mode"] = "浅色模式",
                ["toggle_dark_mode"] = "切换深色模式"
            };
        }

        private Dictionary<string, string> CreateEnglishTranslations()
        {
            return new Dictionary<string, string>
            {
                ["admin_dashboard_title"] = "Server Admin Dashboard",
                ["admin_dashboard_subtitle"] = "Real-time monitoring of server status and player information",
                ["total_rooms"] = "Total Game Rooms",
                ["online_players"] = "Online Players",
                ["active_games"] = "Active Games",
                ["waiting_rooms"] = "Waiting Rooms",
                ["banned_items"] = "Banned Items",
                ["refresh"] = "Refresh",
                ["details"] = "Details",
                ["manage"] = "Manage",
                ["kick"] = "Kick",
                ["ban"] = "Ban",
                ["server_ban"] = "Server Ban",
                ["disconnect"] = "Disconnect",
                ["unban"] = "Unban",
                ["broadcast"] = "Broadcast",
                ["send"] = "Send",
                ["player_name"] = "Player",
                ["friend_code"] = "Friend Code",
                ["ip_address"] = "IP Address",
                ["location"] = "Location",
                ["room_code"] = "Room Code",
                ["status"] = "Status",
                ["actions"] = "Actions",
                ["map"] = "Map",
                ["impostors"] = "Impostors",
                ["host"] = "Host",
                ["players"] = "Players",
                ["status_lobby"] = "In Lobby",
                ["status_ingame"] = "In Game",
                ["status_waiting"] = "Waiting",
                ["ban_management"] = "Ban Management",
                ["manual_ban"] = "Add Manual Ban",
                ["ban_type"] = "Ban Type",
                ["ban_identifier"] = "Identifier",
                ["ban_player_name"] = "Player Name (Optional)",
                ["ban_reason"] = "Ban Reason",
                ["ip_address_ban"] = "IP Address",
                ["friend_code_ban"] = "Friend Code",
                ["view_ban_list"] = "View Ban List",
                ["confirm_ban"] = "Are you sure you want to ban {0}: {1}?",
                ["ban_added"] = "Ban added successfully!",
                ["global_message"] = "Global Message Broadcast",
                ["global_message_placeholder"] = "Enter message to send to all rooms...",
                ["confirm_global_message"] = "Are you sure you want to send this message to all rooms?",
                ["message_sent"] = "Message sent successfully!",
                ["player_details"] = "Player Details",
                ["player_id"] = "Player ID",
                ["game_version"] = "Game Version",
                ["platform"] = "Platform",
                ["chat_mode"] = "Chat Mode",
                ["room_management"] = "Room Management",
                ["room_code_title"] = "Room Code",
                ["players_count"] = "Players",
                ["host_name"] = "Host",
                ["room_chat"] = "Room Chat",
                ["enter_message"] = "Enter message...",
                ["room_not_found"] = "Room Not Found",
                ["room_invalid"] = "Room code is invalid or room no longer exists",
                ["auto_redirect"] = "seconds before returning to admin dashboard...",
                ["return_now"] = "Return to Admin Dashboard Now",
                ["enter_reason"] = "Enter {0} reason (optional)",
                ["operation_success"] = "Operation successful",
                ["operation_failed"] = "Operation failed",
                ["no_players"] = "No Online Players",
                ["no_players_desc"] = "Currently no players online",
                ["no_rooms"] = "No Game Rooms",
                ["no_rooms_desc"] = "Currently no active game rooms",
                ["no_bans"] = "No Banned Items",
                ["no_bans_desc"] = "Currently no ban records",
                ["tip_change_credentials"] = "Tip: Please change the default admin username and password",
                ["loading"] = "Loading...",
                ["error"] = "Error",
                ["success"] = "Success",
                ["close"] = "Close",
                ["view_all_bans"] = "View All {0} Bans",
                ["ban_list_refreshed"] = "Ban list refreshed",
                ["back_to_dashboard"] = "Back to Dashboard",
                ["enter_message_prompt"] = "Enter message to send to room:",
                ["feature_development"] = "Message sending feature is under development",
                ["dark_mode"] = "Dark Mode",
                ["light_mode"] = "Light Mode",
                ["toggle_dark_mode"] = "Toggle Dark Mode"
            };
        }

        public string GetCurrentLanguage()
        {
            // 简化实现：默认返回中文
            return "zh-CN";
        }

        public void SetLanguage(string language)
        {
            // 简化实现：只更新当前语言
            _currentLanguage = language;
        }

        public string T(string key, params object[] args)
        {
            var language = _currentLanguage;

            if (_translations.TryGetValue(language, out var langDict) &&
                langDict.TryGetValue(key, out var value))
            {
                return args.Length > 0 ? string.Format(value, args) : value;
            }

            // 回退到中文
            if (language != "zh-CN" &&
                _translations.TryGetValue("zh-CN", out var defaultDict) &&
                defaultDict.TryGetValue(key, out var defaultValue))
            {
                return args.Length > 0 ? string.Format(defaultValue, args) : defaultValue;
            }

            return key; // 如果找不到翻译，返回key本身
        }
    }

    // 语言切换请求类
    public class LanguageChangeRequest
    {
        public string Language { get; set; } = string.Empty;
    }

    // 主题切换请求类
    public class ThemeChangeRequest
    {
        public string Theme { get; set; } = string.Empty;
    }

    #endregion

    #region 聊天消息存储结构

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

    #endregion

    #region 封禁服务类

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

                if (!System.IO.File.Exists(_banFilePath))
                {
                    return;
                }

                var lines = System.IO.File.ReadAllLines(_banFilePath);
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

                System.IO.File.WriteAllLines(_banFilePath, lines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存封禁列表时出错");
            }
        }
    }

    #endregion

    #region IP地理位置服务

    public class IpLocationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public IpLocationService(HttpClient httpClient, ILogger logger = null)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GetLocationAsync(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "未知" || ipAddress == "127.0.0.1")
            {
                return "本地";
            }

            try
            {
                // 使用ip-api.com免费服务
                var response = await _httpClient.GetAsync($"http://ip-api.com/json/{ipAddress}?lang=zh-CN");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                    if (data.status == "success")
                    {
                        string country = data.country;
                        string region = data.regionName;
                        string city = data.city;

                        if (!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(region))
                        {
                            return $"{country} {region} {city}";
                        }
                        else if (!string.IsNullOrEmpty(region))
                        {
                            return $"{country} {region}";
                        }
                        else
                        {
                            return country;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "获取地理位置失败: {IP}", ipAddress);
            }

            return "未知";
        }
    }

    #endregion

    #region API端点

    // 语言切换API
    [HttpPost("set-language")]
    public IActionResult SetLanguage([FromBody] LanguageChangeRequest request)
    {
        try
        {
            Response.Cookies.Append("admin_language", request.Language, new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddYears(1),
                HttpOnly = true,
                SameSite = SameSiteMode.Strict
            });
            _languageService.SetLanguage(request.Language);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"设置语言失败: {ex.Message}" });
        }
    }

    // 主题切换API - 使用Cookie存储主题偏好
    [HttpPost("set-theme")]
    public IActionResult SetTheme([FromBody] ThemeChangeRequest request)
    {
        try
        {
            Response.Cookies.Append("admin_theme", request.Theme, new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddYears(1),
                HttpOnly = true,
                SameSite = SameSiteMode.Strict
            });
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"设置主题失败: {ex.Message}" });
        }
    }

    // API：获取服务器统计数据
    [HttpGet("api/stats")]
    public IActionResult GetStats()
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        var allGames = _gameManager.Games.ToList();
        var activeGames = allGames.Where(game => game.GameState == GameStates.Started).ToList();
        var waitingGames = allGames.Where(game => game.GameState == GameStates.NotStarted && game.PlayerCount < game.Options.MaxPlayers).ToList();
        var totalPlayers = allGames.Sum(game => game.PlayerCount);
        var bannedPlayers = _banService.GetBannedPlayers();

        return Ok(new
        {
            totalRooms = allGames.Count,
            totalPlayers,
            activeGames = activeGames.Count,
            waitingRooms = waitingGames.Count,
            bannedItems = bannedPlayers.Count
        });
    }

    // API：获取玩家列表
    [HttpGet("api/players")]
    public async Task<IActionResult> GetPlayers()
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        var games = _gameManager.Games.ToList();
        var players = await GetPlayerInfoAsync(games);

        return Ok(players.Select(p => new
        {
            id = p.ClientId,
            name = p.Name,
            friendCode = p.FriendCode,
            ipAddress = p.IPAddress,
            location = p.Location,
            gameCode = p.GameCode,
            isInGame = p.IsInGame,
            status = p.IsInGame ? _languageService.T("status_ingame") : _languageService.T("status_lobby")
        }));
    }

    // API：获取游戏房间列表
    [HttpGet("api/games")]
    public IActionResult GetGames()
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        var games = _gameManager.Games.ToList();

        return Ok(games.Select(g => new
        {
            code = g.Code.ToString(),
            playerCount = $"{g.PlayerCount}/{g.Options.MaxPlayers}",
            map = g.Options.Map.ToString(),
            impostors = g.Options.NumImpostors,
            status = g.GameState == GameStates.Started ? _languageService.T("status_ingame") : _languageService.T("status_waiting"),
            host = g.Host?.Client.Name ?? "Unknown",
            isPublic = g.IsPublic
        }));
    }

    // API：获取封禁列表
    [HttpGet("api/bans")]
    public IActionResult GetBans()
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        var bannedPlayers = _banService.GetBannedPlayers();

        return Ok(bannedPlayers.Select(b => new
        {
            playerName = b.PlayerName,
            identifier = string.IsNullOrEmpty(b.FriendCode) ? b.IPAddress : b.FriendCode,
            type = string.IsNullOrEmpty(b.FriendCode) ? "IP" : "FriendCode",
            reason = b.Reason,
            banTime = b.BanTime.ToString("yyyy-MM-dd HH:mm:ss")
        }));
    }

    // API：获取玩家详细信息
    [HttpGet("player/{clientId}")]
    public async Task<IActionResult> GetPlayerDetails(int clientId, [FromQuery] string roomCode)
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        if (!TryParseGameCode(roomCode, out var gameCode))
        {
            return BadRequest(new { success = false, message = _languageService.T("room_invalid") });
        }

        var game = _gameManager.Find(gameCode);
        if (game == null)
        {
            return NotFound(new { success = false, message = $"未找到代码为 {roomCode} 的游戏" });
        }

        var player = game.GetClientPlayer(clientId);
        if (player == null)
        {
            return NotFound(new { success = false, message = $"未找到玩家" });
        }

        string ipAddress = "未知";
        string location = "未知";

        try
        {
            var endPoint = player.Client.Connection.EndPoint;
            if (endPoint is IPEndPoint ipEndPoint)
            {
                ipAddress = ipEndPoint.Address.ToString();

                var locationService = new IpLocationService(_httpClient, _logger);
                location = await locationService.GetLocationAsync(ipAddress);
            }
        }
        catch
        {
            // 忽略错误
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

    #endregion

    #region 主管理面板

    [HttpGet]
    public IActionResult AdminDashboard()
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        // 获取当前语言
        string currentLang = "zh-CN";
        if (Request.Cookies.TryGetValue("admin_language", out var langCookie))
        {
            currentLang = langCookie;
        }
        var isEnglish = currentLang == "en-US";

        // 获取当前主题
        string theme = "light";
        if (Request.Cookies.TryGetValue("admin_theme", out var themeCookie))
        {
            theme = themeCookie;
        }

        // 获取基础数据用于初始渲染
        var allGames = _gameManager.Games.ToList();
        var activeGames = allGames.Where(game => game.GameState == GameStates.Started).ToList();
        var waitingGames = allGames.Where(game => game.GameState == GameStates.NotStarted && game.PlayerCount < game.Options.MaxPlayers).ToList();
        var totalPlayers = allGames.Sum(game => game.PlayerCount);
        var bannedPlayers = _banService.GetBannedPlayers();

        var html = GenerateAdminDashboardHtml(currentLang, isEnglish, theme, allGames.Count, totalPlayers,
            activeGames.Count, waitingGames.Count, bannedPlayers.Count);

        return Content(html, "text/html");
    }

    private string GenerateAdminDashboardHtml(string currentLang, bool isEnglish, string theme,
        int totalRooms, int totalPlayers, int activeGames, int waitingRooms, int bannedItems)
    {
        // 创建一个StringBuilder来构建HTML，避免字符串插值问题
        var sb = new StringBuilder();

        sb.AppendLine($@"<!DOCTYPE html>
<html lang=""{currentLang}"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{(isEnglish ? "Server Admin Dashboard" : "服务器管理后台")}</title>
    
    <!-- Bootstrap CSS -->
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css"" rel=""stylesheet"">
    <!-- Font Awesome -->
    <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"">
    
    <style>
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
        
        /* 深色模式变量 */
        .dark-mode {{
            --primary: #3498db;
            --primary-dark: #2980b9;
            --success: #27ae60;
            --warning: #d35400;
            --danger: #c0392b;
            --dark: #1a252f;
            --light: #34495e;
            --gray: #7f8c8d;
            --text: #ecf0f1;
            --text-light: #bdc3c7;
            --border: #4a6572;
            --shadow: 0 2px 10px rgba(0,0,0,0.3);
            --background: #2c3e50;
            --card-bg: #34495e;
            --input-bg: #2c3e50;
            --table-bg: #34495e;
            --table-hover: #3d566e;
        }}
        
        /* 浅色模式变量 */
        .light-mode {{
            --background: #f5f7fa;
            --card-bg: white;
            --input-bg: white;
            --table-bg: white;
            --table-hover: rgba(52, 152, 219, 0.05);
        }}
        
        body {{
            background-color: var(--background);
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            color: var(--text);
            transition: background-color 0.3s ease, color 0.3s ease;
        }}
        
        .navbar-brand {{
            font-weight: 600;
            font-size: 1.2rem;
        }}
        
        .language-switcher {{
            margin-right: 15px;
        }}
        
        .stat-card {{
            background: var(--card-bg);
            border-radius: 10px;
            padding: 20px;
            box-shadow: var(--shadow);
            transition: transform 0.3s ease;
            height: 100%;
            border-left: 4px solid var(--primary);
        }}
        
        .stat-card:hover {{
            transform: translateY(-5px);
            box-shadow: 0 5px 20px rgba(0,0,0,0.15);
        }}
        
        .stat-value {{
            font-size: 2.2rem;
            font-weight: bold;
            color: var(--primary);
            margin-bottom: 5px;
        }}
        
        .stat-label {{
            color: var(--text-light);
            font-size: 0.9rem;
            text-transform: uppercase;
            letter-spacing: 1px;
        }}
        
        .card {{
            border: none;
            box-shadow: var(--shadow);
            margin-bottom: 20px;
            background-color: var(--card-bg);
            color: var(--text);
            transition: background-color 0.3s ease, color 0.3s ease;
        }}
        
        .card-header {{
            background-color: var(--card-bg);
            border-bottom: 2px solid var(--border);
            font-weight: 600;
            font-size: 1.1rem;
            color: var(--text);
        }}
        
        .table {{
            background-color: var(--table-bg);
            color: var(--text);
        }}
        
        .table-hover tbody tr:hover {{
            background-color: var(--table-hover);
        }}
        
        .form-control, .form-select {{
            background-color: var(--input-bg);
            border-color: var(--border);
            color: var(--text);
        }}
        
        .form-control:focus, .form-select:focus {{
            background-color: var(--input-bg);
            border-color: var(--primary);
            color: var(--text);
            box-shadow: 0 0 0 0.25rem rgba(52, 152, 219, 0.25);
        }}
        
        .badge-status {{
            padding: 5px 12px;
            border-radius: 20px;
            font-size: 0.8rem;
            font-weight: 600;
        }}
        
        .badge-lobby {{
            background-color: rgba(46, 204, 113, 0.1);
            color: var(--success);
        }}
        
        .badge-ingame {{
            background-color: rgba(231, 76, 60, 0.1);
            color: var(--danger);
        }}
        
        .btn-action {{
            padding: 4px 10px;
            font-size: 0.85rem;
            margin: 2px;
        }}
        
        .loading-overlay {{
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(0, 0, 0, 0.8);
            display: flex;
            justify-content: center;
            align-items: center;
            z-index: 9999;
            flex-direction: column;
        }}
        
        .spinner {{
            width: 50px;
            height: 50px;
            border: 5px solid var(--light);
            border-top: 5px solid var(--primary);
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin-bottom: 20px;
        }}
        
        @keyframes spin {{
            0% {{ transform: rotate(0deg); }}
            100% {{ transform: rotate(360deg); }}
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
        
        .modal-content {{
            background-color: var(--card-bg);
            color: var(--text);
        }}
        
        .modal-header {{
            background: linear-gradient(135deg, var(--primary), var(--primary-dark));
            color: white;
        }}
        
        #globalMessage {{
            border-radius: 6px;
            border: 1px solid var(--border);
            padding: 10px 15px;
            background-color: var(--input-bg);
            color: var(--text);
        }}
        
        .chat-message {{
            padding: 10px;
            margin-bottom: 10px;
            border-radius: 8px;
            background-color: rgba(0,0,0,0.05);
            border-left: 3px solid var(--primary);
        }}
        
        .dark-mode .chat-message {{
            background-color: rgba(255,255,255,0.05);
        }}
        
        .chat-message.admin {{
            background-color: rgba(254, 153, 0, 0.1);
            border-left-color: #FE9900;
        }}
        
        .dark-mode .chat-message.admin {{
            background-color: rgba(254, 153, 0, 0.2);
        }}
        
        .message-strikethrough {{
            text-decoration: line-through;
            opacity: 0.7;
        }}
        
        .player-details-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }}
        
        .detail-item {{
            background: rgba(0,0,0,0.05);
            padding: 12px;
            border-radius: 6px;
        }}
        
        .dark-mode .detail-item {{
            background: rgba(255,255,255,0.05);
        }}
        
        .detail-label {{
            font-size: 0.85rem;
            color: var(--text-light);
            margin-bottom: 5px;
        }}
        
        .detail-value {{
            font-size: 1rem;
            font-weight: 500;
        }}
        
        .toast-container {{
            position: fixed;
            top: 20px;
            right: 20px;
            z-index: 10000;
        }}
        
        .toast {{
            min-width: 300px;
            margin-bottom: 10px;
            background-color: var(--card-bg);
            color: var(--text);
        }}
        
        .theme-toggle {{
            margin-right: 10px;
        }}
        
        .theme-toggle .btn {{
            padding: 6px 12px;
        }}
    </style>
</head>
<body class=""{(theme == "dark" ? "dark-mode" : "light-mode")}"">
    <!-- 顶部导航栏 -->
    <nav class=""navbar navbar-expand-lg navbar-dark"" style=""background: linear-gradient(135deg, var(--primary), var(--primary-dark));"">
        <div class=""container-fluid"">
            <a class=""navbar-brand"" href=""/admin"">
                <i class=""fas fa-server me-2""></i>
                <span id=""nav-title"">{(isEnglish ? "Server Admin Dashboard" : "服务器管理后台")}</span>
            </a>
            
            <div class=""d-flex align-items-center"">
                <!-- 主题切换 -->
                <div class=""theme-toggle"">
                    <button type=""button"" class=""btn btn-sm btn-outline-light"" id=""themeToggle"">
                        <i class=""fas {(theme == "dark" ? "fa-sun" : "fa-moon")} me-1""></i>
                        <span id=""theme-text"">{(theme == "dark" ? (isEnglish ? "Light Mode" : "浅色模式") : (isEnglish ? "Dark Mode" : "深色模式"))}</span>
                    </button>
                </div>
                
                <!-- 语言切换 -->
                <div class=""language-switcher"">
                    <div class=""btn-group"" role=""group"">
                        <button type=""button"" class=""btn btn-sm btn-outline-light {(currentLang == "zh-CN" ? "active" : "")}"" onclick=""changeLanguage('zh-CN')"">
                            <i class=""fas fa-language""></i> 中文
                        </button>
                        <button type=""button"" class=""btn btn-sm btn-outline-light {(currentLang == "en-US" ? "active" : "")}"" onclick=""changeLanguage('en-US')"">
                            <i class=""fas fa-language""></i> English
                        </button>
                    </div>
                </div>
                
                <!-- 刷新按钮 -->
                <button class=""btn btn-light btn-sm me-2"" onclick=""loadData()"">
                    <i class=""fas fa-sync-alt""></i>
                    <span id=""refresh-text"">{(isEnglish ? "Refresh" : "刷新")}</span>
                </button>
                
                <!-- 用户名显示 -->
                <span class=""navbar-text text-white"">
                    <i class=""fas fa-user-shield me-1""></i>
                    {_hostInfoConfig.AdminUser}
                </span>
            </div>
        </div>
    </nav>");

        sb.AppendLine($@"    
    <!-- 主内容区 -->
    <div class=""container-fluid mt-4"">
        <!-- 统计卡片行 -->
        <div class=""row mb-4"" id=""stats-container"">
            <div class=""col-md-4 col-lg-2-4 mb-3"">
                <div class=""stat-card"">
                    <div class=""d-flex align-items-center mb-3"">
                        <div class=""rounded-circle p-3 me-3"" style=""background-color: rgba(52, 152, 219, 0.1);"">
                            <i class=""fas fa-door-open fa-2x text-primary""></i>
                        </div>
                        <div>
                            <div class=""stat-value"">{totalRooms}</div>
                            <div class=""stat-label"" id=""stat-total-rooms"">{(isEnglish ? "Total Game Rooms" : "总游戏房间")}</div>
                        </div>
                    </div>
                </div>
            </div>
            <div class=""col-md-4 col-lg-2-4 mb-3"">
                <div class=""stat-card"">
                    <div class=""d-flex align-items-center mb-3"">
                        <div class=""rounded-circle p-3 me-3"" style=""background-color: rgba(46, 204, 113, 0.1);"">
                            <i class=""fas fa-users fa-2x text-success""></i>
                        </div>
                        <div>
                            <div class=""stat-value"">{totalPlayers}</div>
                            <div class=""stat-label"" id=""stat-online-players"">{(isEnglish ? "Online Players" : "在线玩家")}</div>
                        </div>
                    </div>
                </div>
            </div>
            <div class=""col-md-4 col-lg-2-4 mb-3"">
                <div class=""stat-card"">
                    <div class=""d-flex align-items-center mb-3"">
                        <div class=""rounded-circle p-3 me-3"" style=""background-color: rgba(243, 156, 18, 0.1);"">
                            <i class=""fas fa-gamepad fa-2x text-warning""></i>
                        </div>
                        <div>
                            <div class=""stat-value"">{activeGames}</div>
                            <div class=""stat-label"" id=""stat-active-games"">{(isEnglish ? "Active Games" : "进行中游戏")}</div>
                        </div>
                    </div>
                </div>
            </div>
            <div class=""col-md-4 col-lg-2-4 mb-3"">
                <div class=""stat-card"">
                    <div class=""d-flex align-items-center mb-3"">
                        <div class=""rounded-circle p-3 me-3"" style=""background-color: rgba(52, 152, 219, 0.1);"">
                            <i class=""fas fa-clock fa-2x text-info""></i>
                        </div>
                        <div>
                            <div class=""stat-value"">{waitingRooms}</div>
                            <div class=""stat-label"" id=""stat-waiting-rooms"">{(isEnglish ? "Waiting Rooms" : "等待中房间")}</div>
                        </div>
                    </div>
                </div>
            </div>
            <div class=""col-md-4 col-lg-2-4 mb-3"">
                <div class=""stat-card"">
                    <div class=""d-flex align-items-center mb-3"">
                        <div class=""rounded-circle p-3 me-3"" style=""background-color: rgba(231, 76, 60, 0.1);"">
                            <i class=""fas fa-ban fa-2x text-danger""></i>
                        </div>
                        <div>
                            <div class=""stat-value"">{bannedItems}</div>
                            <div class=""stat-label"" id=""stat-banned-items"">{(isEnglish ? "Banned Items" : "已封禁项目")}</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>");

        sb.AppendLine($@"        
        <!-- 全局消息广播 -->
        <div class=""row mb-4"">
            <div class=""col-12"">
                <div class=""card"">
                    <div class=""card-header"">
                        <i class=""fas fa-bullhorn me-2""></i>
                        <span id=""global-message-title"">{(isEnglish ? "Global Message Broadcast" : "全局消息广播")}</span>
                    </div>
                    <div class=""card-body"">
                        <div class=""input-group"">
                            <input type=""text"" class=""form-control"" id=""globalMessage"" 
                                   placeholder=""{(isEnglish ? "Enter message to send to all rooms..." : "输入要发送到所有房间的消息...")}"">
                            <button class=""btn btn-primary"" type=""button"" onclick=""sendGlobalMessage()"">
                                <i class=""fas fa-broadcast-tower me-1""></i>
                                <span id=""broadcast-text"">{(isEnglish ? "Broadcast" : "广播消息")}</span>
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>");

        sb.AppendLine($@"        
        <!-- 主要内容区域：两栏布局 -->
        <div class=""row"">
            <!-- 左侧：玩家信息和封禁管理 -->
            <div class=""col-lg-6"">
                <!-- 玩家信息卡片 -->
                <div class=""card"">
                    <div class=""card-header d-flex justify-content-between align-items-center"">
                        <span>
                            <i class=""fas fa-users me-2""></i>
                            <span id=""players-title"">{(isEnglish ? "Player Information" : "玩家信息")}</span>
                        </span>
                        <span class=""badge bg-primary"" id=""players-count"">0</span>
                    </div>
                    <div class=""card-body p-0"">
                        <div class=""table-responsive"">
                            <table class=""table table-hover mb-0"" id=""players-table"">
                                <thead>
                                    <tr>
                                        <th id=""col-player"">{(isEnglish ? "Player" : "玩家")}</th>
                                        <th id=""col-friendcode"">{(isEnglish ? "Friend Code" : "好友代码")}</th>
                                        <th id=""col-status"">{(isEnglish ? "Status" : "状态")}</th>
                                        <th id=""col-actions"">{(isEnglish ? "Actions" : "操作")}</th>
                                    </tr>
                                </thead>
                                <tbody id=""players-body"">
                                    <!-- 玩家数据将通过JavaScript动态加载 -->
                                </tbody>
                            </table>
                        </div>
                        <div class=""text-center p-4"" id=""players-empty"" style=""display: none;"">
                            <i class=""fas fa-user-slash fa-3x text-muted mb-3""></i>
                            <h5 id=""no-players-text"">{(isEnglish ? "No Online Players" : "暂无在线玩家")}</h5>
                            <p class=""text-muted"" id=""no-players-desc"">{(isEnglish ? "Currently no players online" : "当前没有玩家在线")}</p>
                        </div>
                    </div>
                </div>");

        sb.AppendLine($@"                
                <!-- 封禁管理卡片 -->
                <div class=""card mt-4"">
                    <div class=""card-header"">
                        <i class=""fas fa-ban me-2""></i>
                        <span id=""ban-management-title"">{(isEnglish ? "Ban Management" : "封禁管理")}</span>
                    </div>
                    <div class=""card-body"">
                        <!-- 手动封禁表单 -->
                        <div class=""p-3 mb-4"" style=""background-color: rgba(0,0,0,0.05); border-radius: 8px;"">
                            <h6 id=""manual-ban-title"">{(isEnglish ? "Add Manual Ban" : "手动添加封禁")}</h6>
                            <div class=""row g-3"">
                                <div class=""col-md-6"">
                                    <label class=""form-label"" id=""label-ban-type"">{(isEnglish ? "Ban Type" : "封禁类型")}</label>
                                    <select class=""form-select"" id=""banType"">
                                        <option value=""IP"">{(isEnglish ? "IP Address" : "IP地址")}</option>
                                        <option value=""FriendCode"">{(isEnglish ? "Friend Code" : "好友代码")}</option>
                                    </select>
                                </div>
                                <div class=""col-md-6"">
                                    <label class=""form-label"" id=""label-identifier"">{(isEnglish ? "Identifier" : "标识符")}</label>
                                    <input type=""text"" class=""form-control"" id=""banIdentifier"" 
                                           placeholder=""{(isEnglish ? "Enter IP or Friend Code" : "输入IP地址或好友代码")}"">
                                </div>
                                <div class=""col-md-6"">
                                    <label class=""form-label"" id=""label-player-name"">{(isEnglish ? "Player Name (Optional)" : "玩家名称（可选）")}</label>
                                    <input type=""text"" class=""form-control"" id=""banPlayerName"" 
                                           placeholder=""{(isEnglish ? "Enter player name" : "输入玩家名称")}"">
                                </div>
                                <div class=""col-md-6"">
                                    <label class=""form-label"" id=""label-ban-reason"">{(isEnglish ? "Ban Reason" : "封禁原因")}</label>
                                    <input type=""text"" class=""form-control"" id=""banReason"" 
                                           placeholder=""{(isEnglish ? "Enter ban reason" : "输入封禁原因")}"">
                                </div>
                                <div class=""col-12"">
                                    <div class=""d-flex justify-content-end"">
                                        <button class=""btn btn-danger"" onclick=""addManualBan()"">
                                            <i class=""fas fa-ban me-1""></i>
                                            <span id=""add-ban-text"">{(isEnglish ? "Add Ban" : "添加封禁")}</span>
                                        </button>
                                    </div>
                                </div>
                            </div>
                        </div>
                        
                        <!-- 封禁列表 -->
                        <div class=""d-flex justify-content-between align-items-center mb-3"">
                            <h6 id=""ban-list-title"">{(isEnglish ? "Ban List" : "封禁列表")}</h6>
                            <button class=""btn btn-outline-danger btn-sm"" onclick=""loadBanList()"">
                                <i class=""fas fa-sync-alt me-1""></i>
                                <span id=""refresh-bans-text"">{(isEnglish ? "Refresh" : "刷新")}</span>
                            </button>
                        </div>
                        
                        <div id=""ban-list-container"">
                            <!-- 封禁列表将通过JavaScript动态加载 -->
                        </div>
                        
                        <div class=""text-center p-3"" id=""bans-empty"" style=""display: none;"">
                            <i class=""fas fa-check-circle fa-2x text-success mb-2""></i>
                            <p class=""text-muted mb-0"" id=""no-bans-text"">{(isEnglish ? "No ban records" : "当前没有封禁记录")}</p>
                        </div>
                    </div>
                </div>
            </div>");

        sb.AppendLine($@"            
            <!-- 右侧：游戏房间 -->
            <div class=""col-lg-6"">
                <!-- 游戏房间卡片 -->
                <div class=""card"">
                    <div class=""card-header d-flex justify-content-between align-items-center"">
                        <span>
                            <i class=""fas fa-door-open me-2""></i>
                            <span id=""rooms-title"">{(isEnglish ? "Game Rooms" : "游戏房间")}</span>
                        </span>
                        <span class=""badge bg-primary"" id=""rooms-count"">0</span>
                    </div>
                    <div class=""card-body p-0"">
                        <div class=""table-responsive"">
                            <table class=""table table-hover mb-0"" id=""rooms-table"">
                                <thead>
                                    <tr>
                                        <th id=""col-room-code"">{(isEnglish ? "Room Code" : "房间代码")}</th>
                                        <th id=""col-players-count"">{(isEnglish ? "Players" : "玩家")}</th>
                                        <th id=""col-room-status"">{(isEnglish ? "Status" : "状态")}</th>
                                        <th id=""col-room-actions"">{(isEnglish ? "Actions" : "操作")}</th>
                                    </tr>
                                </thead>
                                <tbody id=""rooms-body"">
                                    <!-- 房间数据将通过JavaScript动态加载 -->
                                </tbody>
                            </table>
                        </div>
                        <div class=""text-center p-4"" id=""rooms-empty"" style=""display: none;"">
                            <i class=""fas fa-door-closed fa-3x text-muted mb-3""></i>
                            <h5 id=""no-rooms-text"">{(isEnglish ? "No Game Rooms" : "暂无游戏房间")}</h5>
                            <p class=""text-muted"" id=""no-rooms-desc"">{(isEnglish ? "Currently no active game rooms" : "当前没有活跃的游戏房间")}</p>
                        </div>
                    </div>
                </div>
                
                <!-- 最近聊天记录卡片 -->
                <div class=""card mt-4"">
                    <div class=""card-header"">
                        <i class=""fas fa-comments me-2""></i>
                        <span id=""recent-chat-title"">{(isEnglish ? "Recent Chat" : "最近聊天")}</span>
                    </div>
                    <div class=""card-body"" style=""max-height: 400px; overflow-y: auto;"">
                        <div id=""chat-container"">
                            <!-- 聊天记录将通过JavaScript动态加载 -->
                        </div>
                        <div class=""text-center p-3"" id=""chat-empty"" style=""display: none;"">
                            <i class=""fas fa-comment-slash fa-2x text-muted mb-2""></i>
                            <p class=""text-muted mb-0"" id=""no-chat-text"">{(isEnglish ? "No chat messages" : "暂无聊天记录")}</p>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>");

        sb.AppendLine($@"    
    <!-- 加载遮罩 -->
    <div class=""loading-overlay"" id=""loading-overlay"">
        <div class=""spinner""></div>
        <p id=""loading-text"">{(isEnglish ? "Loading..." : "加载中...")}</p>
    </div>");

        sb.AppendLine($@"    
    <!-- 玩家详情模态框 -->
    <div class=""modal fade"" id=""playerModal"" tabindex=""-1"" aria-hidden=""true"">
        <div class=""modal-dialog modal-lg"">
            <div class=""modal-content"">
                <div class=""modal-header"">
                    <h5 class=""modal-title"">
                        <i class=""fas fa-user-circle me-2""></i>
                        <span id=""player-details-title"">{(isEnglish ? "Player Details" : "玩家详细信息")}</span>
                    </h5>
                    <button type=""button"" class=""btn-close btn-close-white"" data-bs-dismiss=""modal"" aria-label=""Close""></button>
                </div>
                <div class=""modal-body"">
                    <div class=""player-details-grid"" id=""playerDetails"">
                        <!-- 玩家详情将通过JavaScript动态加载 -->
                    </div>
                    <div class=""d-flex justify-content-end gap-2"">
                        <button type=""button"" class=""btn btn-warning"" onclick=""performAction('kick')"">
                            <i class=""fas fa-user-minus me-1""></i>
                            <span id=""kick-text"">{(isEnglish ? "Kick" : "踢出")}</span>
                        </button>
                        <button type=""button"" class=""btn btn-danger"" onclick=""performAction('ban')"">
                            <i class=""fas fa-ban me-1""></i>
                            <span id=""ban-text"">{(isEnglish ? "Ban" : "封禁")}</span>
                        </button>
                        <button type=""button"" class=""btn btn-danger"" onclick=""performAction('serverban')"">
                            <i class=""fas fa-gavel me-1""></i>
                            <span id=""server-ban-text"">{(isEnglish ? "Server Ban" : "服务器封禁")}</span>
                        </button>
                        <button type=""button"" class=""btn btn-info"" onclick=""performAction('disconnect')"">
                            <i class=""fas fa-plug me-1""></i>
                            <span id=""disconnect-text"">{(isEnglish ? "Disconnect" : "断开连接")}</span>
                        </button>
                    </div>
                </div>
            </div>
        </div>
    </div>");

        sb.AppendLine($@"    
    <!-- 封禁列表模态框 -->
    <div class=""modal fade"" id=""banModal"" tabindex=""-1"" aria-hidden=""true"">
        <div class=""modal-dialog modal-lg"">
            <div class=""modal-content"">
                <div class=""modal-header"">
                    <h5 class=""modal-title"">
                        <i class=""fas fa-list me-2""></i>
                        <span id=""ban-list-modal-title"">{(isEnglish ? "Ban List" : "封禁列表")}</span>
                    </h5>
                    <button type=""button"" class=""btn-close btn-close-white"" data-bs-dismiss=""modal"" aria-label=""Close""></button>
                </div>
                <div class=""modal-body"">
                    <div id=""ban-list-modal-content"">
                        <!-- 封禁列表将通过JavaScript动态加载 -->
                    </div>
                </div>
                <div class=""modal-footer"">
                    <button type=""button"" class=""btn btn-secondary"" data-bs-dismiss=""modal"">
                        <i class=""fas fa-times me-1""></i>
                        <span id=""close-text"">{(isEnglish ? "Close" : "关闭")}</span>
                    </button>
                </div>
            </div>
        </div>
    </div>");

        sb.AppendLine($@"    
    <!-- Bootstrap JS -->
    <script src=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js""></script>
    
    <!-- 主JavaScript代码 -->
    <script>
        let currentLanguage = '{currentLang}';
        let currentTheme = '{theme}';
        let currentPlayer = null;
        
        // 页面加载完成后执行
        document.addEventListener('DOMContentLoaded', function() {{
            // 加载数据
            loadData();
            
            // 设置自动刷新（每30秒）
            setInterval(loadData, 30000);
        }});
        
        // 切换主题
        function toggleTheme() {{
            const newTheme = currentTheme === 'light' ? 'dark' : 'light';
            currentTheme = newTheme;
            
            // 更新页面类
            document.body.classList.remove('light-mode', 'dark-mode');
            document.body.classList.add(newTheme + '-mode');
            
            // 更新按钮文本和图标
            const themeToggle = document.getElementById('themeToggle');
            const themeText = document.getElementById('theme-text');
            const icon = themeToggle.querySelector('i');
            
            if (newTheme === 'dark') {{
                themeText.textContent = currentLanguage === 'zh-CN' ? '浅色模式' : 'Light Mode';
                icon.classList.remove('fa-moon');
                icon.classList.add('fa-sun');
            }} else {{
                themeText.textContent = currentLanguage === 'zh-CN' ? '深色模式' : 'Dark Mode';
                icon.classList.remove('fa-sun');
                icon.classList.add('fa-moon');
            }}
            
            // 保存主题到cookie
            fetch('/admin/set-theme', {{
                method: 'POST',
                headers: {{
                    'Content-Type': 'application/json',
                    'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                }},
                body: JSON.stringify({{ theme: newTheme }})
            }});
        }}
        
        // 设置主题切换按钮事件
        document.getElementById('themeToggle').addEventListener('click', toggleTheme);
        
        // 切换语言
        async function changeLanguage(lang) {{
            try {{
                const response = await fetch('/admin/set-language', {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json',
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }},
                    body: JSON.stringify({{ language: lang }})
                }});
                
                if (response.ok) {{
                    currentLanguage = lang;
                    location.reload();
                }}
            }} catch (error) {{
                console.error('Failed to change language:', error);
                showError(currentLanguage === 'zh-CN' ? '切换语言失败' : 'Failed to change language');
            }}
        }}
        
        // 显示加载动画
        function showLoading() {{
            document.getElementById('loading-overlay').style.display = 'flex';
        }}
        
        // 隐藏加载动画
        function hideLoading() {{
            document.getElementById('loading-overlay').style.display = 'none';
        }}
        
        // 显示错误消息
        function showError(message) {{
            // 创建或获取toast容器
            let toastContainer = document.getElementById('toast-container');
            if (!toastContainer) {{
                toastContainer = document.createElement('div');
                toastContainer.id = 'toast-container';
                toastContainer.className = 'toast-container';
                document.body.appendChild(toastContainer);
            }}
            
            // 创建toast
            const toastId = 'toast-' + Date.now();
            const toast = document.createElement('div');
            toast.id = toastId;
            toast.className = 'toast show';
            toast.innerHTML = `
                <div class=""toast-header bg-danger text-white"">
                    <strong class=""me-auto""><i class=""fas fa-exclamation-triangle me-2""></i>${{currentLanguage === 'zh-CN' ? '错误' : 'Error'}}</strong>
                    <button type=""button"" class=""btn-close btn-close-white"" onclick=""document.getElementById('${{toastId}}').remove()""></button>
                </div>
                <div class=""toast-body"">${{message}}</div>
            `;
            
            toastContainer.appendChild(toast);
            
            // 5秒后自动移除
            setTimeout(() => {{
                if (document.getElementById(toastId)) {{
                    document.getElementById(toastId).remove();
                }}
            }}, 5000);
        }}
        
        // 显示成功消息
        function showSuccess(message) {{
            // 创建或获取toast容器
            let toastContainer = document.getElementById('toast-container');
            if (!toastContainer) {{
                toastContainer = document.createElement('div');
                toastContainer.id = 'toast-container';
                toastContainer.className = 'toast-container';
                document.body.appendChild(toastContainer);
            }}
            
            // 创建toast
            const toastId = 'toast-' + Date.now();
            const toast = document.createElement('div');
            toast.id = toastId;
            toast.className = 'toast show';
            toast.innerHTML = `
                <div class=""toast-header bg-success text-white"">
                    <strong class=""me-auto""><i class=""fas fa-check-circle me-2""></i>${{currentLanguage === 'zh-CN' ? '成功' : 'Success'}}</strong>
                    <button type=""button"" class=""btn-close btn-close-white"" onclick=""document.getElementById('${{toastId}}').remove()""></button>
                </div>
                <div class=""toast-body"">${{message}}</div>
            `;
            
            toastContainer.appendChild(toast);
            
            // 3秒后自动移除
            setTimeout(() => {{
                if (document.getElementById(toastId)) {{
                    document.getElementById(toastId).remove();
                }}
            }}, 3000);
        }}
        
        // 加载所有数据
        async function loadData() {{
            try {{
                showLoading();
                
                // 并行加载所有数据
                await Promise.all([
                    loadStats(),
                    loadPlayers(),
                    loadRooms(),
                    loadBans()
                ]);
                
                hideLoading();
            }} catch (error) {{
                hideLoading();
                console.error('Failed to load data:', error);
                showError(currentLanguage === 'zh-CN' ? '加载数据失败' : 'Failed to load data');
            }}
        }}
        
        // 加载统计数据
        async function loadStats() {{
            try {{
                const response = await fetch('/admin/api/stats', {{
                    headers: {{
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }}
                }});
                
                if (!response.ok) throw new Error('Network response was not ok');
                
                const data = await response.json();
                
                // 更新统计卡片
                document.querySelectorAll('.stat-value')[0].textContent = data.totalRooms;
                document.querySelectorAll('.stat-value')[1].textContent = data.totalPlayers;
                document.querySelectorAll('.stat-value')[2].textContent = data.activeGames;
                document.querySelectorAll('.stat-value')[3].textContent = data.waitingRooms;
                document.querySelectorAll('.stat-value')[4].textContent = data.bannedItems;
            }} catch (error) {{
                console.error('Failed to load stats:', error);
                throw error;
            }}
        }}
        
        // 加载玩家列表
        async function loadPlayers() {{
            try {{
                const response = await fetch('/admin/api/players', {{
                    headers: {{
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }}
                }});
                
                if (!response.ok) throw new Error('Network response was not ok');
                
                const players = await response.json();
                const playersBody = document.getElementById('players-body');
                const playersEmpty = document.getElementById('players-empty');
                const playersCount = document.getElementById('players-count');
                
                playersCount.textContent = players.length;
                
                if (players.length === 0) {{
                    playersBody.innerHTML = '';
                    playersEmpty.style.display = 'block';
                }} else {{
                    playersEmpty.style.display = 'none';
                    
                    const rows = players.map(player => `
                        <tr>
                            <td>
                                <div class=""d-flex align-items-center"">
                                    <div class=""player-avatar"">
                                        ${{player.name.charAt(0).toUpperCase()}}
                                    </div>
                                    <div>
                                        <div style=""font-weight: 600;"">${{player.name}}</div>
                                        <div style=""font-size: 0.8em; color: var(--text-light);"">${{player.ipAddress}} | ${{player.location}}</div>
                                    </div>
                                </div>
                            </td>
                            <td>
                                <code>${{player.friendCode}}</code>
                            </td>
                            <td>
                                <span class=""badge-status ${{player.isInGame ? 'badge-ingame' : 'badge-lobby'}}"">
                                    ${{player.status}}
                                </span>
                            </td>
                            <td>
                                <div class=""d-flex flex-wrap"">
                                    <button class=""btn btn-sm btn-outline-primary btn-action"" onclick=""showPlayerDetails('${{player.id}}', '${{player.gameCode}}')"">
                                        <i class=""fas fa-info-circle""></i>
                                    </button>
                                    <button class=""btn btn-sm btn-outline-warning btn-action"" onclick=""performPlayerAction('kick', ${{player.id}}, '${{player.gameCode}}')"">
                                        <i class=""fas fa-user-minus""></i>
                                    </button>
                                    <button class=""btn btn-sm btn-outline-danger btn-action"" onclick=""performPlayerAction('ban', ${{player.id}}, '${{player.gameCode}}')"">
                                        <i class=""fas fa-ban""></i>
                                    </button>
                                </div>
                            </td>
                        </tr>
                    `).join('');
                    
                    playersBody.innerHTML = rows;
                }}
            }} catch (error) {{
                console.error('Failed to load players:', error);
                throw error;
            }}
        }}
        
        // 加载游戏房间
        async function loadRooms() {{
            try {{
                const response = await fetch('/admin/api/games', {{
                    headers: {{
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }}
                }});
                
                if (!response.ok) throw new Error('Network response was not ok');
                
                const games = await response.json();
                const roomsBody = document.getElementById('rooms-body');
                const roomsEmpty = document.getElementById('rooms-empty');
                const roomsCount = document.getElementById('rooms-count');
                
                roomsCount.textContent = games.length;
                
                if (games.length === 0) {{
                    roomsBody.innerHTML = '';
                    roomsEmpty.style.display = 'block';
                }} else {{
                    roomsEmpty.style.display = 'none';
                    
                    const rows = games.map(game => `
                        <tr>
                            <td>
                                <a href=""javascript:void(0);"" onclick=""showRoomDetails('${{game.code}}')"" style=""text-decoration: none; font-weight: 600; color: var(--primary);"">
                                    <i class=""fas fa-external-link-alt me-1""></i>
                                    ${{game.code}}
                                </a>
                            </td>
                            <td>${{game.playerCount}}</td>
                            <td>
                                <span class=""badge-status ${{game.status.includes('Game') ? 'badge-ingame' : 'badge-lobby'}}"">
                                    ${{game.status}}
                                </span>
                            </td>
                            <td>
                                <div class=""d-flex flex-wrap"">
                                    <a href=""/admin/room/${{game.code}}"" class=""btn btn-sm btn-outline-primary btn-action"" target=""_blank"">
                                        <i class=""fas fa-cog""></i>
                                    </a>
                                    <button class=""btn btn-sm btn-outline-warning btn-action"" onclick=""sendRoomMessage('${{game.code}}')"">
                                        <i class=""fas fa-comment""></i>
                                    </button>
                                </div>
                            </td>
                        </tr>
                    `).join('');
                    
                    roomsBody.innerHTML = rows;
                }}
            }} catch (error) {{
                console.error('Failed to load rooms:', error);
                throw error;
            }}
        }}
        
        // 加载封禁列表
        async function loadBans() {{
            try {{
                const response = await fetch('/admin/api/bans', {{
                    headers: {{
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }}
                }});
                
                if (!response.ok) throw new Error('Network response was not ok');
                
                const bans = await response.json();
                const banListContainer = document.getElementById('ban-list-container');
                const bansEmpty = document.getElementById('bans-empty');
                
                if (bans.length === 0) {{
                    banListContainer.innerHTML = '';
                    bansEmpty.style.display = 'block';
                }} else {{
                    bansEmpty.style.display = 'none';
                    
                    // 只显示前5个封禁
                    const recentBans = bans.slice(0, 5);
                    
                    const banItems = recentBans.map(ban => `
                        <div class=""d-flex justify-content-between align-items-center p-3 border-bottom"">
                            <div>
                                <div class=""fw-bold"">${{ban.playerName}}</div>
                                <div class=""text-muted small"">
                                    <i class=""fas fa-${{ban.type === 'IP' ? 'network-wired' : 'address-card'}} me-1""></i>
                                    ${{ban.identifier}}
                                </div>
                                <div class=""small mt-1"">
                                    <span class=""badge bg-danger me-2"">${{ban.type === 'IP' ? (currentLanguage === 'zh-CN' ? 'IP封禁' : 'IP Ban') : (currentLanguage === 'zh-CN' ? '好友代码' : 'Friend Code')}}</span>
                                    ${{ban.reason}}
                                </div>
                            </div>
                            <button class=""btn btn-sm btn-success"" onclick=""unbanPlayer('${{ban.identifier}}', '${{ban.type}}')"">
                                <i class=""fas fa-unlock""></i>
                            </button>
                        </div>
                    `).join('');
                    
                    banListContainer.innerHTML = banItems;
                    
                    // 添加查看全部按钮
                    if (bans.length > 5) {{
                        banListContainer.innerHTML += `
                            <div class=""text-center p-2"">
                                <button class=""btn btn-sm btn-outline-danger"" onclick=""showBanModal()"">
                                    ${{currentLanguage === 'zh-CN' ? `查看全部 ${{bans.length}} 个封禁` : `View All ${{bans.length}} Bans`}}
                                </button>
                            </div>
                        `;
                    }}
                }}
            }} catch (error) {{
                console.error('Failed to load bans:', error);
                throw error;
            }}
        }}
        
        // 显示玩家详情
        async function showPlayerDetails(playerId, roomCode) {{
            try {{
                const response = await fetch('/admin/player/' + playerId + '?roomCode=' + roomCode, {{
                    headers: {{
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }}
                }});
                
                if (!response.ok) throw new Error('Network response was not ok');
                
                const data = await response.json();
                
                if (data.success) {{
                    const player = data.player;
                    currentPlayer = {{ id: playerId, roomCode, player }};
                    
                    const detailsHtml = `
                        <div class=""detail-item"">
                            <div class=""detail-label"">${{currentLanguage === 'zh-CN' ? '玩家名称' : 'Player Name'}}</div>
                            <div class=""detail-value"">${{player.name}}</div>
                        </div>
                        <div class=""detail-item"">
                            <div class=""detail-label"">${{currentLanguage === 'zh-CN' ? '玩家ID' : 'Player ID'}}</div>
                            <div class=""detail-value"">${{player.clientId}}</div>
                        </div>
                        <div class=""detail-item"">
                            <div class=""detail-label"">Friend Code</div>
                            <div class=""detail-value"">${{player.friendCode || 'N/A'}}</div>
                        </div>
                        <div class=""detail-item"">
                            <div class=""detail-label"">${{currentLanguage === 'zh-CN' ? 'IP地址' : 'IP Address'}}</div>
                            <div class=""detail-value"">${{player.ipAddress || 'Unknown'}}</div>
                        </div>
                        <div class=""detail-item"">
                            <div class=""detail-label"">${{currentLanguage === 'zh-CN' ? '地理位置' : 'Location'}}</div>
                            <div class=""detail-value"">${{player.location || 'Unknown'}}</div>
                        </div>
                        <div class=""detail-item"">
                            <div class=""detail-label"">${{currentLanguage === 'zh-CN' ? '所在房间' : 'Room'}}</div>
                            <div class=""detail-value"">${{player.gameCode}}</div>
                        </div>
                        <div class=""detail-item"">
                            <div class=""detail-label"">${{currentLanguage === 'zh-CN' ? '游戏版本' : 'Game Version'}}</div>
                            <div class=""detail-value"">${{player.clientVersion || 'Unknown'}}</div>
                        </div>
                        <div class=""detail-item"">
                            <div class=""detail-label"">${{currentLanguage === 'zh-CN' ? '平台' : 'Platform'}}</div>
                            <div class=""detail-value"">${{player.platform || 'Unknown'}}</div>
                        </div>
                    `;
                    
                    document.getElementById('playerDetails').innerHTML = detailsHtml;
                    
                    // 显示模态框
                    const modal = new bootstrap.Modal(document.getElementById('playerModal'));
                    modal.show();
                }} else {{
                    showError(data.message || (currentLanguage === 'zh-CN' ? '获取玩家信息失败' : 'Failed to get player details'));
                }}
            }} catch (error) {{
                console.error('Failed to get player details:', error);
                showError(currentLanguage === 'zh-CN' ? '获取玩家信息失败' : 'Failed to get player details');
            }}
        }}
        
        // 显示房间详情
        function showRoomDetails(roomCode) {{
            // 在新窗口打开房间详情页
            window.open('/admin/room/' + roomCode, '_blank');
        }}
        
        // 执行玩家操作
        async function performPlayerAction(action, playerId, roomCode) {{
            const actionText = {{
                'kick': currentLanguage === 'zh-CN' ? '踢出' : 'kick',
                'ban': currentLanguage === 'zh-CN' ? '封禁' : 'ban',
                'serverban': currentLanguage === 'zh-CN' ? '服务器封禁' : 'server ban',
                'disconnect': currentLanguage === 'zh-CN' ? '断开连接' : 'disconnect'
            }}[action];
            
            const reason = prompt(currentLanguage === 'zh-CN' ? '请输入' + actionText + '原因（可选）:' : 'Enter ' + actionText + ' reason (optional):');
            if (reason === null) return; // 用户取消
            
            try {{
                const response = await fetch('/admin/' + action, {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json',
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }},
                    body: JSON.stringify({{
                        roomCode: roomCode,
                        clientId: playerId,
                        reason: reason || (currentLanguage === 'zh-CN' ? '管理员' + actionText : 'Admin ' + actionText)
                    }})
                }});
                
                const data = await response.json();
                
                if (data.success) {{
                    showSuccess(data.message || (currentLanguage === 'zh-CN' ? '操作成功' : 'Operation successful'));
                    // 刷新数据
                    setTimeout(loadData, 1000);
                }} else {{
                    showError(data.message || (currentLanguage === 'zh-CN' ? '操作失败' : 'Operation failed'));
                }}
            }} catch (error) {{
                console.error('Error:', error);
                showError(currentLanguage === 'zh-CN' ? '操作时发生错误' : 'Error performing action');
            }}
        }}
        
        // 从模态框执行操作
        function performAction(action) {{
            if (!currentPlayer) {{
                showError(currentLanguage === 'zh-CN' ? '未选择玩家' : 'No player selected');
                return;
            }}
            
            performPlayerAction(action, currentPlayer.id, currentPlayer.roomCode);
            
            // 关闭模态框
            const modal = bootstrap.Modal.getInstance(document.getElementById('playerModal'));
            if (modal) modal.hide();
        }}
        
        // 解封玩家
        async function unbanPlayer(identifier, type) {{
            if (!confirm(currentLanguage === 'zh-CN' ? '确定要解封此项目吗？' : 'Are you sure you want to unban this item?')) {{
                return;
            }}
            
            try {{
                const response = await fetch('/admin/unban', {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json',
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }},
                    body: JSON.stringify({{
                        identifier: identifier,
                        type: type
                    }})
                }});
                
                const data = await response.json();
                
                if (data.success) {{
                    showSuccess(data.message || (currentLanguage === 'zh-CN' ? '解封成功' : 'Unban successful'));
                    // 刷新封禁列表
                    setTimeout(loadBans, 1000);
                }} else {{
                    showError(data.message || (currentLanguage === 'zh-CN' ? '解封失败' : 'Unban failed'));
                }}
            }} catch (error) {{
                console.error('Error:', error);
                showError(currentLanguage === 'zh-CN' ? '解封时发生错误' : 'Error unbanning player');
            }}
        }}
        
        // 添加手动封禁
        async function addManualBan() {{
            const banType = document.getElementById('banType').value;
            const identifier = document.getElementById('banIdentifier').value.trim();
            const playerName = document.getElementById('banPlayerName').value.trim();
            const reason = document.getElementById('banReason').value.trim();
            
            if (!identifier) {{
                showError(currentLanguage === 'zh-CN' ? '请输入标识符' : 'Please enter identifier');
                return;
            }}
            
            if (!reason) {{
                showError(currentLanguage === 'zh-CN' ? '请输入封禁原因' : 'Please enter ban reason');
                return;
            }}
            
            const typeText = banType === 'IP' 
                ? (currentLanguage === 'zh-CN' ? 'IP地址' : 'IP address')
                : (currentLanguage === 'zh-CN' ? '好友代码' : 'Friend code');
            
            if (!confirm(currentLanguage === 'zh-CN' 
                ? '确定要封禁' + typeText + '：' + identifier + '吗？'
                : 'Are you sure you want to ban ' + typeText + ': ' + identifier + '?')) {{
                return;
            }}
            
            try {{
                const response = await fetch('/admin/manual-ban', {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json',
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }},
                    body: JSON.stringify({{
                        type: banType,
                        identifier: identifier,
                        playerName: playerName || (currentLanguage === 'zh-CN' ? '手动封禁' : 'Manual ban'),
                        reason: reason
                    }})
                }});
                
                const data = await response.json();
                
                if (data.success) {{
                    showSuccess(currentLanguage === 'zh-CN' ? '封禁添加成功！' : 'Ban added successfully!');
                    
                    // 清空表单
                    document.getElementById('banIdentifier').value = '';
                    document.getElementById('banPlayerName').value = '';
                    document.getElementById('banReason').value = '';
                    
                    // 刷新封禁列表
                    setTimeout(loadBans, 1000);
                }} else {{
                    showError(data.message || (currentLanguage === 'zh-CN' ? '封禁失败' : 'Ban failed'));
                }}
            }} catch (error) {{
                console.error('Error:', error);
                showError(currentLanguage === 'zh-CN' ? '添加封禁时发生错误' : 'Error adding ban');
            }}
        }}
        
        // 发送全局消息
        async function sendGlobalMessage() {{
            const message = document.getElementById('globalMessage').value.trim();
            
            if (!message) {{
                showError(currentLanguage === 'zh-CN' ? '请输入要发送的消息' : 'Please enter message to send');
                return;
            }}
            
            if (!confirm(currentLanguage === 'zh-CN' 
                ? '确定要向所有房间发送这条消息吗？'
                : 'Are you sure you want to send this message to all rooms?')) {{
                return;
            }}
            
            try {{
                const response = await fetch('/admin/global-chat', {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json',
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }},
                    body: JSON.stringify({{
                        message: message
                    }})
                }});
                
                const data = await response.json();
                
                if (data.success) {{
                    showSuccess(currentLanguage === 'zh-CN' ? '消息发送成功！' : 'Message sent successfully!');
                    document.getElementById('globalMessage').value = '';
                }} else {{
                    showError(data.message || (currentLanguage === 'zh-CN' ? '发送失败' : 'Send failed'));
                }}
            }} catch (error) {{
                console.error('Error:', error);
                showError(currentLanguage === 'zh-CN' ? '发送消息时发生错误' : 'Error sending message');
            }}
        }}
        
        // 发送房间消息
        function sendRoomMessage(roomCode) {{
            const message = prompt(currentLanguage === 'zh-CN' ? '请输入要发送到房间的消息:' : 'Enter message to send to room:');
            if (!message) return;
            
            // 这里需要实现发送房间消息的API
            // 暂时显示提示
            alert(currentLanguage === 'zh-CN' ? '消息发送功能正在开发中' : 'Message sending feature is under development');
        }}
        
        // 显示封禁列表模态框
        async function showBanModal() {{
            try {{
                const response = await fetch('/admin/api/bans', {{
                    headers: {{
                        'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                    }}
                }});
                
                if (!response.ok) throw new Error('Network response was not ok');
                
                const bans = await response.json();
                
                const banListContent = document.getElementById('ban-list-modal-content');
                
                if (bans.length === 0) {{
                    banListContent.innerHTML = `
                        <div class=""text-center p-5"">
                            <i class=""fas fa-check-circle fa-3x text-success mb-3""></i>
                            <h5>${{currentLanguage === 'zh-CN' ? '暂无封禁项目' : 'No Banned Items'}}</h5>
                            <p class=""text-muted"">${{currentLanguage === 'zh-CN' ? '当前没有封禁记录' : 'Currently no ban records'}}</p>
                        </div>
                    `;
                }} else {{
                    const banItems = bans.map(ban => `
                        <div class=""d-flex justify-content-between align-items-center p-3 border-bottom"">
                            <div style=""flex: 1;"">
                                <div class=""fw-bold"">${{ban.playerName}}</div>
                                <div class=""text-muted small"">
                                    <i class=""fas fa-${{ban.type === 'IP' ? 'network-wired' : 'address-card'}} me-1""></i>
                                    ${{ban.identifier}}
                                    <span class=""badge bg-${{ban.type === 'IP' ? 'danger' : 'info'}} ms-2"">
                                        ${{ban.type === 'IP' ? (currentLanguage === 'zh-CN' ? 'IP封禁' : 'IP Ban') : (currentLanguage === 'zh-CN' ? '好友代码' : 'Friend Code')}}
                                    </span>
                                </div>
                                <div class=""small mt-2"">
                                    <i class=""fas fa-comment me-1""></i>
                                    ${{ban.reason}}
                                </div>
                                <div class=""text-muted small mt-1"">
                                    <i class=""fas fa-clock me-1""></i>
                                    ${{ban.banTime}}
                                </div>
                            </div>
                            <div class=""ms-3"">
                                <button class=""btn btn-sm btn-success"" onclick=""unbanPlayer('${{ban.identifier}}', '${{ban.type}}')"">
                                    <i class=""fas fa-unlock me-1""></i>
                                    ${{currentLanguage === 'zh-CN' ? '解封' : 'Unban'}}
                                </button>
                            </div>
                        </div>
                    `).join('');
                    
                    banListContent.innerHTML = banItems;
                }}
                
                // 显示模态框
                const modal = new bootstrap.Modal(document.getElementById('banModal'));
                modal.show();
            }} catch (error) {{
                console.error('Failed to load ban list:', error);
                showError(currentLanguage === 'zh-CN' ? '加载封禁列表失败' : 'Failed to load ban list');
            }}
        }}
        
        // 加载封禁列表（用于封禁管理卡片）
        async function loadBanList() {{
            await loadBans();
            showSuccess(currentLanguage === 'zh-CN' ? '封禁列表已刷新' : 'Ban list refreshed');
        }}
    </script>
</body>
</html>");

        return sb.ToString();
    }

    #endregion

    #region 房间详情页面

    [HttpGet("room/{roomCode}")]
    public IActionResult GetRoomDetail(string roomCode)
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        if (!TryParseGameCode(roomCode, out var gameCode))
        {
            return InvalidRoomCodePage(roomCode);
        }

        var game = _gameManager.Find(gameCode);
        if (game == null)
        {
            return InvalidRoomCodePage(roomCode);
        }

        // 获取当前语言
        string currentLang = "zh-CN";
        if (Request.Cookies.TryGetValue("admin_language", out var langCookie))
        {
            currentLang = langCookie;
        }
        var isEnglish = currentLang == "en-US";

        // 获取当前主题
        string theme = "light";
        if (Request.Cookies.TryGetValue("admin_theme", out var themeCookie))
        {
            theme = themeCookie;
        }

        var html = GenerateRoomDetailHtml(roomCode, game, currentLang, isEnglish, theme);

        return Content(html, "text/html");
    }

    private string GenerateRoomDetailHtml(string roomCode, IGame game, string currentLang, bool isEnglish, string theme)
    {
        var roomChat = RoomChatStore.GetRoomChat(roomCode);
        var sb = new StringBuilder();

        sb.AppendLine($@"<!DOCTYPE html>
<html lang=""{currentLang}"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{(isEnglish ? "Room Management" : "房间管理")} - {roomCode}</title>
    
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css"" rel=""stylesheet"">
    <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"">
    
    <style>
        /* 深色模式变量 */
        .dark-mode {{
            --primary: #3498db;
            --primary-dark: #2980b9;
            --success: #27ae60;
            --warning: #d35400;
            --danger: #c0392b;
            --dark: #1a252f;
            --light: #34495e;
            --gray: #7f8c8d;
            --text: #ecf0f1;
            --text-light: #bdc3c7;
            --border: #4a6572;
            --shadow: 0 2px 10px rgba(0,0,0,0.3);
            --background: #2c3e50;
            --card-bg: #34495e;
            --input-bg: #2c3e50;
        }}
        
        /* 浅色模式变量 */
        .light-mode {{
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
            --background: #f5f7fa;
            --card-bg: white;
            --input-bg: white;
        }}
        
        body {{
            background-color: var(--background);
            color: var(--text);
            transition: background-color 0.3s ease, color 0.3s ease;
        }}
        
        .room-header {{
            background: linear-gradient(135deg, var(--primary), var(--primary-dark));
            color: white;
            padding: 30px 0;
            margin-bottom: 30px;
            border-radius: 0 0 20px 20px;
        }}
        
        .stat-card {{
            background: var(--card-bg);
            border-radius: 10px;
            padding: 20px;
            box-shadow: var(--shadow);
            text-align: center;
            margin-bottom: 20px;
        }}
        
        .stat-icon {{
            width: 60px;
            height: 60px;
            border-radius: 50%;
            background: linear-gradient(135deg, var(--primary), var(--primary-dark));
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 15px;
            color: white;
            font-size: 24px;
        }}
        
        .player-card {{
            background: var(--card-bg);
            border-radius: 10px;
            padding: 20px;
            margin-bottom: 20px;
            box-shadow: var(--shadow);
            border-left: 4px solid var(--primary);
        }}
        
        .chat-container {{
            height: 400px;
            overflow-y: auto;
            border: 1px solid var(--border);
            border-radius: 10px;
            padding: 15px;
            background-color: var(--card-bg);
        }}
        
        .chat-message {{
            margin-bottom: 10px;
            padding: 10px;
            border-radius: 8px;
            background-color: rgba(0,0,0,0.05);
        }}
        
        .dark-mode .chat-message {{
            background-color: rgba(255,255,255,0.05);
        }}
        
        .chat-message.admin {{
            background-color: rgba(254, 153, 0, 0.1);
            border-left: 3px solid #FE9900;
        }}
        
        .dark-mode .chat-message.admin {{
            background-color: rgba(254, 153, 0, 0.2);
        }}
        
        .message-strikethrough {{
            text-decoration: line-through;
            opacity: 0.7;
        }}
        
        .form-control, .form-select {{
            background-color: var(--input-bg);
            border-color: var(--border);
            color: var(--text);
        }}
        
        .form-control:focus, .form-select:focus {{
            background-color: var(--input-bg);
            border-color: var(--primary);
            color: var(--text);
            box-shadow: 0 0 0 0.25rem rgba(52, 152, 219, 0.25);
        }}
        
        .card {{
            background-color: var(--card-bg);
            color: var(--text);
        }}
        
        .theme-toggle {{
            position: absolute;
            right: 120px;
            top: 20px;
        }}
    </style>
</head>
<body class=""{(theme == "dark" ? "dark-mode" : "light-mode")}"">
    <!-- 房间头部 -->
    <div class=""room-header"">
        <div class=""container"">
            <div class=""d-flex justify-content-between align-items-center"">
                <div>
                    <h1>
                        <i class=""fas fa-door-open me-3""></i>
                        {(isEnglish ? "Room Management" : "房间管理")} - {roomCode}
                    </h1>
                    <p class=""mb-0"">
                        <i class=""fas fa-info-circle me-2""></i>
                        {(isEnglish ? $"Host: {game.Host?.Client.Name ?? "Unknown"}" : $"房主: {game.Host?.Client.Name ?? "未知"}")}
                    </p>
                </div>
                <div>
                    <!-- 主题切换按钮 -->
                    <div class=""theme-toggle"">
                        <button type=""button"" class=""btn btn-light btn-sm"" id=""themeToggle"">
                            <i class=""fas {(theme == "dark" ? "fa-sun" : "fa-moon")}""></i>
                        </button>
                    </div>
                    <a href=""/admin"" class=""btn btn-light"">
                        <i class=""fas fa-arrow-left me-2""></i>
                        {(isEnglish ? "Back to Dashboard" : "返回管理后台")}
                    </a>
                </div>
            </div>
        </div>
    </div>");

        sb.AppendLine($@"    
    <div class=""container"">
        <!-- 房间统计 -->
        <div class=""row mb-4"">
            <div class=""col-md-3"">
                <div class=""stat-card"">
                    <div class=""stat-icon"">
                        <i class=""fas fa-users""></i>
                    </div>
                    <h3>{game.PlayerCount}/{game.Options.MaxPlayers}</h3>
                    <p class=""text-muted"">{(isEnglish ? "Players" : "玩家数量")}</p>
                </div>
            </div>
            <div class=""col-md-3"">
                <div class=""stat-card"">
                    <div class=""stat-icon"">
                        <i class=""fas fa-map""></i>
                    </div>
                    <h3>{game.Options.Map}</h3>
                    <p class=""text-muted"">{(isEnglish ? "Map" : "地图")}</p>
                </div>
            </div>
            <div class=""col-md-3"">
                <div class=""stat-card"">
                    <div class=""stat-icon"">
                        <i class=""fas fa-mask""></i>
                    </div>
                    <h3>{game.Options.NumImpostors}</h3>
                    <p class=""text-muted"">{(isEnglish ? "Impostors" : "内鬼数量")}</p>
                </div>
            </div>
            <div class=""col-md-3"">
                <div class=""stat-card"">
                    <div class=""stat-icon"">
                        <i class=""fas fa-gamepad""></i>
                    </div>
                    <h3>{game.GameState}</h3>
                    <p class=""text-muted"">{(isEnglish ? "Status" : "状态")}</p>
                </div>
            </div>
        </div>");

        // 玩家列表
        sb.AppendLine($@"        
        <!-- 玩家列表 -->
        <div class=""row mb-4"">
            <div class=""col-12"">
                <div class=""card"">
                    <div class=""card-header"">
                        <h5 class=""mb-0"">
                            <i class=""fas fa-users me-2""></i>
                            {(isEnglish ? "Players" : "玩家列表")}
                        </h5>
                    </div>
                    <div class=""card-body"">
                        <div class=""row"" id=""players-container"">");

        foreach (var player in game.Players)
        {
            sb.AppendLine(PlayerToHtmlCard(player, roomCode, isEnglish));
        }

        sb.AppendLine($@"
                        </div>
                    </div>
                </div>
            </div>
        </div>");

        // 聊天记录
        sb.AppendLine($@"        
        <!-- 聊天记录 -->
        <div class=""row"">
            <div class=""col-12"">
                <div class=""card"">
                    <div class=""card-header d-flex justify-content-between align-items-center"">
                        <h5 class=""mb-0"">
                            <i class=""fas fa-comments me-2""></i>
                            {(isEnglish ? "Room Chat" : "房间聊天")}
                        </h5>
                        <button class=""btn btn-sm btn-primary"" onclick=""sendChatMessage()"">
                            <i class=""fas fa-paper-plane me-1""></i>
                            {(isEnglish ? "Send Message" : "发送消息")}
                        </button>
                    </div>
                    <div class=""card-body"">
                        <div class=""chat-container"" id=""chatContainer"">");

        foreach (var message in roomChat.Messages)
        {
            sb.AppendLine(MessageToHtml(message));
        }

        sb.AppendLine($@"
                        </div>
                        <div class=""input-group mt-3"">
                            <input type=""text"" class=""form-control"" id=""chatInput"" 
                                   placeholder=""{(isEnglish ? "Enter message..." : "输入消息...")}"">
                            <button class=""btn btn-primary"" type=""button"" onclick=""sendChatMessage()"">
                                <i class=""fas fa-paper-plane""></i>
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>");

        sb.AppendLine($@"    
    <script src=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js""></script>
    
    <script>
        const roomCode = '{roomCode}';
        let currentTheme = '{theme}';
        
        // 切换主题
        function toggleTheme() {{
            const newTheme = currentTheme === 'light' ? 'dark' : 'light';
            currentTheme = newTheme;
            
            // 更新页面类
            document.body.classList.remove('light-mode', 'dark-mode');
            document.body.classList.add(newTheme + '-mode');
            
            // 更新按钮图标
            const themeToggle = document.getElementById('themeToggle');
            const icon = themeToggle.querySelector('i');
            
            if (newTheme === 'dark') {{
                icon.classList.remove('fa-moon');
                icon.classList.add('fa-sun');
            }} else {{
                icon.classList.remove('fa-sun');
                icon.classList.add('fa-moon');
            }}
            
            // 保存主题到cookie
            fetch('/admin/set-theme', {{
                method: 'POST',
                headers: {{
                    'Content-Type': 'application/json',
                    'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                }},
                body: JSON.stringify({{ theme: newTheme }})
            }});
        }}
        
        // 设置主题切换按钮事件
        document.getElementById('themeToggle').addEventListener('click', toggleTheme);
        
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
                        location.reload();
                    }} else {{
                        alert(data.message || '{(isEnglish ? "Failed to send message" : "发送消息失败")}');
                    }}
                }});
            }}
        }}
        
        function performPlayerAction(action, playerId) {{
            const actionText = {{
                'kick': '{(isEnglish ? "kick" : "踢出")}',
                'ban': '{(isEnglish ? "ban" : "封禁")}',
                'serverban': '{(isEnglish ? "server ban" : "服务器封禁")}',
                'disconnect': '{(isEnglish ? "disconnect" : "断开连接")}'
            }}[action];
            
            const reason = prompt('{(isEnglish ? "Enter" : "请输入")}' + actionText + '{(isEnglish ? " reason (optional):" : "原因（可选）:")}');
            if (reason === null) return;
            
            fetch('/admin/' + action, {{
                method: 'POST',
                headers: {{
                    'Content-Type': 'application/json',
                    'Authorization': 'Basic ' + btoa('{_hostInfoConfig.AdminUser}:{_hostInfoConfig.AdminPassword}')
                }},
                body: JSON.stringify({{
                    roomCode: roomCode,
                    clientId: playerId,
                    reason: reason || '{(isEnglish ? "Admin" : "管理员")}' + actionText
                }})
            }})
            .then(response => response.json())
            .then(data => {{
                if (data.success) {{
                    alert(data.message);
                    location.reload();
                }} else {{
                    alert(data.message || '{(isEnglish ? "Failed to" : "操作失败")} ' + actionText);
                }}
            }});
        }}
        
        // 按Enter键发送消息
        document.getElementById('chatInput').addEventListener('keypress', function(e) {{
            if (e.key === 'Enter') {{
                sendChatMessage();
            }}
        }});
        
        // 滚动到聊天底部
        const chatContainer = document.getElementById('chatContainer');
        chatContainer.scrollTop = chatContainer.scrollHeight;
    </script>
</body>
</html>");

        return sb.ToString();
    }

    #endregion

    #region 玩家卡片生成

    private string PlayerToHtmlCard(IClientPlayer player, string roomCode, bool isEnglish)
    {
        string ipAddress = "未知";
        string location = "未知";

        try
        {
            var endPoint = player.Client.Connection.EndPoint;
            if (endPoint is IPEndPoint ipEndPoint)
            {
                ipAddress = ipEndPoint.Address.ToString();
                var locationService = new IpLocationService(_httpClient, _logger);
                location = locationService.GetLocationAsync(ipAddress).GetAwaiter().GetResult();
            }
        }
        catch
        {
            // 忽略错误
        }

        return $@"
        <div class=""col-md-6 col-lg-4"">
            <div class=""player-card"">
                <div class=""d-flex align-items-center mb-3"">
                    <div class=""rounded-circle p-3 me-3"" style=""background-color: rgba(52, 152, 219, 0.1);"">
                        <i class=""fas fa-user fa-2x text-primary""></i>
                    </div>
                    <div>
                        <h5 class=""mb-1"">{player.Client.Name}</h5>
                        <p class=""text-muted small mb-0"">ID: {player.Client.Id}</p>
                    </div>
                </div>
                
                <div class=""mb-3"">
                    <div class=""row g-2"">
                        <div class=""col-6"">
                            <small class=""text-muted"">{(isEnglish ? "Friend Code:" : "好友代码:")}</small>
                            <div><code>{player.Client.FriendCode ?? "N/A"}</code></div>
                        </div>
                        <div class=""col-6"">
                            <small class=""text-muted"">{(isEnglish ? "IP:" : "IP地址:")}</small>
                            <div>{ipAddress}</div>
                        </div>
                        <div class=""col-12"">
                            <small class=""text-muted"">{(isEnglish ? "Location:" : "地理位置:")}</small>
                            <div>{location}</div>
                        </div>
                        <div class=""col-6"">
                            <small class=""text-muted"">{(isEnglish ? "Platform:" : "平台:")}</small>
                            <div>{player.Client.PlatformSpecificData?.Platform.ToString() ?? "未知"}</div>
                        </div>
                        <div class=""col-6"">
                            <small class=""text-muted"">{(isEnglish ? "Version:" : "版本:")}</small>
                            <div>{player.Client.GameVersion}</div>
                        </div>
                    </div>
                </div>
                
                <div class=""d-flex flex-wrap gap-2"">
                    <button class=""btn btn-sm btn-outline-warning"" onclick=""performPlayerAction('kick', {player.Client.Id})"">
                        <i class=""fas fa-user-minus me-1""></i>
                        {(isEnglish ? "Kick" : "踢出")}
                    </button>
                    <button class=""btn btn-sm btn-outline-danger"" onclick=""performPlayerAction('ban', {player.Client.Id})"">
                        <i class=""fas fa-ban me-1""></i>
                        {(isEnglish ? "Ban" : "封禁")}
                    </button>
                    <button class=""btn btn-sm btn-outline-danger"" onclick=""performPlayerAction('serverban', {player.Client.Id})"">
                        <i class=""fas fa-gavel me-1""></i>
                        {(isEnglish ? "Server Ban" : "服务器封禁")}
                    </button>
                    <button class=""btn btn-sm btn-outline-info"" onclick=""performPlayerAction('disconnect', {player.Client.Id})"">
                        <i class=""fas fa-plug me-1""></i>
                        {(isEnglish ? "Disconnect" : "断开连接")}
                    </button>
                </div>
            </div>
        </div>";
    }

    #endregion

    #region 聊天消息HTML生成

    private string MessageToHtml(ChatMessage message)
    {
        var isAdmin = message.PlayerName == "Admin";
        var messageClass = isAdmin ? "chat-message admin" : "chat-message";
        var contentClass = message.IsStrikethrough ? "message-content message-strikethrough" : "message-content";

        return $@"
        <div class=""{messageClass}"">
            <div class=""d-flex justify-content-between align-items-center mb-2"">
                <span class=""fw-bold"">{message.PlayerName}</span>
                <span class=""text-muted small"">{message.Timestamp:HH:mm:ss}</span>
            </div>
            <div class=""{contentClass}"">{message.Content}</div>
        </div>";
    }

    #endregion

    #region 错误页面

    private IActionResult InvalidRoomCodePage(string roomCode)
    {
        var currentLang = _languageService.GetCurrentLanguage();
        var isEnglish = currentLang == "en-US";

        var html = $@"<!DOCTYPE html>
<html lang=""{currentLang}"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{(isEnglish ? "Room Not Found" : "房间不存在")}</title>
    
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css"" rel=""stylesheet"">
    <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"">
    
    <style>
        body {{
            background: linear-gradient(135deg, #74b9ff, #0984e3);
            height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }}
        
        .error-container {{
            background: white;
            border-radius: 20px;
            padding: 40px;
            text-align: center;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
            max-width: 500px;
        }}
        
        .error-icon {{
            font-size: 4em;
            color: #e74c3c;
            margin-bottom: 20px;
        }}
        
        .countdown {{
            font-size: 1.2em;
            color: #3498db;
            font-weight: bold;
            margin: 20px 0;
        }}
    </style>
</head>
<body>
    <div class=""error-container"">
        <div class=""error-icon"">
            <i class=""fas fa-exclamation-triangle""></i>
        </div>
        <h1 class=""mb-3"">{(isEnglish ? "Room Not Found" : "房间不存在")}</h1>
        <p class=""text-muted mb-4"">
            {(isEnglish ? $"Room code" : "房间代码")} <strong>{roomCode}</strong> {(isEnglish ? "is invalid or room no longer exists." : "无效或房间已不存在。")}
        </p>
        
        <div class=""countdown"" id=""countdown"">
            10{(isEnglish ? " seconds before returning to admin dashboard..." : "秒后自动返回管理后台...")}
        </div>
        
        <a href=""/admin"" class=""btn btn-primary mt-3"">
            <i class=""fas fa-arrow-left me-2""></i>
            {(isEnglish ? "Return to Admin Dashboard Now" : "立即返回管理后台")}
        </a>
    </div>
    
    <script>
        let countdown = 10;
        const countdownElement = document.getElementById('countdown');
        
        function updateCountdown() {{
            countdown--;
            countdownElement.textContent = countdown + '{(isEnglish ? " seconds before returning to admin dashboard..." : "秒后自动返回管理后台...")}';
            
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

    #endregion

    #region 玩家操作API

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

    [HttpPost("serverban")]
    public async Task<IActionResult> ServerBanPlayer([FromBody] PlayerActionRequest request)
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        if (!TryParseGameCode(request.RoomCode, out var gameCode))
        {
            return BadRequest(new { success = false, message = "房间代码无效" });
        }

        var game = _gameManager.Find(gameCode);
        if (game == null)
        {
            return NotFound(new { success = false, message = $"未找到代码为 {request.RoomCode} 的游戏" });
        }

        var player = game.GetClientPlayer(request.ClientId);
        if (player == null)
        {
            return NotFound(new { success = false, message = $"未找到玩家" });
        }

        try
        {
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
                // 忽略错误
            }

            var reason = string.IsNullOrEmpty(request.Reason) ? "服务器封禁" : request.Reason;

            // 封禁IP
            _banService.BanPlayer(
                player.Client.Name,
                null,
                ipAddress,
                reason
            );

            // 封禁FriendCode
            if (!string.IsNullOrEmpty(player.Client.FriendCode))
            {
                _banService.BanPlayer(
                    player.Client.Name,
                    player.Client.FriendCode,
                    null,
                    reason
                );
            }

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

    [HttpPost("manual-ban")]
    public IActionResult ManualBan([FromBody] ManualBanRequest request)
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        try
        {
            if (request.Type == "IP")
            {
                _banService.BanPlayer(
                    request.PlayerName,
                    null,
                    request.Identifier,
                    request.Reason
                );
            }
            else if (request.Type == "FriendCode")
            {
                _banService.BanPlayer(
                    request.PlayerName,
                    request.Identifier,
                    null,
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

    [HttpPost("unban")]
    public IActionResult UnbanPlayer([FromBody] UnbanRequest request)
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

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

    [HttpPost("chat")]
    public async Task<IActionResult> SendChat([FromBody] ChatRequest request)
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        if (!TryParseGameCode(request.RoomCode, out var gameCode))
        {
            return BadRequest(new { success = false, message = "房间代码无效" });
        }

        var game = _gameManager.Find(gameCode);
        if (game == null)
        {
            return NotFound(new { success = false, message = $"未找到代码为 {request.RoomCode} 的游戏" });
        }

        try
        {
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

    [HttpPost("global-chat")]
    public async Task<IActionResult> SendGlobalChat([FromBody] GlobalChatRequest request)
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        try
        {
            var allGames = _gameManager.Games.ToList();
            var successCount = 0;

            foreach (var game in allGames)
            {
                try
                {
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

    private async Task<IActionResult> HandlePlayerAction(PlayerActionRequest request, string actionType, Func<IClientPlayer, Task<string>> action)
    {
        var authResult = CheckAuthentication();
        if (authResult != null) return authResult;

        if (!TryParseGameCode(request.RoomCode, out var gameCode))
        {
            return BadRequest(new { success = false, message = "房间代码无效" });
        }

        var game = _gameManager.Find(gameCode);
        if (game == null)
        {
            return NotFound(new { success = false, message = $"未找到代码为 {request.RoomCode} 的游戏" });
        }

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

    #endregion

    #region 辅助方法和模型

    // 玩家信息类
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

    // 异步获取玩家信息
    private async Task<List<PlayerInfo>> GetPlayerInfoAsync(List<IGame> games)
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
                        location = await locationService.GetLocationAsync(ipAddress);
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

    // 解析游戏代码
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
        public string Type { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class UnbanRequest
    {
        public string Identifier { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
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

    #endregion
}
