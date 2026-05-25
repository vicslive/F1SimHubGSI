namespace F1SimHubLive.Telemetry
{
    internal sealed class TelemetryBuffer
    {
        private DriverSnapshot? _prev;
        private DriverSnapshot? _curr;
        private readonly object _lock = new();

        public void Push(DriverSnapshot snapshot)
        {
            lock (_lock)
            {
                _prev = _curr;
                _curr = snapshot;
            }
        }

        public (DriverSnapshot? prev, DriverSnapshot? curr) Snapshot()
        {
            lock (_lock)
            {
                return (_prev, _curr);
            }
        }
    }
}
