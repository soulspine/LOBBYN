using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using System.Security.Authentication;
using System.Net.Http;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;

namespace soulspine
{
    // THIS CLASS WAS CREATED TO PROPERLY HANDLE CONNECTIONS AND DISCONNECTIONS TO THE LEAGUE CLIENT
    // INSPIRED BY https://github.com/Ponita0/PoniLCU AND MODIFIED TO FIT THE PROJECT
    // PREVIOUS COMMITS USED PoniLCU BUT IT WAS CAUSING STACK OVERFLOWS
    // THIS ISSUE WAS REPORTED AND THERE IS NO INTENT OF FIXING IT
    // https://github.com/Ponita0/PoniLCU/issues/19
    public class LeagueClient
    {
        private bool firstConnection = true;

        // process 
        private int? lcuPort = null;
        private string lcuToken = null;
        private string rawLcuToken = null;
        private bool lcuProcessRunning;

        // config
        public LeagueClientConfig config { get; private set; }

        // http and websocket
        private HttpClient client = new HttpClient(new HttpClientHandler()
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) =>
            {
                return true;
            }
        });
        private WebSocket socketConnection = null;
        ConcurrentDictionary<string, List<Action<OnWebsocketEventArgs>>> subscriptions = new ConcurrentDictionary<string, List<Action<OnWebsocketEventArgs>>>();

        //events
        public EventHandler<EventArgs> OnLobbyEnter = null;
        public EventHandler<EventArgs> OnLobbyLeave = null;
        public EventHandler<EventArgs> OnChampSelectEnter = null;
        public EventHandler<EventArgs> OnChampSelectLeave = null;
        public EventHandler<EventArgs> OnGameEnter = null;
        public EventHandler<EventArgs> OnGameLeave = null;

        // status
        public bool isConnected { get; private set; }
        public bool isInLobby { get; private set; }
        public bool isInChampSelect { get; private set; }
        public bool isInGame { get; private set; }

        public Summoner localSummoner { get; private set; }
        public string localSummonerRegion { get; private set; }

        public LeagueClient(LeagueClientConfig config = null)
        {
            if (config == null)
            {
                config = new LeagueClientConfig();
            }

            this.config = config;
            
            log("Started LOBBYN.");

            handleConnection();
        }

        //handles are the only things allowed to use .Result instead of await
        //just because I SAID SO

        private void handleConnection()
        {
            if (!(lcuProcessRunning = isProcessRunning("LeagueClientUx")) && firstConnection)
            {
                logError("League of Legends is not running, waiting for it to start...", false);
            }

        RESTART:

            while (!lcuProcessRunning)
            {
                Thread.Sleep(2000);
                lcuProcessRunning = isProcessRunning("LeagueClientUx");
            }

            List<(string, string)> leagueArgs = getProcessCmdArgs("LeagueClientUx");

            lcuPort = null;
            lcuToken = null;
            rawLcuToken = null;

            foreach ((string arg, string value) in leagueArgs)
            {
                if (arg == "app-port")
                {
                    lcuPort = int.Parse(value);
                }
                else if (arg == "remoting-auth-token")
                {
                    rawLcuToken = value;
                    lcuToken = base64Encode("riot:" + value);
                }

                if (lcuPort != null && lcuToken != null) break;
            }

            if (lcuPort == null || lcuToken == null)
            {
                lcuProcessRunning = false;
                goto RESTART;
            }

            bool? apiConnected = lcuApiReadyCheck().Result;

            while (true)
            {
                if (apiConnected == null) goto RESTART;
                else if (apiConnected == false)
                {
                    Thread.Sleep(2000);
                    apiConnected = lcuApiReadyCheck().Result;
                }
                else break; //ready
            }

            socketConnection = new WebSocket($"wss://127.0.0.1:{lcuPort}/", "wamp");
            socketConnection.SetCredentials("riot", rawLcuToken, true);
            socketConnection.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
            socketConnection.SslConfiguration.ServerCertificateValidationCallback = (a, b, c, d) => true;
            socketConnection.OnMessage += handleWebsocketMessage;
            socketConnection.OnClose += handleWebsocketDisconnection;
            socketConnection.Connect();

            isConnected = true;
            if (firstConnection) firstConnection = false;

            _websocketSubscriptionSend("OnJsonApiEvent", 5, isKey:true); //subscribing to all events because then you can assign methods to endpoint and not event - tldr its simpler and safer

            isInLobby = false;
            isInChampSelect = false;
            isInGame = false;

            HttpResponseMessage gameflowResponse = request(requestMethod.GET, "/lol-gameflow/v1/gameflow-phase").Result;

            if (gameflowResponse == null || gameflowResponse.StatusCode != HttpStatusCode.OK)
            {
                logError("Failed to get gameflow phase at startup.");
                gameflowEventProc("None");
            }
            else
            {
                gameflowEventProc(gameflowResponse.Content.ReadAsStringAsync().Result.Replace("\"", ""));
            }


            localSummoner = getLocalSummonerFromLCU().Result;
            localSummonerRegion = getSummonerRegionFromLCU(localSummoner).Result;

            log($"Connected to the LCU API. Logged in as {localSummoner.gameName}#{localSummoner.tagLine} - {localSummonerRegion}");
        }

        private void handleDisconnection(bool byLCUexit = false)
        {
            if (socketConnection.IsAlive) socketConnection.Close();

            if (byLCUexit) return;

            isConnected = false;

            socketConnection = null;

            isInChampSelect = false;
            isInLobby = false;
            isInLobby = false;

            localSummoner = null;
            localSummonerRegion = null;

            lcuToken = null;
            lcuPort = null;

            log("Disconnected from LCU API");

            while (lcuProcessRunning)
            {
                Thread.Sleep(2000);
                lcuProcessRunning = isProcessRunning("LeagueClientUx.exe");
            }

            log("League of Legends has been closed... Waiting for it to start again");
            Task.Run(() => handleConnection());
        }

        private string _websocketEventFromEndpoint(string endpoint)
        {
            if (!endpoint.StartsWith("/")) endpoint = "/" + endpoint;
            return "OnJsonApiEvent" + endpoint.Replace("/", "_");
        }

        private void handleWebsocketMessage(object sender, MessageEventArgs e)
        {
            //Console.WriteLine(e.Data);

            var arr = JsonConvert.DeserializeObject<JArray>(e.Data);

            if (arr.Count != 3) return;
            else if (Convert.ToInt32(arr[0]) != 8) return;
            else if (!arr[1].ToString().StartsWith("OnJsonApiEvent")) return;

            string eventKey = arr[1].ToString();
            dynamic data = arr[2];

            if (eventKey != "OnJsonApiEvent")
            {
                logError($"Received unknown event key: {eventKey}");
                return;
            }

            OnWebsocketEventArgs args = new OnWebsocketEventArgs()
            {
                Endpoint = data["uri"].ToString(),
                Type = data["eventType"].ToString(),
                Data = data["data"]
            };

            // special case for exiting the client
            if (args.Endpoint == "/process-control/v1/process")
            {
                string status = args.Data["status"].ToString();
                if (status == "Stopping")
                {
                    handleDisconnection(true);
                    return;
                }
            }
            // gameflow updates
            else if (args.Endpoint == "/lol-gameflow/v1/gameflow-phase")
            {
                gameflowEventProc(args.Data.ToString());
            }

            if (!subscriptions.ContainsKey(args.Endpoint)) return;

            foreach (Action<OnWebsocketEventArgs> action in subscriptions[args.Endpoint]) action(args);
        }

        private void mock(OnWebsocketEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private void gameflowEventProc(string phase)
        {
            switch (phase)
            {
                case "None":
                    if (isInLobby) OnLobbyLeave?.Invoke(this, EventArgs.Empty);
                    else if (isInChampSelect) OnChampSelectLeave?.Invoke(this, EventArgs.Empty);
                    else if (isInGame) OnGameLeave?.Invoke(this, EventArgs.Empty);

                    isInLobby = false;
                    isInChampSelect = false;
                    isInGame = false;

                    break;

                case "Lobby":
                    if (isInChampSelect) OnChampSelectLeave?.Invoke(this, EventArgs.Empty);
                    else if (isInGame) OnGameLeave?.Invoke(this, EventArgs.Empty);

                    OnLobbyEnter?.Invoke(this, EventArgs.Empty);

                    isInLobby = true;
                    isInChampSelect = false;
                    isInGame = false;
                    break;

                case "ChampSelect":
                    if (isInLobby) OnLobbyLeave?.Invoke(this, EventArgs.Empty);
                    else if (isInGame) OnGameLeave?.Invoke(this, EventArgs.Empty);

                    OnChampSelectEnter?.Invoke(this, EventArgs.Empty);

                    isInLobby = false;
                    isInChampSelect = true;
                    isInGame = false;
                    break;

                case "InProgress":
                    if (isInLobby) OnLobbyLeave?.Invoke(this, EventArgs.Empty);
                    else if (isInChampSelect) OnChampSelectLeave?.Invoke(this, EventArgs.Empty);

                    OnGameEnter?.Invoke(this, EventArgs.Empty);

                    isInLobby = false;
                    isInChampSelect = false;
                    isInGame = true;
                    break;
            }
        }

        private void handleWebsocketDisconnection(object sender, CloseEventArgs e)
        {
            log("Websocket connection closed.", false);
            handleDisconnection();
        }

        private bool _websocketSubscriptionSend(string endpoint, int opcode, bool isKey = false)
        {
            if (!isConnected || socketConnection == null)
            {
                switch (opcode)
                {
                    case 5:
                        logError($"Tried to subscribe to {endpoint}, but LCU is not connected.");
                        break;

                    case 6:
                        logError($"Tried to unsubscribe from {endpoint}, but LCU is not connected.");
                        break;

                    default:
                        logError($"Tried to send a message to {endpoint}, but LCU is not connected.");
                        break;
                }

                return false;
            }

            if (!isKey) socketConnection.Send($"[{opcode}, \"{_websocketEventFromEndpoint(endpoint)}\"]");
            else socketConnection.Send($"[{opcode}, \"{endpoint}\"]");
            return true;
        }

        public void Subscribe(string endpoint, Action<OnWebsocketEventArgs> action)
        {
            websocketSubscribe(endpoint, action);
        }

        public void Unsubscribe(string endpoint, Action<OnWebsocketEventArgs> action)
        {
            websocketUnsubscribe(endpoint, action);
        }

        private void websocketSubscribe(string endpoint, Action<OnWebsocketEventArgs> action = null)
        {
            if (action == null) return;

            if (!endpoint.StartsWith("/")) endpoint = "/" + endpoint;

            if (!subscriptions.ContainsKey(endpoint))
            {
                subscriptions.TryAdd(endpoint, new List<Action<OnWebsocketEventArgs>>() { action });
            }
            else
            {
                subscriptions[endpoint].Add(action);
            }
        }

        private void websocketUnsubscribe(string endpoint, Action<OnWebsocketEventArgs> action = null)
        {
            if (!endpoint.StartsWith("/")) endpoint = "/" + endpoint;

            if (!subscriptions.ContainsKey(endpoint))
            {
                logError($"Tried to unsubscribe from {endpoint}, but there are no actions binded to it.");
                return;
            }

            if (action == null)
            {
                subscriptions.TryRemove(endpoint, out _);
            }
            else
            {
                if (!subscriptions[endpoint].Remove(action))
                {
                    logError($"Tried to unsubscribe {action.ToString()} from {endpoint}, but this action was not bound.");
                }
                else if (subscriptions[endpoint].Count == 0)
                {
                    subscriptions.TryRemove(endpoint, out _);
                }
            }
        }

        private void websocketClearSubscriptions()
        {
            subscriptions.Clear();
        }

        public void log(string message, bool toFile = true)
        {
            string dateSig = $"{DateTime.Now.ToString("[dd-MM-yyyy | HH:mm:ss]")}";
            message = $"{dateSig} {message}";
            Console.WriteLine(message);

            if (!File.Exists(config.logfilePath)) File.Create(config.logfilePath).Close();

            if (!toFile && !config.logEverything) return;

            var sw = new StreamWriter(File.Open(config.logfilePath, FileMode.Append));
            sw.Write($"{message}\n");
            sw.Close();
        }

        public void logError(string message, bool toFile = true)
        {
            log($"[ERROR] {message}", toFile);
        }

        private bool isProcessRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }

        private List<(string, string)> getProcessCmdArgs(string processName)
        {
            if (!isProcessRunning(processName)) return new List<(string, string)>();

            if (!processName.EndsWith(".exe")) processName += ".exe";

            string command = $"wmic process where \"caption='{processName}'\" get CommandLine";
            Process p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = "/c " + command;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.UseShellExecute = false;
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            List<(string, string)> outputList = new List<(string, string)>();

            foreach (string line in output.Split(' '))
            {
                string rawArg = line.Replace("\"", "").Replace("--", "");

                if (rawArg.Contains("="))
                {
                    string arg = rawArg.Split(Convert.ToChar("="))[0];
                    string value = rawArg.Split(Convert.ToChar("="))[1];
                    outputList.Add((arg, value));
                }
                else
                {
                    outputList.Add((rawArg, ""));
                }
            }

            return outputList;
        }

        private string base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public async Task<HttpResponseMessage> request(requestMethod method, string endpoint, dynamic data = null, bool ignoreReadyCheck = false)
        {
            if (!ignoreReadyCheck && !isConnected)
            {
                logError($"Tried to request {endpoint}, but LCU is not connected yet.");
                return null;
            }

            if (endpoint.StartsWith("/")) endpoint = endpoint.Substring(1);

            HttpRequestMessage request = new HttpRequestMessage
            {
                Method = new HttpMethod(method.ToString()),
                RequestUri = new Uri($"https://127.0.0.1:{lcuPort}/{endpoint}"),
                Headers =
                    {
                        { HttpRequestHeader.Authorization.ToString(), $"Basic {lcuToken}" },
                        { HttpRequestHeader.Accept.ToString(), "application/json" }
                    },
            };

            if (data != null)
            {
                request.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
            }

            try
            {
                return await client.SendAsync(request);
            }
            catch (Exception e)
            {
                if (!ignoreReadyCheck) logError($"Failed to send request to {endpoint}. - {e.Message}");
                return null;
            }
        }

        private async Task<bool?> lcuApiReadyCheck()
        {
            HttpResponseMessage response;
            try
            {
                response = await request(requestMethod.GET, "/lol-gameflow/v1/availability", ignoreReadyCheck: true);
            }
            catch { return null; }

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }
            else
            {
                JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
                if (json == null) return null;
                else return json["isAvailable"].ToObject<bool>();
            }
        }

        public async Task<List<Summoner>> GetSummoners(List<Tuple<string, string>> tupList)
        {
            List<string> names = new List<string>();

            foreach ((string name, string tagline) in tupList)
            {
                names.Add($"{name}#{tagline}");
            }

            HttpResponseMessage response = await request(requestMethod.POST, "/lol-summoner/v2/summoners/names", names);

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                string v = "";

                foreach (string n in names) v += $" {n}";

                logError($"Failed to get summoner data for: [{v.Substring(1)}]");
                return null;
            }
            else
            {
                return JsonConvert.DeserializeObject<List<Summoner>>(await response.Content.ReadAsStringAsync());
            }

            
        }

        public async Task<Summoner> GetSummoner(string name, string tagline)
        {
            List<Tuple<string, string>> list = new List<Tuple<string, string>> { new Tuple<string, string>(name, tagline) };
            List<Summoner> outList = await GetSummoners(list);
            if (outList == null || outList.Count == 0) return null;
            else return outList[0];
        }

        private async Task<Summoner> getLocalSummonerFromLCU()
        {
            HttpResponseMessage response = await request(requestMethod.GET, "/lol-summoner/v1/current-summoner");

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                logError("Failed to get local summoner data.");
                return null;
            }
            else
            {
                return JsonConvert.DeserializeObject<Summoner>(await response.Content.ReadAsStringAsync());
            }
        }

        private async Task<string> getSummonerRegionFromLCU(Summoner summoner)
        {
            HttpResponseMessage response = await request(requestMethod.GET, $"/riotclient/region-locale");

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                logError("Failed to get region locale.");
                return null;
            }
            else
            {
                string region = JObject.Parse(await response.Content.ReadAsStringAsync())["region"].ToString();

                return region;
            }
        }
    }

    public enum requestMethod
    {
        GET, POST, PATCH, DELETE, PUT
    }

    public class LeagueClientConfig
    {
        public bool logToFile { get; set; }
        public bool logEverything { get; set; }
        public string logfilePath { get; set;}

        public LeagueClientConfig()
        {
            logToFile = true;
            logEverything = true;
            logfilePath = "soulspineLCU.log";
        }

    }

    public class OnWebsocketEventArgs : EventArgs
    {   // URI    
        public string Endpoint { get; set; }

        // Update create delete
        public string Type { get; set; }

        // data :D
        public dynamic Data { get; set; }
    }

    public class RerollPoints
    {
        public int currentPoints { get; set; }
        public int maxRolls { get; set; }
        public int numberOfRolls { get; set; }
        public int pointsCostToRoll { get; set; }
        public int pointsToReroll { get; set; }
    }

    public class Summoner
    {
        public Int64 accountId { get; set; }
        public string displayName { get; set; }
        public string gameName { get; set; }
        public string internalName { get; set; }
        public bool nameChangeFlag { get; set; }
        public int percentCompleteForNextLevel { get; set; }
        public string privacy { get; set; }
        public int profileIconId { get; set; }
        public string puuid { get; set; }
        public RerollPoints rerollPoints { get; set; }
        public Int64 summonerId { get; set; }
        public int summonerLevel { get; set; }
        public string tagLine { get; set; }
        public bool unnamed { get; set; }
        public Int64 xpSinceLastLevel { get; set; }
        public Int64 xpUntilNextLevel { get; set; }
    }
}
