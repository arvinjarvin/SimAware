using System;

namespace SimAware.Client.Logic
{
    public class AircraftStatusUpdatedEventArgs : EventArgs
    {

        public AircraftStatusUpdatedEventArgs(AircraftStatus aircraftStatus)
        {
            AircraftStatus = aircraftStatus;
        }

        public AircraftStatus AircraftStatus { get; }
    }
}
