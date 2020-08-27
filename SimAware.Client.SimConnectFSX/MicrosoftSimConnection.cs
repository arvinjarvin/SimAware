using SimAware.Client.Logic;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.FlightSimulator.SimConnect;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Xml.Serialization;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace SimAware.Client.SimConnectFSX
{
    public class MicrosoftSimConnection : IFlightConnector
    {
        public bool SlowMode { get; private set; }

        public event EventHandler<AircraftDataUpdatedEventArgs> AircraftDataUpdated;
        public event EventHandler<AircraftStatusUpdatedEventArgs> AircraftStatusUpdated;
        public event EventHandler AircraftPositionChanged;
        public event EventHandler<FlightPlanUpdatedEventArgs> FlightPlanUpdated;
        public event EventHandler Connected;
        public event EventHandler Closed;
        public event EventHandler<ConnectorErrorEventArgs> Error;

        private TaskCompletionSource<FlightPlanData> flightPlanTcs = null;
        private TaskCompletionSource<AircraftData> aircraftDataTcs = null;

        // User-defined win32 event
        const int WM_USER_SIMCONNECT = 0x0402;
        public IntPtr Handle { get; private set; }
        private SimConnect simconnect = null;
        private CancellationTokenSource cts = null;

        public MicrosoftSimConnection() { }

        public bool slowMode = false;

        #region Public Methods

        // <summary>
        //  Simconnect Client will send a Win32 message
        //  When there is a packet to process. This model keeps simconnect 
        //  Processing on the main thread.
        // </summary>

        public IntPtr HandleSimConnectEvents(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam, ref bool isHandled)
        {
            isHandled = false;

            switch(message)
            {
                case WM_USER_SIMCONNECT:
                    {
                        if(simconnect != null)
                        {
                            try
                            {
                                this.simconnect.ReceiveMessage();
                            }
                            catch (Exception ex)
                            {
                                RecoverFromError(ex);
                            }

                            isHandled = true;
                        }
                    }
                    break;

                default:
                    break;
            }

            return IntPtr.Zero;
        }

        public void Initialize(IntPtr Handle, bool slowMode)
        {
            SlowMode = slowMode;

            simconnect = new SimConnect("Flight Events", Handle, WM_USER_SIMCONNECT, null, 0);

            // Listen to Connections and Disconnections
            simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(Simconnect_OnRecvOpen);
            simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(Simconnect_OnRecvQuit);

            // Listen to Exceptions
            simconnect.OnRecvException += Simconnect_OnRecvException;

            simconnect.OnRecvSimobjectDataBytype += Simconnect_OnRecvSimobjectDataBytypeAsync;
            simconnect.OnRecvSimobjectData += Simconnect_OnRecvSimobjectData;

            RegisterAircraftDataDefinition();
            RegisterFlightStatusDefinition();
            RegisterAircraftPositionDefinition();

            simconnect.SubscribeToSystemEvent(EVENTS.POSITION_CHANGED, "PositionChanged");
            simconnect.OnRecvEvent += Simconnect_OnRecvEvent;

            simconnect.OnRecvSystemState += Simconnect_OnRecvSystemState;

            Connected?.Invoke(this, new EventArgs());

        }

        public void CloseConnection()
        {
            try
            {
                cts?.Cancel();
                cts = null;
            }
            catch (Exception )
            {
                
            }
            try
            {
                if (simconnect != null)
                {
                    simconnect.Dispose();
                    simconnect = null;
                }
            }
            catch (Exception)
            { }
        }

        public Task<FlightPlanData> RequestFlightPlanAsync(CancellationToken cancellationtoken = default)
        {
            if (flightPlanTcs != null)
            {
                return flightPlanTcs.Task;
            }

            var tcs = new TaskCompletionSource<FlightPlanData>();
            flightPlanTcs = tcs;

            cancellationtoken.Register(() =>
            {
                if (tcs.TrySetCanceled())
                {
                    flightPlanTcs = null;
                }
            }, useSynchronizationContext: false);

            simconnect.RequestSystemState(DATA_REQUESTS.FLIGHT_PLAN, "FlightPlan");

            return flightPlanTcs.Task;

        }

        public Task<AircraftData> RequestAircraftDataAsync(CancellationToken cancellationtoken = default)
        {
            if(aircraftDataTcs != null)
            {
                return aircraftDataTcs.Task;
            }

            var tcs = new TaskCompletionSource<AircraftData>();
            aircraftDataTcs = tcs;

            cancellationtoken.Register(() =>
            {
                if (tcs.TrySetCanceled())
                {
                    aircraftDataTcs = null;
                }

            }, useSynchronizationContext: false);

            simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.AIRCRAFT_DATA, DEFINITIONS.AircraftData, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);

            return aircraftDataTcs.Task;

        }

        #endregion

        #region Private Methods

        #region Register Data Definitions

        private void RegisterAircraftDataDefinition()
        {
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "ATC TYPE",
                null,
                SIMCONNECT_DATATYPE.STRING32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "ATC MODEL",
                null,
                SIMCONNECT_DATATYPE.STRING32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "Title",
                null,
                SIMCONNECT_DATATYPE.STRING256,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "ESTIMATED CRUISE SPEED",
                "Knots",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            // Register with the simconnect managed wrapper marshaller
            // Skipping, means you will only receive uint in the .dwData field
            simconnect.RegisterDataDefineStruct<AircraftDataStruct>(DEFINITIONS.AircraftData);
        }

        private void RegisterFlightStatusDefinition()
        {
          

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE LATITUDE",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE LONGITUDE",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE ALTITUDE",
                "Feet",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE ALT ABOVE GROUND",
                "Feet",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE BANK DEGREES",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE HEADING DEGREES TRUE",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE HEADING DEGREES MAGNETIC",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "GROUND ALTITUDE",
                "Meters",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "GROUND VELOCITY",
                "Knots",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "AIRSPEED INDICATED",
                "Knots",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "VERTICAL SPEED",
                "Feet per minute",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "AMBIENT WIND VELOCITY",
                "Feet per second",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "AMBIENT WIND DIRECTION",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "SIM ON GROUND",
                "number",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "AUTOPILOT MASTER",
                "number",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "TRANSPONDER CODE:1",
                "Hz",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "COM ACTIVE FREQUENCY:1",
                "kHz",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "COM ACTIVE FREQUENCY:2",
                "kHz",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            // Register with the simconnect managed wrapper marshaller
            // Skipping, means you will only receive uint in the .dwData field
            simconnect.RegisterDataDefineStruct<FlightStatusStruct>(DEFINITIONS.FlightStatus);

        }

        private void RegisterAircraftPositionDefinition()
        {
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "PLANE LATITUDE",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "PLANE LONGITUDE",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
                "PLANE ALTITUDE",
                "Feet",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            // Register with the simconnect managed wrapper marshaller
            // Skipping, means you will only receive uint in the .dwData field
            simconnect.RegisterDataDefineStruct<AircraftPositionStruct>(DEFINITIONS.AircraftPosition);
        }

        #endregion

        private void Simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            switch (data.dwRequestID)
            {
                case (uint)DATA_REQUESTS.FLIGHT_STATUS:
                    {

                        var flightStatus = data.dwData[0] as FlightStatusStruct?;
                        if(flightStatus.HasValue)
                        {
                            
                            AircraftStatusUpdated?.Invoke(this, new AircraftStatusUpdatedEventArgs(
                                new AircraftStatus
                                {
                                    Latitude = flightStatus.Value.Latitude,
                                    Longitude = flightStatus.Value.Longitude,
                                    Altitude = flightStatus.Value.Altitude,
                                    AltitudeAboveGround = flightStatus.Value.AltitudeAboveGround,
                                    Bank = flightStatus.Value.Bank,
                                    Heading = flightStatus.Value.MagneticHeading,
                                    TrueHeading = flightStatus.Value.TrueHeading,
                                    GroundSpeed = flightStatus.Value.GroundSpeed,
                                    IndicatedAirSpeed = flightStatus.Value.IndicatedAirspeed,
                                    VerticalSpeed = flightStatus.Value.VerticalSpeed,
                                    IsOnGround = flightStatus.Value.IsOnGround == 1,
                                    Transponder = flightStatus.Value.Transponder.ToString().PadLeft(4, '0'),
                                    FrequencyCom1 = flightStatus.Value.Com1,
                                    FrequencyCom2 = flightStatus.Value.Com2
                                }));
                        }
                    }
                    break;
            }
        }

        private void Simconnect_OnRecvSimobjectDataBytypeAsync(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            switch (data.dwRequestID)
            {
                case (uint)DATA_REQUESTS.AIRCRAFT_DATA:
                    {
                        var aircraftData = data.dwData[0] as AircraftDataStruct?;

                        if(aircraftData.HasValue)
                        {
                            var result = new AircraftData
                            {
                                Type = aircraftData.Value.Type,
                                Model = aircraftData.Value.Model,
                                Title = aircraftData.Value.Title,
                                EstimatedCruiseSpeed = aircraftData.Value.EstimatedCruiseSpeed
                            };
                            AircraftDataUpdated?.Invoke(this, new AircraftDataUpdatedEventArgs(result));

                            aircraftDataTcs?.TrySetResult(result);
                            aircraftDataTcs = null;
                        }
                    }
                    break;
            }
        }

        void Simconnect_OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            switch ((SIMCONNECT_RECV_ID)data.dwID)
            {
                case SIMCONNECT_RECV_ID.EVENT_FILENAME:

                    break;
                case SIMCONNECT_RECV_ID.QUIT:

                    break;
            }

            switch ((EVENTS)data.uEventID)
            {
                case EVENTS.POSITION_CHANGED:
                    AircraftPositionChanged?.Invoke(this, new EventArgs());
                    break;
            }
        }

        private async void Simconnect_OnRecvSystemState(SimConnect sender, SIMCONNECT_RECV_SYSTEM_STATE data)
        {
            switch (data.dwRequestID)
            {
                case (int)DATA_REQUESTS.FLIGHT_PLAN:
                    if(!string.IsNullOrEmpty(data.szString))
                    {
                        var planName = data.szString;

                        if(planName == ".PLN")
                        {
                            await Task.Delay(5000);

                            simconnect.RequestSystemState(DATA_REQUESTS.FLIGHT_PLAN, "FlightPlan");
                        }
                        else
                        {
                            if (File.Exists(planName))
                            {
                                using var stream = File.OpenRead(planName);
                                var serializer = new XmlSerializer(typeof(FlightPlanDocumentXml));
                                var flightPlan = serializer.Deserialize(stream) as FlightPlanDocumentXml;

                                var flightPlanData = flightPlan.FlightPlan.ToData();
                                FlightPlanUpdated?.Invoke(this, new FlightPlanUpdatedEventArgs(flightPlanData));

                                flightPlanTcs?.TrySetResult(flightPlanData);
                                flightPlanTcs = null;
                            }
                            else
                            {
                                FlightPlanUpdated?.Invoke(this, new FlightPlanUpdatedEventArgs(null));
                                flightPlanTcs?.TrySetResult(null);
                                flightPlanTcs = null;
                            }
                        }
                    }
                    break;

            }
        }

        void Simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Debug.WriteLine("Loaded Simconnect");
            simconnect.RequestDataOnSimObject(DATA_REQUESTS.FLIGHT_STATUS, DEFINITIONS.FlightStatus, 0,
                !SlowMode ? SIMCONNECT_PERIOD.SECOND : SIMCONNECT_PERIOD.SIM_FRAME,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        }

        void Simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Closed?.Invoke(this, new EventArgs());
            CloseConnection();
        }

        void Simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            var error = (SIMCONNECT_EXCEPTION)data.dwException;
            Debug.WriteLine(error);
            Error?.Invoke(this, new ConnectorErrorEventArgs(error.ToString()));
        }

        private void RecoverFromError(Exception exception)
        {
            //      err 0xC000014B: CTD
            //      err 0xC00000B0: sim has exited

            CloseConnection();
            Closed?.Invoke(this, new EventArgs());
        }

        #endregion

    }
}
