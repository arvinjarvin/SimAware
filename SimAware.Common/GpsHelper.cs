﻿using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SimAware
{
    public class GpsHelper
    {
        private static readonly Regex re = new Regex(@"^([N|S])([0-9]*[\.[0-9]+]*)[*|°]\s([0-9]*[\.[0-9]+]*)'\s([0-9]*[\.[0-9]+]*)"",([W|E])([0-9]*[\.[0-9]+]*)[*|°]\s([0-9]*[\.[0-9]+]*)'\s([0-9]*[\.[0-9]+]*)"",?([+-][0-9]*[\.[0-9]+]*)?$");

        public static (double latitude, double longitude, double? altitude) ConvertString(string data)
        {
            double lt, ln;
            lt = 0.0;
            ln = 0.0;
            double? alt = null;

            var m = re.Match(data);

            if(m.Success)
            {
                lt = Convert.ToDouble(m.Groups[2].Value, CultureInfo.InvariantCulture)
                    + (Convert.ToDouble(m.Groups[3].Value, CultureInfo.InvariantCulture) / 60.0)
                    + (Convert.ToDouble(m.Groups[4].Value, CultureInfo.InvariantCulture) / 3600.0);
                if(m.Groups[1].Value.Contains("S"))
                {
                    lt *= -1.0;
                }

                ln = Convert.ToDouble(m.Groups[6].Value, CultureInfo.InvariantCulture)
                    + (Convert.ToDouble(m.Groups[7].Value, CultureInfo.InvariantCulture) / 60.0)
                    + (Convert.ToDouble(m.Groups[8].Value, CultureInfo.InvariantCulture) / 3600.0);
                if (m.Groups[5].Value.Contains("W"))
                {
                    ln *= -1.0;
                }

                if(m.Groups.Count >= 10)
                {
                    alt = Convert.ToDouble(m.Groups[8].Value, CultureInfo.InvariantCulture);
                }
            }

            return (lt, ln, alt);
        }

        public static double CalculateDistance(double latitude1, double longitude1, 
            double latitude2, double longitude2)
        {
            var earthRadiusKt = 3440d;

            var dLat = DegreesToRadians(latitude2 - latitude1);
            var dLon = DegreesToRadians(longitude2 - longitude1);

            latitude1 = DegreesToRadians(latitude1);
            latitude2 = DegreesToRadians(latitude2);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(latitude1) * Math.Cos(latitude2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)); 

            return earthRadiusKt * c;
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

        public static (double x, double y, double z) ToXyz(double latitude, double longitude, double altitude)
        {
            var cosLat = Math.Cos(DegreesToRadians(latitude));
            var sinLat = Math.Sin(DegreesToRadians(latitude));
            var cosLon = Math.Cos(DegreesToRadians(longitude));
            var sinLon = Math.Sin(DegreesToRadians(longitude));

            var rad = 500.0;

            return (rad * cosLat * cosLon, rad * cosLat * sinLon, rad * sinLat);
        }
    }
}
