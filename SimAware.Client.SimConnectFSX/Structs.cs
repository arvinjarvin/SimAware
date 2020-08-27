using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SimAware.Client.SimConnectFSX
{
    enum GROUPID
    {
        FLAG = 2000000000
    };

    enum DEFINITIONS
    { 
        AircraftData,
        FlightStatus,
        AircraftPosition
    }

    internal enum DATA_REQUESTS
    {
        NONE,
        SUBSCRIBE_GENERIC,
        AIRCRAFT_DATA,
        FLIGHT_STATUS,
        ENVIRONMENT_DATA,
        FLIGHT_PLAN
    }

    internal enum EVENTS
    {
        CONNECTED,
        MESSAGE_RECEIVED,
        POSITION_CHANGED
    }

    #region AircraftData

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    struct AircraftDataStruct
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Type;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Model;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Title;
        public double EstimatedCruiseSpeed;
    }

    #endregion

    #region FlightData

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    struct FlightStatusStruct
    {
        public double Latitude;
        public double Longitude;

        public double Altitude;
        public double AltitudeAboveGround;
        public double Bank;
        public double TrueHeading;
        public double MagneticHeading;
        public double GroundAltitude;
        public double GroundSpeed;
        public double IndicatedAirspeed;
        public double VerticalSpeed;
        public double FuelTotalQuantity;
        public double WindVelocity;
        public double WindDirection;

        public int IsOnGround;                      // Enumerate if the user is on the ground
        public int IsAutopilotOn;                   // Enumerate Autopilot

        public int Transponder;                     // Secondary Surveillance Radar (SSR) Code || Squawk
        public int Com1;                            // Com1 Frequency
        public int Com2;                            // Com2 Frequency

    }

    #endregion

    #region BodyPosition

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    struct AircraftPositionStruct
    {
        public double Latitude;
        public double Longitude;
        public double Altitude;
    }

    #endregion


}
