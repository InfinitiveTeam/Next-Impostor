using System.Linq;
using System.Reflection;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Games;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Meeting
{
    public class MeetingStartedEvent : IMeetingStartedEvent
    {
        public MeetingStartedEvent(IGame game, IInnerMeetingHud meetingHud)
        {
            Game = game;
            MeetingHud = meetingHud;

            var players = Game.Players.ToArray().Where(p => Game.GetClientPlayer(p.Client.Id)?.Character?.PlayerInfo?.IsDead != true);
            GameRecorderMain.MeetingRecorder.OnMeetingOpened(Game.Code, $"会议已开启，当前存活玩家列表：【{string.Join(", ", players)}】");
        }

        public IGame Game { get; }

        public IInnerMeetingHud MeetingHud { get; }
    }
}
