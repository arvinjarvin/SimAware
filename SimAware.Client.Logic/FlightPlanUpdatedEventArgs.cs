using System;

namespace SimAware.Client.Logic
{
    public class FlightPlanUpdatedEventArgs : EventArgs
    {
        public FlightPlanUpdatedEventArgs(FlightPlanData flightPlan)
        {
            FlightPlan = flightPlan;
        }

        public FlightPlanData FlightPlan { get; }
    }
   
}
