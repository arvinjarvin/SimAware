using System;

namespace SimAware.Client.Logic
{
    public class ConnectorErrorEventArgs : EventArgs
    {

        public ConnectorErrorEventArgs(string simConnectError)
        {
            SimConnectError = simConnectError;
        }

        public string SimConnectError { get; set; }

    }
}
