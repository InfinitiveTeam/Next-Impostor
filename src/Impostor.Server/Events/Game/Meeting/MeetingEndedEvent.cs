using Impostor.Api.Events.Meeting;
using Impostor.Api.Games;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Meeting
{
    public class MeetingEndedEvent : IMeetingEndedEvent
    {
        public MeetingEndedEvent(IGame game, IInnerMeetingHud meetingHud, IInnerPlayerControl? exiled, bool isTie)
        {
            Game = game;
            MeetingHud = meetingHud;
            Exiled = exiled;
            IsTie = isTie;

            GameRecorderMain.MeetingRecorder.OnMeetingClosed();
        }

        public IGame Game { get; }

        public IInnerMeetingHud MeetingHud { get; }

        public IInnerPlayerControl? Exiled { get; }

        public bool IsTie { get; }
    }
}
