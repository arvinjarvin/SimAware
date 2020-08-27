using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimAware.Client.Logic
{
    public interface IFlightConnector
    {
        event EventHandler<AircraftDataUpdatedEventArgs> AircraftDataUpdated;
        event EventHandler<AircraftStatusUpdatedEventArgs> AircraftStatusUpdated;
        event EventHandler AircraftPositionChanged;
        event EventHandler<FlightPlanUpdatedEventArgs> FlightPlanUpdated;
        event EventHandler Connected;
        event EventHandler Closed;
        event EventHandler<ConnectorErrorEventArgs> Error;

        Task<AircraftData> RequestAircraftDataAsync(CancellationToken cancellationtoken = default);
        Task<FlightPlanData> RequestFlightPlanAsync(CancellationToken cancellationtoken = default);


    }
}
