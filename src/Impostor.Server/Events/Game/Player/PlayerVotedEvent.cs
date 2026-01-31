using System;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Player
{
    public class PlayerVotedEvent : IPlayerVotedEvent
    {
        public PlayerVotedEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, VoteType voteType, IInnerPlayerControl? votedFor)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            VoteType = voteType;
            VotedFor = votedFor;

            // 记录玩家投票事件 - 添加空引用检查
            try
            {
                string playerName = clientPlayer?.Client?.Name ?? "未知玩家";
                string votedForName = votedFor?.PlayerInfo?.PlayerName ?? "跳过";
                string voteTypeName = voteType.ToString();

                GameRecorderMain.PlayerRecorder.OnPlayerVoted(
                    game.Code.ToString(),
                    playerName,
                    votedForName,
                    voteTypeName);
            }
            catch (Exception ex)
            {
                Program.LogToConsole($"记录投票事件失败: {ex.Message}", ConsoleColor.Yellow);
            }
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IInnerPlayerControl? VotedFor { get; }

        public VoteType VoteType { get; }
    }
}
