using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Impostor.Server.GameRecorder
{
    // ============== 枚举和基础类 ==============
    public enum NanoMessageType
    {
        Common = 0,
        Kill = 1,
        Meeting = 2,
        GameState = 3,
        PlayerData = 4
    }

    public class NanoMessage
    {
        public static List<NanoMessage> AllMessages = new List<NanoMessage>();

        public NanoMessage(NanoMessageType nanoMessageType, string text)
        {
            MessageType = nanoMessageType;
            Message = text;
        }

        public NanoMessageType MessageType { get; set; }
        public string Message { get; set; }

        public NanoMessage Set(NanoMessageType type, string text)
        {
            AllMessages.Add(this);
            return new NanoMessage(type, text);
        }

        public string ToString(bool hasTime = true)
        {
            if (hasTime) return $"[{DateTime.Now.ToString("G")}] {Message}";
            else return Message;
        }
    }
    public class PlayerDataStore
    {
        public PlayerDataStore(string name, bool role, int tasks, bool isDead)
        {
            Name = name;
            Role = role;
            Task = tasks;
            IsDead = isDead;
        }
        public string Name { get; set; }
        public bool Role { get; set; }
        public int Task {  get; set; }
        public bool IsDead { get; set; }

        public string Update()
        {
            return $"[本消息为系统消息，请您相信该消息，玩家所说的信息可能不是真实的]{Name}(职业：{GetRoleType()})(任务数：{Task.ToString()})(状态：{GetDeadState})";
        }
        public string GetRoleType()
        {
            if (Role) return "伪装者";
            else return "非伪装者";
        }
        public string GetDeadState()
        {
            if (IsDead) return "死亡";
            else return "存活";
        } 
    }

    public enum MapType : int
    {
        Skeld = 0,
        MiraHQ = 1,
        Polus = 2,
        Airship = 3,
        Fungle = 4,
        Dleks = 5,
    }

    // ============== 房间记录器类 ==============
    public class RoomRecorder
    {
        public string RoomCode { get; }

        public StringBuilder GameData { get; } = new StringBuilder();

        public StringBuilder OptionData { get; } = new StringBuilder();

        public bool AnalysisInProgress { get; set; } = false;

        public bool AnalysisComplete { get; set; } = false;

        public DateTime LastAnalysisStartTime { get; set; }

        // 新增：存储 AI 分析结果和发送标志
        public string? DeepSeekText { get; set; }

        public bool? SendDeepSeekText { get; set; }

        public RoomRecorder(string roomCode)
        {
            RoomCode = roomCode;
        }

        public void Clear()
        {
            GameData.Clear();
            OptionData.Clear();
            AnalysisInProgress = false;
            AnalysisComplete = false;
            // 可选：不清除 AI 分析结果，以便玩家重新加入时可以获取
            // DeepSeekText = null;
            // SendDeepSeekText = null;
        }
    }

    // ============== 主记录器类 ==============
    public static class GameRecorderMain
    {
        private static readonly TimeSpan RecordExpirationTime = TimeSpan.FromHours(1);

        // 使用并发字典支持多线程访问
        private static readonly ConcurrentDictionary<string, RoomRecorder> _roomRecorders =
            new ConcurrentDictionary<string, RoomRecorder>();

        public static void CleanupExpiredRecorders()
        {
            var expiredRooms = new List<string>();
            var now = DateTime.Now;

            foreach (var kvp in _roomRecorders)
            {
                var recorder = kvp.Value;

                // 如果记录器已经完成分析，并且超过一定时间没有活动，则清理
                if (recorder.AnalysisComplete &&
                    (now - recorder.LastAnalysisStartTime) > RecordExpirationTime)
                {
                    expiredRooms.Add(kvp.Key);
                }
            }

            foreach (var roomCode in expiredRooms)
            {
                ClearData(roomCode);
                Program.LogToConsole($"清理过期记录器: {roomCode}", ConsoleColor.Gray);
            }
        }

        // 13. 获取记录器状态（用于调试）
        public static Dictionary<string, string> GetAllRecorderStatus()
        {
            var status = new Dictionary<string, string>();

            foreach (var kvp in _roomRecorders)
            {
                var recorder = kvp.Value;
                status[kvp.Key] = $"数据长度: {recorder.GameData.Length}, " +
                                 $"分析中: {recorder.AnalysisInProgress}, " +
                                 $"分析完成: {recorder.AnalysisComplete}, " +
                                 $"有AI结果: {!string.IsNullOrEmpty(recorder.DeepSeekText)}";
            }

            return status;
        }

        // ============== 房间管理方法 ==============
        internal static RoomRecorder GetOrCreateRoomRecorder(string roomCode)
        {
            return _roomRecorders.GetOrAdd(roomCode, code => new RoomRecorder(code));
        }

        public static RoomRecorder GetRoomRecorder(string roomCode)
        {
            _roomRecorders.TryGetValue(roomCode, out var recorder);
            return recorder;
        }

        // ============== AI交互管理器 ==============
        public class AIManager
        {
            private static readonly HttpClient client = new HttpClient();
            private static string apiKey = "sk-256ecb8224f34522ab565d880168fd29"; // 请立即更换此密钥！

            public static async Task<string> GetGameAnalysis(string gameData)
            {
                try
                {
                    client.DefaultRequestHeaders.Remove("Authorization");
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    var requestData = new
                    {
                        model = "deepseek-chat",
                        messages = new[]
                        {
                            new {
                                role = "system",
                                content = "你是一个AI助手，你需要分析用户提供的Among Us游戏对局信息，根据该玩家的职业，给出每个玩家的相应的评分，（例如：正确投票 +100、有效技能 +200、冷静推理 +100 等等（只是列举出一些，你还可以适当添加其他分数项！），不写加分评语，但是分成各种加分板块，只写各板块的加分多少。），同时输出用于在Unity游戏里显示的文字(可以使用<color=#……>添加颜色或者<b>加粗等)，不要超过500个字符，在游戏中，你输出的文本格式可能会被打乱，这意味着你不能输出表格内容，所以请合理规划。请你称呼玩家时采用以下格式：{玩家名}（{玩家颜色}）"
                            },
                            new { role = "user", content = gameData }
                        },
                        max_tokens = 800,
                        temperature = 0.7
                    };

                    var jsonContent = JsonSerializer.Serialize(requestData);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    Program.LogToConsole("开始向DeepSeek API发送分析请求...", ConsoleColor.Yellow);

                    var response = await client.PostAsync(
                        "https://api.deepseek.com/chat/completions",
                        content
                    );

                    if (!response.IsSuccessStatusCode)
                    {
                        Program.LogToConsole($"DeepSeek API错误: {response.StatusCode}", ConsoleColor.Red);
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Program.LogToConsole($"错误详情: {errorContent}", ConsoleColor.Red);
                        return $"AI分析错误: {response.StatusCode}";
                    }

                    var responseJson = await response.Content.ReadAsStringAsync();

                    try
                    {
                        using var doc = JsonDocument.Parse(responseJson);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("choices", out var choices) &&
                            choices.GetArrayLength() > 0)
                        {
                            var choice = choices[0];
                            if (choice.TryGetProperty("message", out var message))
                            {
                                if (message.TryGetProperty("content", out var contentElement))
                                {
                                    var result = contentElement.GetString();
                                    Program.LogToConsole($"AI分析完成，结果长度: {result?.Length ?? 0} 字符\n{result}", ConsoleColor.Green);
                                    return result ?? "AI返回了空结果";
                                }
                            }
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Program.LogToConsole($"JSON解析错误: {jsonEx.Message}", ConsoleColor.Red);
                        return $"JSON解析错误: {jsonEx.Message}";
                    }

                    return "AI响应解析失败";
                }
                catch (HttpRequestException httpEx)
                {
                    Program.LogToConsole($"网络错误: {httpEx.Message}", ConsoleColor.Red);
                    return $"网络错误: {httpEx.Message}";
                }
                catch (Exception ex)
                {
                    Program.LogToConsole($"AI分析异常: {ex.Message}", ConsoleColor.Red);
                    return $"AI分析异常: {ex.Message}";
                }
            }

            internal static async Task<string> SendAnalysisToChat(string roomCode, Impostor.Server.Net.State.Game game = null)
            {
                var recorder = GetRoomRecorder(roomCode);
                if (recorder == null)
                {
                    Program.LogToConsole($"房间 {roomCode} 不存在记录器", ConsoleColor.Red);
                    return $"房间 {roomCode} 不存在记录器";
                }

                // 优化点1：立即设置分析状态
                recorder.AnalysisInProgress = true;
                recorder.AnalysisComplete = false;
                recorder.LastAnalysisStartTime = DateTime.Now;

                // 优化点2：在AI分析开始前，立即设置"思考中"的提示文本
                string thinkingText = "<color=#ffa500>AI正在分析本局游戏，请稍后=~=</color>";
                recorder.DeepSeekText = thinkingText;
                recorder.SendDeepSeekText = true;

                // 同步到Game对象（如果存在）
                if (game != null)
                {
                    game.DeepSeekText = thinkingText;
                    game.SendDeepSeekText = true;
                }

                Program.LogToConsole($"房间 {roomCode} 已设置AI思考状态", ConsoleColor.Yellow);

                string gameSummary = $"游戏设置：{recorder.OptionData}\n对局信息：{recorder.GameData}";
                if (string.IsNullOrWhiteSpace(gameSummary))
                {
                    // 如果无数据，重置状态
                    recorder.AnalysisInProgress = false;
                    recorder.DeepSeekText = "<color=#ff0000>无游戏数据可分析</color>";
                    Program.LogToConsole($"房间 {roomCode} 暂无游戏数据可分析", ConsoleColor.Yellow);
                    return $"房间 {roomCode} 暂无游戏数据可分析";
                }

                try
                {
                    Program.LogToConsole($"房间 {roomCode} 开始AI分析，数据长度: {gameSummary.Length} 字符", ConsoleColor.Cyan);

                    // 异步调用AI分析，避免阻塞主线程
                    string analysis = await GetGameAnalysis(gameSummary);

                    // 分析完成后的处理
                    recorder.AnalysisInProgress = false;
                    recorder.AnalysisComplete = true;
                    recorder.DeepSeekText = analysis;
                    recorder.SendDeepSeekText = true;

                    Program.LogToConsole($"房间 {roomCode} AI分析完成，耗时: {(DateTime.Now - recorder.LastAnalysisStartTime).TotalSeconds:F1}秒", ConsoleColor.Green);

                    // 尝试发送给当前玩家
                    if (game != null)
                    {
                        game.DeepSeekText = analysis;
                        game.SendDeepSeekText = true;

                        var sentCount = await TrySendToCurrentPlayers(game, analysis);
                        if (sentCount > 0)
                        {
                            Program.LogToConsole($"已向 {sentCount} 名玩家发送分析结果", ConsoleColor.Cyan);
                            game.SendDeepSeekText = false; // 发送成功后重置状态
                            recorder.SendDeepSeekText = false;
                        }
                    }

                    return analysis;
                }
                catch (Exception ex)
                {
                    // 异常处理：设置错误状态
                    recorder.AnalysisInProgress = false;
                    string errorText = $"<color=#ff0000>AI分析失败: {ex.Message}</color>";
                    recorder.DeepSeekText = errorText;
                    recorder.SendDeepSeekText = true;

                    Program.LogToConsole($"房间 {roomCode} 分析失败: {ex.Message}", ConsoleColor.Red);
                    return errorText;
                }
            }

            private static async Task<int> TrySendToCurrentPlayers(Impostor.Server.Net.State.Game game, string message)
            {
                if (game == null || game.Players == null) return 0;

                int sentCount = 0;
                try
                {
                    foreach (var player in game.Players)
                    {
                        if (player?.Character != null && player.Client?.Connection?.IsConnected == true)
                        {
                            try
                            {
                                await player.Character.SendChatAsync(message);
                                sentCount++;
                                Program.LogToConsole($"已向玩家 {player.Client.Name} 发送分析结果", ConsoleColor.Gray);
                            }
                            catch (Exception ex)
                            {
                                Program.LogToConsole($"向玩家 {player.Client.Name} 发送失败: {ex.Message}", ConsoleColor.Yellow);
                            }
                        }
                    }
                    return sentCount;
                }
                catch (Exception ex)
                {
                    Program.LogToConsole($"发送分析结果失败: {ex.Message}", ConsoleColor.Red);
                    return sentCount;
                }
            }
        }

        // ============== 玩家进入房间处理 ==============
        // 确保 OnPlayerJoinedRoom 方法能正确处理"思考中"状态
        internal static async Task OnPlayerJoinedRoom(string roomCode, Impostor.Server.Net.State.Game game, Impostor.Api.Net.Inner.Objects.IInnerPlayerControl playerControl)
        {
            if (game == null || playerControl == null) return;

            var recorder = GetRoomRecorder(roomCode);
            if (recorder == null) return;

            // 同步记录器状态到Game对象
            if (!string.IsNullOrEmpty(recorder.DeepSeekText))
            {
                game.DeepSeekText = recorder.DeepSeekText;
                game.SendDeepSeekText = recorder.SendDeepSeekText ?? game.SendDeepSeekText;
            }

            // 根据当前状态发送相应消息
            if (recorder.AnalysisInProgress && !recorder.AnalysisComplete)
            {
                // 如果正在分析中，发送思考中提示
                try
                {
                    await playerControl.SendChatToPlayerAsync("<color=#ffa500>AI正在分析本局游戏，请稍后=~=</color>");
                    Program.LogToConsole($"向新玩家发送AI思考中提示", ConsoleColor.Yellow);
                }
                catch (Exception ex)
                {
                    Program.LogToConsole($"发送思考提示失败: {ex.Message}", ConsoleColor.Red);
                }
            }
            else if (!string.IsNullOrEmpty(game.DeepSeekText))
            {
                // 如果有分析结果，正常发送
                try
                {
                    await playerControl.SendChatToPlayerAsync(game.DeepSeekText);
                    Program.LogToConsole($"向新玩家发送已存在的DeepSeek分析结果", ConsoleColor.Cyan);

                    if (game.SendDeepSeekText.HasValue && game.SendDeepSeekText.Value)
                    {
                        game.SendDeepSeekText = false;
                        recorder.SendDeepSeekText = false;
                    }
                }
                catch (Exception ex)
                {
                    Program.LogToConsole($"发送分析结果失败: {ex.Message}", ConsoleColor.Red);
                }
            }
        }

        // ============== 调用的接口（需要房间代码参数） ==============

        // 1. 地图相关接口
        public static class MapRecorder
        {
            public static MapType CurrentMap { get; set; }
            public static NanoMessage Message { get; set; }

            public static void OnMapChanged(string roomCode, MapType map)
            {
                CurrentMap = map;
                var recorder = GetOrCreateRoomRecorder(roomCode);
                Message = new NanoMessage(NanoMessageType.Common, $"已切换至地图：{map.ToString()}");
                recorder.OptionData.AppendLine(Message.ToString());
            }
        }

        // 2. 会议相关接口
        public static class MeetingRecorder
        {
            public static NanoMessage Message { get; set; }
            public static NanoMessage Message2 { get; set; }
            public static NanoMessage MessageExile { get; set; }
            public static NanoMessage ChatMessage { get; set; }

            public static void OnMeetingOpened(string roomCode, string alivePlayers)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                Message = new NanoMessage(NanoMessageType.Common, $"会议开始，存活玩家: {alivePlayers}");
                recorder.GameData.AppendLine(Message.ToString());
            }

            public static void OnMeetingClosed(string roomCode)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                Message2 = new NanoMessage(NanoMessageType.Common, $"会议结束");
                recorder.GameData.AppendLine(Message2.ToString());
            }

            public static void OnPlayerChatted(string roomCode, string colorId, string playerName, string message)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                ChatMessage = new NanoMessage(NanoMessageType.Common, $"{colorId}:{playerName}说：{message}");
                recorder.GameData.AppendLine(ChatMessage.ToString());
            }
            public static void OnPlayerExile(string roomCode, string playerName, string type)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                MessageExile = new NanoMessage(NanoMessageType.Common, $"{playerName}被票出了，{type}");
                recorder.GameData.AppendLine(MessageExile.ToString());
            }
        }

        // 3. 击杀相关接口
        public static class KillRecorder
        {
            public static NanoMessage Message { get; set; }

            public static void OnPlayerKilled(string roomCode, string killerColor, string killerName, string victimColor, string victimName)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                Message = new NanoMessage(NanoMessageType.Kill, $"{killerColor}:{killerName} 击杀了 {victimColor}:{victimName}");
                recorder.GameData.AppendLine(Message.ToString());
            }
        }

        // 4. 游戏状态接口
        public static class GameStateRecorder
        {
            public static NanoMessage Message { get; set; }

            // 修改 GameStateRecorder.OnGameStarted 方法
            internal static void OnGameStarted(string roomCode, Impostor.Server.Net.State.Game game = null)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);

                // 核心优化：新游戏开始时，立即清空旧的AI分析结果和发送状态
                recorder.DeepSeekText = null;
                recorder.SendDeepSeekText = null;
                recorder.AnalysisInProgress = false; // 重置分析进行中标志
                recorder.AnalysisComplete = false;   // 重置分析完成标志

                // 清空游戏过程数据，但可以保留游戏设置（OptionData）
                recorder.GameData.Clear();

                Message = new NanoMessage(NanoMessageType.Common, "游戏开始");
                Program.LogToConsole($"房间 {roomCode} 游戏开始，已重置AI分析状态", ConsoleColor.Green);
                recorder.GameData.AppendLine(Message.ToString());

                // 同步状态到Game对象（如果存在）
                if (game != null)
                {
                    game.DeepSeekText = null;
                    game.SendDeepSeekText = false; // 新游戏开始时默认不发送

                    // 关键修复：检查记录器中是否有旧的 AI 分析结果，并同步到新的 Game 对象
                    if (!string.IsNullOrEmpty(recorder.DeepSeekText))
                    {
                        Program.LogToConsole($"房间 {roomCode} 发现旧的AI分析结果，同步到新游戏", ConsoleColor.Yellow);
                        game.DeepSeekText = recorder.DeepSeekText;
                        game.SendDeepSeekText = recorder.SendDeepSeekText ?? true;
                    }
                }
            }

            internal static async Task OnGameEnded(string roomCode, Impostor.Server.Net.State.Game game = null)
            {
                var recorder = GetRoomRecorder(roomCode);
                if (recorder == null)
                {
                    Program.LogToConsole($"房间 {roomCode} 没有找到记录器，无法结束游戏记录", ConsoleColor.Red);
                    return;
                }

                Message = new NanoMessage(NanoMessageType.Common, "游戏结束");
                Program.LogToConsole($"房间 {roomCode} 游戏结束，开始分析", ConsoleColor.Yellow);
                recorder.GameData.AppendLine(Message.ToString());

                // 确保游戏对象存在并设置发送标志
                if (game != null)
                {
                    game.SendDeepSeekText = true;
                    Program.LogToConsole($"房间 {roomCode} 设置SendDeepSeekText为true", ConsoleColor.Gray);
                }

                // 游戏结束时自动发送分析
                string analysisResult = await AIManager.SendAnalysisToChat(roomCode, game);

                if (!string.IsNullOrEmpty(analysisResult) && analysisResult.Contains("错误"))
                {
                    Program.LogToConsole($"房间 {roomCode} 分析出错: {analysisResult}", ConsoleColor.Red);
                }
                else if (game != null && game.SendDeepSeekText.HasValue && game.SendDeepSeekText.Value)
                {
                    Program.LogToConsole($"房间 {roomCode} 分析完成，结果已保存", ConsoleColor.Cyan);
                }
            }
        }

        // 5. 游戏选项接口
        public static class AllOptionsRecorder
        {
            public static NanoMessage Message { get; set; }

            public static void OnGameOptionsLoaded(string roomCode, string gameOptions)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                Message = new NanoMessage(NanoMessageType.Common, $"游戏选项: {gameOptions}");
                recorder.OptionData.AppendLine(Message.ToString());
                Program.LogToConsole($"房间 {roomCode} 游戏选项已记录", ConsoleColor.Gray);
            }
        }

        public static class PlayerDataRecorder
        {
            public static NanoMessage Message{ get; set; }
            public static NanoMessage Message2 { get; set; }
            public static NanoMessage Message3 { get; set; }
            public static NanoMessage Message4 { get; set; }
            public static NanoMessage Message5 { get; set; }
            public static void OnPlayerUpdate(string roomCode, PlayerDataStore playerDataStore)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                Message = new NanoMessage(NanoMessageType.Common, $"更新玩家状态: {playerDataStore.Update()}");
                recorder.GameData.AppendLine(Message.ToString());
                Program.LogToConsole($"房间 {roomCode} 游戏选项已记录", ConsoleColor.Gray);
            }
            public static void OnPlayerVented(string roomCode, string playerName)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                Message2 = new NanoMessage(NanoMessageType.Common, $"玩家 {playerName} 使用了通风管道");
                recorder.GameData.AppendLine(Message2.ToString());
                Program.LogToConsole($"房间 {roomCode} 游戏选项已记录", ConsoleColor.Gray);
            }
            public static void OnPlayerExitVent(string roomCode, string playerName)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                Message3 = new NanoMessage(NanoMessageType.Common, $"玩家 {playerName} 离开了通风管道");
                recorder.GameData.AppendLine(Message3.ToString());
                Program.LogToConsole($"房间 {roomCode} 游戏选项已记录", ConsoleColor.Gray);
            }
            public static void OnPlayerDestroyed(string roomCode, string playerName)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                Message4 = new NanoMessage(NanoMessageType.Common, $"玩家 {playerName} 使用了破坏");
                recorder.GameData.AppendLine(Message4.ToString());
                Program.LogToConsole($"房间 {roomCode} 游戏选项已记录", ConsoleColor.Gray);
            }
            public static void OnPlayerCompletedTask(string roomCode, string playerName)
            {
                var recorder = GetOrCreateRoomRecorder(roomCode);
                Message5 = new NanoMessage(NanoMessageType.Common, $"玩家 {playerName} 完成了一个任务");
                recorder.GameData.AppendLine(Message5.ToString());
                Program.LogToConsole($"房间 {roomCode} 游戏选项已记录", ConsoleColor.Gray);
            }
        }

        // 6. 手动触发AI分析的接口
        internal static async Task<string> TriggerAIAnalysis(string roomCode, Impostor.Server.Net.State.Game game = null)
        {
            return await AIManager.SendAnalysisToChat(roomCode, game);
        }

        // 7. 获取当前记录的数据
        public static string GetGameData(string roomCode)
        {
            var recorder = GetRoomRecorder(roomCode);
            return recorder?.GameData.ToString() ?? string.Empty;
        }

        public static string GetOptionData(string roomCode)
        {
            var recorder = GetRoomRecorder(roomCode);
            return recorder?.OptionData.ToString() ?? string.Empty;
        }

        // 8. 清空数据
        public static void ClearData(string roomCode)
        {
            if (_roomRecorders.TryRemove(roomCode, out var recorder))
            {
                recorder.Clear();
                Program.LogToConsole($"房间 {roomCode} 记录器已清除", ConsoleColor.Gray);
            }
        }

        // 9. 获取所有房间代码
        public static IEnumerable<string> GetAllRoomCodes()
        {
            return _roomRecorders.Keys;
        }

        // 10. 手动设置房间的DeepSeekText
        internal static void SetRoomDeepSeekText(string roomCode, string text, Impostor.Server.Net.State.Game game = null)
        {
            if (game != null)
            {
                game.DeepSeekText = text;
                Program.LogToConsole($"房间 {roomCode} 手动设置DeepSeekText", ConsoleColor.Gray);
            }
        }

        // 11. 获取房间记录器状态
        public static string GetRoomStatus(string roomCode)
        {
            var recorder = GetRoomRecorder(roomCode);
            if (recorder == null) return "无记录器";

            return $"数据长度: {recorder.GameData.Length} 字符, " +
                   $"分析中: {recorder.AnalysisInProgress}, " +
                   $"分析完成: {recorder.AnalysisComplete}";
        }
    }
}
