using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using DiscordRPC;
using Newtonsoft.Json;

namespace SimAware.Client.Logic
{
    public class DiscordRichPresenceLogic
    {
        private readonly DiscordRpcClient discordRpcClient;
        private readonly HttpClient httpClient;

        private readonly Dictionary<string, AirportDataResult> cachedAirports = new Dictionary<string, AirportDataResult>();
        private readonly ThrottleExecutor updateExecutor = new ThrottleExecutor(TimeSpan.FromMilliseconds(1000));
        private readonly ThrottleExecutor geocodeExecutor = new ThrottleExecutor(TimeSpan.FromMilliseconds(60000));

        private AircraftStatus lastStatus = null;
        private Timestamps groundStateChanged = null;

        private bool isStarted = false;
        private bool isConnected = false;

        private string callsign;
        private string lastICAO = null;
        private string lastAirport = null;

        public DiscordRichPresenceLogic(DiscordRpcClient discordRpcClient, IFlightConnector flightConnector)
        {
            this.discordRpcClient = discordRpcClient;
            httpClient = new HttpClient();

            flightConnector.Connected += FlightConnector_Connected;
            flightConnector.Closed += FlightConnector_Closed;
            flightConnector.AircraftStatusUpdated += FlightConnector_AircraftStatusUpdated;
            
        }

        public void Initialize()
        {
            
            try
            {
                discordRpcClient.Initialize();
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
            }
        }

        public void Start(string callsign)
        {
            isStarted = true;
            this.callsign = callsign;
            if(isConnected)
            {
                Debug.WriteLine("Setting Preparing: Discord RPC Started");
                SetPreparing();
            }
        } 

        public void Stop()
        {
            isStarted = false;
            ClearPresence();
        }

        private void FlightConnector_Connected(object sender, EventArgs e)
        {
            isConnected = true;
            if (isStarted)
            {
                Debug.WriteLine("FlightConnector Connected: Setting Status to Preparing.");
                SetPreparing();
            }
        }

        private void FlightConnector_Closed(object sender, EventArgs e)
        {
            isConnected = false;
            Debug.WriteLine("SimConnect Connection Lost");
            ClearPresence();
        }

        private async void FlightConnector_AircraftStatusUpdated(object sender, AircraftStatusUpdatedEventArgs e)
        {


            if (!isStarted) return;

            var status = e.AircraftStatus;

            DetectTakeOffLanding(status);

            lastStatus = status;

            await updateExecutor.ExecuteAsync(async () =>
            {
                if (Math.Abs(status.Latitude) < 0.02 && Math.Abs(status.Longitude) < 0.02)
                {
                    if(isStarted)
                    {
                        Debug.WriteLine(e.AircraftStatus.Latitude + " " + e.AircraftStatus.Longitude);
                        Debug.WriteLine("Latitude less than .02, Longitude less than .02. Setting to Preparing.");
                        SetPreparing();
                    }
                }
                else
                {
                    try
                    {

                        
                        string icao = lastICAO;
                        string airport = lastAirport;
                        await geocodeExecutor.ExecuteAsync(async () =>
                        {
                            try
                            {
                                Debug.Write(e.AircraftStatus.Latitude + e.AircraftStatus.Longitude);
                                var dataString = await httpClient.GetStringAsync($"http://iatageo.com/getCode/{e.AircraftStatus.Latitude.ToString(CultureInfo.InvariantCulture)}/{e.AircraftStatus.Longitude.ToString(CultureInfo.InvariantCulture)}");
                                var result = JsonConvert.DeserializeObject<IATAGeoResult>(dataString);
                                icao = result.ICAO;
                                airport = result.name;
                            }
                            catch (Exception ex) {
                                Debug.WriteLine("HTTPRequestException, IATA-DATA: " + ex);
                            }
                        });
                        lastICAO = icao;
                        lastAirport = airport;

                        string country = null;
                        if(!string.IsNullOrEmpty(icao))
                        {
                            if(cachedAirports.TryGetValue(icao, out var airportData))
                            {
                                country = airportData.country;
                            }
                            else
                            {
                                try
                                {
                                    var dataString = await httpClient.GetStringAsync($"https://www.airport-data.com/api/ap_info.json?icao={icao}");
                                    var result = JsonConvert.DeserializeObject<AirportDataResult>(dataString);
                                    cachedAirports.TryAdd(icao, result);
                                    country = result.country;
                                }
                                catch (Exception ex) {
                                    Debug.WriteLine("HTTPRequestException, AIRPORT-DATA: " + ex);
                                }
                            }
                        }

                        var tooltip = callsign;
                        var details = string.Empty;
                        if(!string.IsNullOrEmpty(airport))
                        {
                            tooltip += " Near " + airport;
                        }
                        if(!string.IsNullOrEmpty(icao))
                        {
                            details += $" Near {icao}";
                            tooltip += $" ({icao})";
                        }
                        if(!string.IsNullOrEmpty(country))
                        {
                            details += $", {country}";
                            tooltip += $" in {country}";
                        }
                        


                        discordRpcClient.SetPresence(new RichPresence
                        {
                            Details = details.Trim(),
                            State = status.IsOnGround ? "Currently on the Ground" : $"Alt {Math.Round(status.Altitude)}ft, {Math.Round(status.IndicatedAirSpeed)}kt",
                           
                            Assets = new Assets
                            {
                                LargeImageKey = "icon_large",
                                LargeImageText = tooltip.Trim()
                            },
                            Timestamps = groundStateChanged
                        });
                    }
                    catch (Exception) { }

                }
            });
        }

        private void DetectTakeOffLanding(AircraftStatus status)
        {
            if(lastStatus == null || status.IsOnGround != lastStatus.IsOnGround)
            {
                groundStateChanged = Timestamps.Now;
            }
        }

        private void SetPreparing()
        {
            try
            {
                Debug.WriteLine("Status set to Preparation.");
                discordRpcClient.SetPresence(new RichPresence()
                {
                    State = "Preflight...",
                    Assets = new Assets
                    {
                        LargeImageKey = "icon_large",
                        LargeImageText = "by Arvin Abdollahzadeh"
                    }
                });
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
            }
        }

        private void ClearPresence()
        {
            try
            {
                discordRpcClient.ClearPresence();
            }
            catch (Exception) { }
        }


    }

    public class ThrottleExecutor
    {
        private readonly TimeSpan interval;
        private DateTime lastExecution = DateTime.MinValue;

        public ThrottleExecutor(TimeSpan interval)
        {
            this.interval = interval;
        }

        public async Task ExecuteAsync(Func<Task> action)
        {
            if (DateTime.Now - lastExecution < interval) return;
            lastExecution = DateTime.Now;
            await action();
        }
    }
}
