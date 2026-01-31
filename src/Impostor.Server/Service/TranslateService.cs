using Impostor.Server.Net.State;

namespace Impostor.Server.Service
{
    public class TranslateService
    {
        internal static string GetTranslateString(Api.Innersloth.Language language, string translateText)
        {
            switch (language)
            {
                case Api.Innersloth.Language.SChinese:
                    return GetSChinese(translateText);
                default:
                    return translateText;
            }
        }

        private static string GetSChinese(string englishText)
        {
            // 根据英文文本返回对应的中文翻译
            switch (englishText)
            {
                case "Note successful":
                    return "备注成功！";
                case "An internal server error occurred, please contact an administrator/developer":
                    return "发生内部服务器错误，请联系管理/开发人员";
                case "Please update your game to join this lobby":
                    return "请更新游戏以加入该大厅";
                case "Your game version is too new for this lobby. If you want to join this lobby, please downgrade your client version":
                    return "您的游戏版本对此大厅来说过新，如需加入该大厅，请降级您的客户端版本";
                case "The game you tried to join is being destroyed. Please create a new game":
                    return "您尝试加入的游戏正在被销毁，请创建新游戏";
                case "Username is too long, please shorten it":
                    return "用户名过长，请缩短用户名";
                case "Username contains illegal characters, please remove them":
                    return "用户名包含非法字符，请移除这些字符";
                case "Please update your game to connect to this server":
                    return "请更新游戏以连接此服务器";
                case "Error: Unsupported version [1]":
                    return "错误：不支持的版本 [1]";
                case "Error: Unsupported version [2]":
                    return "错误：不支持的版本 [2]";
                case "Sorry, UDP matchmaking is no longer supported. Please refer to <link=https://github.com/Impostor/Impostor/blob/master/docs/Upgrading.md#impostor-190>Impostor documentation</link> for how to migrate to HTTP matchmaking":
                    return "很抱歉，UDP匹配已不再受支持。请参阅<link=https://github.com/Impostor/Impostor/blob/master/docs/Upgrading.md#impostor-190>Impostor文档</link>了解如何迁移至HTTP匹配";
                case "Your client requested host authority [+25 protocol], but this NImpostor server does not have this feature enabled.":
                    return "您的客户端请求主机权限[+25协议]，但此NImpostor服务器未启用该功能。";
                case "Client is in an invalid state.":
                    return "客户端处于无效状态。";
                case "Invalid limbo state while joining.":
                    return "加入时处于无效的等待状态。";
                case "Unknown error.":
                    return "未知错误。";
                case "Game not found.":
                    return "游戏未找到。";
                case "Game is full.":
                    return "游戏已满。";
                case "Game has already started.":
                    return "游戏已开始。";
                case "You are banned from this game.":
                    return "您已被禁止加入此游戏。";
                case "You have been caught cheating and were kicked from the lobby. For questions, contact your server admin and share the following code: {0}.":
                    return "您因作弊被检测到并被踢出大厅。如有疑问，请联系服务器管理员并提供以下代码：{0}。";
                case "You have been caught cheating and were banned from the lobby. For questions, contact your server admin and share the following code: {0}.":
                    return "您因作弊被检测到并被禁止加入大厅。如有疑问，请联系服务器管理员并提供以下代码：{0}。";
                default:
                    return englishText; // 如果没有找到对应的翻译，返回原文本
            }
        }
    }
}
