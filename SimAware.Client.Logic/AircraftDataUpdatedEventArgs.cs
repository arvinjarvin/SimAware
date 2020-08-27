using System;

namespace SimAware.Client.Logic
{
    public class AircraftDataUpdatedEventArgs : EventArgs
    {

        public AircraftDataUpdatedEventArgs(AircraftData aircraftData)
        {
            AircraftData = aircraftData;
        }
        public AircraftData AircraftData { get;  }
    }
}
