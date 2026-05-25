namespace F1SimHubLive.F1Signalr
{
    internal static class TopicNames
    {
        public const string CarData = "CarData.z";
        public const string Position = "Position.z";
        public const string TimingData = "TimingData";
        public const string TimingAppData = "TimingAppData";
        public const string DriverList = "DriverList";
        public const string SessionInfo = "SessionInfo";
        public const string SessionStatus = "SessionStatus";
        public const string TrackStatus = "TrackStatus";
        public const string Heartbeat = "Heartbeat";

        public static readonly string[] AllSubscribed =
        {
            CarData,
            TimingData,
            TimingAppData,
            DriverList,
            SessionInfo,
            SessionStatus,
            TrackStatus,
            Heartbeat,
        };
    }
}
