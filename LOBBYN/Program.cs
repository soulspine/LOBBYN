using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using soulspine.LCU;
using System.Runtime.CompilerServices;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;

namespace LOBBYN
{
    internal class Program
    {
        static LOBBYNconfig config = new LOBBYNconfig
        {
            obsWebsocketPort = 4444,
            obsWebsocketPassword = "argentina123",

            champSelectRecordingPath = "/champSelects",
        };
        static LOBBYN lobbyn = new LOBBYN(config);

        static async Task Main(string[] args)
        {

            //lobbyn.Connect();

            //await lobbyn.CreateCustomLobby();

            lobbyn.obs.Connected += OnOBSConnected;
            lobbyn.obs.Disconnected += OnOBSDisconnected;

            lobbyn.lcu.OnConnected += OnLCUConnected;
            lobbyn.lcu.OnDisconnected += OnLCUDisconnected;

            //lobbyn.ChampSelectProjection("C:\\code\\LOBBYN\\LOBBYN\\champSelects\\RANKED_FLEX 24-06-2024 21-18-43.json");

            while (true)
            {
                Console.ReadKey();
                if (lobbyn.obs.IsConnected) lobbyn.obs.Disconnect();
                else lobbyn.obs.ConnectAsync("ws://localhost:" + config.obsWebsocketPort, config.obsWebsocketPassword);
                
            }
        }

        static void OnLCUConnected()
        {
            Console.WriteLine("LCU Connected");
        }

        static void OnLCUDisconnected()
        {
            Console.WriteLine("LCU Disconnected");
        }

        static void OnOBSConnected(object sender, EventArgs e)
        {
            Console.WriteLine("OBS Connected");
        }

        static void OnOBSDisconnected(object sender, ObsDisconnectionInfo e)
        {
            Console.WriteLine("OBS Disconnected");
        }
    }
}
