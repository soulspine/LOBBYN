using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using soulspineLCU;
using System.Runtime.CompilerServices;

namespace LOBBYN
{
    internal class Program
    {
        static LOBBYNconfig config = new LOBBYNconfig
        {
            champSelectRecordingPath = "/champSelects",
            lcuConfig = new LeagueClientConfig
            {
                autoReconnect = true,
                logToFile = true,
                logEverything = true,
                logfilePath = "soulspineLCU.log",
            }
        };
        static LOBBYN lobbyn = new LOBBYN(config);

        static async Task Main(string[] args)
        {

            lobbyn.Connect();

            //await lobbyn.CreateCustomLobby();

            while (true)
            {
                Console.ReadKey();
                
            }
        }
    }
}
