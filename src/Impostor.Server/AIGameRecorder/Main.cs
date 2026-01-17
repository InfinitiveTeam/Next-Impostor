using System;
using System.Collections.Generic;
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

    public enum MapType : int
    {
        Skeld = 0,
        MiraHQ = 1,
        Polus = 2,
        Airship = 3,
        Fungle = 4,
        Dleks = 5,
    }

    // ============== 主记录器类 ==============
    public static class GameRecorderMain
    {
        public static StringBuilder GameData = new StringBuilder();
        public static StringBuilder OptionData = new StringBuilder();

        // ============== 工具方法 ==============
        public static int TranslateColorName(string colorName)
        {
            colorName = colorName.ToUpper().Replace("(", "").Replace(")", "");
            string[] COLOR_NAMES = {
                "红色", "蓝色", "绿色", "粉色",
                "橙色", "黄色", "黑色", "白色",
                "紫色", "棕色", "青色", "浅绿色",
                "玫红色", "浅粉色", "焦黄色", "灰色",
                "茶色", "珊瑚色"};

            return Array.IndexOf(COLOR_NAMES, colorName.ToUpper());
        }

        // ============== AI交互管理器 ==============
        public class AIManager
        {
            private static readonly HttpClient client = new HttpClient();
            private static string apiKey = "Deep Seek Api Key";

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
                            new { role = "system", content = "你是一个AI助手，你需要分析用户提供的Among Us游戏对局信息，给出每个玩家的相应的评分" },
                            new { role = "user", content = gameData }
                        },
                        max_tokens = 2048,
                        temperature = 0.7
                    };

                    var jsonContent = JsonSerializer.Serialize(requestData);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(
                        "https://api.deepseek.com/chat/completions",
                        content
                    );

                    if (!response.IsSuccessStatusCode)
                    {
                        return $"错误: {response.StatusCode}";
                    }

                    var responseJson = await response.Content.ReadAsStringAsync();
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
                                return contentElement.GetString();
                            }
                        }
                    }

                    return "AI响应解析失败";
                }
                catch (Exception ex)
                {
                    return $"AI分析失败: {ex.Message}";
                }
            }

            public static async void SendAnalysisToChat()
            {
                string gameSummary = GameData.ToString();
                if (string.IsNullOrEmpty(gameSummary))
                {
                    Program.LogToConsole("暂无游戏数据可分析", ConsoleColor.Gray);
                    return;
                }

                Program.LogToConsole("正在分析游戏数据...", ConsoleColor.Gray);

                try
                {
                    string analysis = await GetGameAnalysis(gameSummary);
                    Program.LogToConsole(analysis, ConsoleColor.Cyan);
                }
                catch (Exception ex)
                {
                    Program.LogToConsole($"分析失败: {ex.Message}", ConsoleColor.Red);
                }
            }
        }

        // ============== 调用的接口 ==============
        // 这些方法可以在对应的游戏事件发生时调用

        // 1. 地图相关接口
        public static class MapRecorder
        {
            public static MapType CurrentMap { get; set; }
            public static NanoMessage Message { get; set; }

            public static void OnMapChanged(MapType map)
            {
                CurrentMap = map;
                Message = new NanoMessage(NanoMessageType.Common, $"已切换至地图：{map.ToString()}");
                OptionData.AppendLine(Message.ToString());
            }
        }

        // 2. 会议相关接口
        public static class MeetingRecorder
        {
            public static NanoMessage Message { get; set; }

            public static NanoMessage Message2 { get; set; }

            public static NanoMessage ChatMessage { get; set; }

            public static void OnMeetingOpened(string alivePlayers)
            {
                Message = new NanoMessage(NanoMessageType.Common, alivePlayers);
                GameData.AppendLine(Message.ToString());
            }

            public static void OnMeetingClosed()
            {
                Message2 = new NanoMessage(NanoMessageType.Common, $"会议结束");
                GameData.AppendLine(Message2.ToString());
            }

            public static void OnPlayerChatted(string colorId, string playerName, string message)
            {
                ChatMessage = new NanoMessage(NanoMessageType.Common, $"{colorId}:{playerName}说：{message}");
                GameData.AppendLine(ChatMessage.ToString());
            }
        }

        // 3. 击杀相关接口
        public static class KillRecorder
        {
            public static NanoMessage Message { get; set; }

            public static void OnPlayerKilled(string killerColor, string killerName, string victimColor, string victimName)
            {
                Message = new NanoMessage(NanoMessageType.Kill, $"{killerColor}:{killerName} 击杀了 {victimColor}:{victimName}");
                GameData.AppendLine(Message.ToString());
            }
        }

        // 4. 游戏状态接口
        public static class GameStateRecorder
        {
            public static NanoMessage Message { get; set; }

            public static void OnGameStarted()
            {
                Message = new NanoMessage(NanoMessageType.Common, "游戏开始");
                GameData.AppendLine(Message.ToString());
            }

            public static void OnGameEnded()
            {
                Message = new NanoMessage(NanoMessageType.Common, "游戏结束");
                GameData.AppendLine(Message.ToString());

                // 游戏结束时自动发送分析
                AIManager.SendAnalysisToChat();
            }
        }

        // 5. 游戏选项接口
        public static class AllOptionsRecorder
        {
            public static NanoMessage Message { get; set; }

            public static void OnGameOptionsLoaded(string gameOptions)
            {
                Message = new NanoMessage(NanoMessageType.Common, $"当前游戏选项：{gameOptions}");
                OptionData.AppendLine(Message.ToString());
            }
        }

        // 6. 手动触发AI分析的接口
        public static void TriggerAIAnalysis()
        {
            AIManager.SendAnalysisToChat();
        }

        // 7. 获取当前记录的数据
        public static string GetGameData() => GameData.ToString();
        public static string GetOptionData() => OptionData.ToString();

        // 8. 清空数据
        public static void ClearData()
        {
            GameData.Clear();
            OptionData.Clear();
            NanoMessage.AllMessages.Clear();
        }
    }
}
