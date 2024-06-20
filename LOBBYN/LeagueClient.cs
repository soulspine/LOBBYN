using Salaros.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

namespace LOBBYN
{
    // THIS CLASS WAS CREATED TO PROPERLY HANDLE CONNECTIONS AND DISCONNECTIONS TO THE LEAGUE CLIENT
    // INSPIRED BY https://github.com/Ponita0/PoniLCU AND MODIFIED TO FIT THE PROJECT
    // PREVIOUS COMMITS USED PoniLCU BUT IT WAS CAUSING STACK OVERFLOWS
    // THIS ISSUE WAS REPORTED AND THERE IS NO INTENT OF FIXING IT
    // https://github.com/Ponita0/PoniLCU/issues/19
    internal class LeagueClient
    {
        private bool firstConnection = true;

        // process 
        private int? lcuPort = null;
        private string lcuToken = null;
        private string rawLcuToken = null;
        private bool lcuProcessRunning;

        // config
        private readonly ConfigParser config = new ConfigParser(File.ReadAllText("config.ini"));
        private string logfilePath;
        bool logEverything;

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

        // status
        public bool isReady = false;
        public bool isInLobby = false;
        public bool isInChampSelect = false;
        public Summoner localAccount = null;

        public LeagueClient()
        {
            logfilePath = config.GetValue("Logger", "logfile");
            logEverything = Convert.ToBoolean(config.GetValue("Logger", "logEverything"));
            log("Started LOBBYN.");

            handleConnection();
        }

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

            bool? apiConnected = lcuApiReadyCheck();

            while (true)
            {
                if (apiConnected == null) goto RESTART;
                else if (apiConnected == false)
                {
                    Thread.Sleep(2000);
                    apiConnected = lcuApiReadyCheck();
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

            isReady = true;
            if (firstConnection) firstConnection = false;

            foreach (string eventKey in subscriptions.Keys)
            {
                websocketSubscribe(eventKey, isKey: true);
            }

            websocketSubscribe("/process-control/v1/process");
            localAccount = getLocalSummoner();

            log($"Connected to the LCU API. Logged in as {localAccount.gameName}#{localAccount.tagLine}");
        }

        private void handleDisconnection(bool byLCUexit = false)
        {
            if (socketConnection.IsAlive) socketConnection.Close();

            if (byLCUexit) return;

            socketConnection = null;

            isInChampSelect = false;
            isInLobby = false;
            localAccount = null;

            lcuToken = null;
            lcuPort = null;

            log("Disconnected from LCU API");

            while (lcuProcessRunning)
            {
                Thread.Sleep(2000);
                lcuProcessRunning = isProcessRunning("LeagueClientUx.exe");
            }
            isReady = false;
            log("League of Legends has been closed... Waiting for it to start again");
            Task.Run(() => handleConnection());
        }

        public class OnWebsocketEventArgs : EventArgs
        {   // URI    
            public string Endpoint { get; set; }

            // Update create delete
            public string Type { get; set; }

            // data :D
            public dynamic Data { get; set; }
        }

        private string _websocketEventFromEndpoint(string endpoint)
        {
            if (endpoint.StartsWith("/")) endpoint = endpoint.Substring(1);
            return "OnJsonApiEvent_" + endpoint.Replace("/", "_");
        }

        private void handleWebsocketMessage(object sender, MessageEventArgs e)
        {
            if (!e.IsText) return;

            var arr = JsonConvert.DeserializeObject<JArray>(e.Data);

            if (arr.Count != 3) return;
            else if (Convert.ToInt32(arr[0]) != 8) return;
            else if (!arr[1].ToString().StartsWith("OnJsonApiEvent")) return;

            string eventKey = arr[1].ToString();
            dynamic data = arr[2];

            if (eventKey == "OnJsonApiEvent_process-control_v1_process")
            {
                OnWebsocketEventArgs args = new OnWebsocketEventArgs()
                {
                    Endpoint = data["uri"].ToString(),
                    Type = data["eventType"].ToString(),
                    Data = data["data"]
                };

                string status = args.Data["status"].ToString();
                if (status == "Stopping")
                {
                    handleDisconnection(true);
                    return;
                }

            }

            if (!subscriptions.ContainsKey(eventKey))
            {
                logError($"Received {eventKey} but there were not methods binded to it.");
                return;
            }

            foreach (Action<OnWebsocketEventArgs> action in subscriptions[eventKey])
            {
                action(new OnWebsocketEventArgs()
                {
                    Endpoint = data["uri"].ToString(),
                    Type = data["eventType"].ToString(),
                    Data = data["data"]
                });
            }
        }

        private void mock(OnWebsocketEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private void handleWebsocketDisconnection(object sender, CloseEventArgs e)
        {
            log("Websocket connection closed.", false);
            handleDisconnection();
        }

        private bool _websocketSubscriptionSend(string endpoint, int opcode)
        {
            if (!isReady || socketConnection == null)
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

            socketConnection.Send($"[{opcode}, \"{_websocketEventFromEndpoint(endpoint)}\"]");
            return true;
        }

        private void websocketSubscribe(string endpoint, Action<OnWebsocketEventArgs> action = null, bool isKey = false)
        {

            if (isKey) endpoint = endpoint.Replace("OnJsonApiEvent_", "").Replace("_", "/");

            if (!_websocketSubscriptionSend(endpoint, 5))
            {
                logError($"Failed to subscribe to {endpoint}.");
                return;
            }

            if (action == null) return;

            string eventKey = _websocketEventFromEndpoint(endpoint);

            if (!subscriptions.ContainsKey(eventKey))
            {
                subscriptions.TryAdd(eventKey, new List<Action<OnWebsocketEventArgs>>() { action });
            }
            else
            {
                subscriptions[eventKey].Add(action);
            }
        }

        private void websocketUnsubscribe(string endpoint, Action<OnWebsocketEventArgs> action = null)
        {
            string eventKey = _websocketEventFromEndpoint(endpoint);

            if (!subscriptions.ContainsKey(eventKey))
            {
                logError($"Tried to unsubscribe from {endpoint}, but there are no actions binded to it.");
                return;
            }

            if (action == null)
            {
                subscriptions.TryRemove(eventKey, out _);
                _websocketSubscriptionSend(endpoint, 6);
            }
            else
            {
                if (!subscriptions[eventKey].Remove(action))
                {
                    logError($"Tried to unsubscribe {action.ToString()} from {endpoint}, but this action was not bound.");
                }
                else if (subscriptions[eventKey].Count == 0)
                {
                    subscriptions.TryRemove(eventKey, out _);
                    _websocketSubscriptionSend(endpoint, 6);
                }
            }

            _websocketSubscriptionSend(endpoint, 6);
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

            if (!File.Exists(logfilePath)) File.Create(logfilePath).Close();

            if (!toFile && !logEverything) return;

            var sw = new StreamWriter(File.Open(logfilePath, FileMode.Append));
            sw.Write($"{message}\n");
            sw.Close();
        }

        private void logError(string message, bool toFile = true)
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

        public enum requestMethod
        {
            GET, POST, PATCH, DELETE, PUT
        }

        private async Task<HttpResponseMessage> request(requestMethod method, string endpoint, dynamic data = null, bool ignoreReadyCheck = false)
        {
            if (!ignoreReadyCheck && !isReady)
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

        private bool? lcuApiReadyCheck()
        {
            HttpResponseMessage response;
            try
            {
                response = request(requestMethod.GET, "/lol-gameflow/v1/availability", ignoreReadyCheck: true).Result;
            }
            catch { return null; }

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }
            else
            {
                JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                if (json == null) return null;
                else return json["isAvailable"].ToObject<bool>();
            }
        }

        internal static class MapType
        {
            public const int SUMMONERS_RIFT = 11;
            public const int HOWLING_ABYSS = 12;
        }

        internal static class PickType
        {
            public const string BLIND = "SimulPickStrategy";
            public const string DRAFT = "DraftModeSinglePickStrategy";
            public const string ALL_RANDOM = "AllRandomPickStrategy";
            public const string TOURNAMENT = "TournamentPickStrategy";
        }

        internal static class SpectatorPolicy
        {
            public const string LOBBY = "LobbyAllowed";
            public const string FRIENDS = "FriendsAllowed";
            public const string ALL = "AllAllowed";
            public const string NONE = "NotAllowed";
        }

        private List<Summoner> getSummoners(List<Tuple<string, string>> tupList)
        {
            List<string> names = new List<string>();

            foreach ((string name, string tagline) in tupList)
            {
                names.Add($"{name}#{tagline}");
            }

            HttpResponseMessage response = request(requestMethod.POST, "/lol-summoner/v2/summoners/names", names).Result;

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                string v = "";

                foreach (string n in names) v += $" {n}";

                logError($"Failed to get summoner data for: [{v.Substring(1)}]");
                return null;
            }
            else
            {
                return JsonConvert.DeserializeObject<List<Summoner>>(response.Content.ReadAsStringAsync().Result);
            }

            
        }

        private Summoner getSummoner(string name, string tagline)
        {
            List<Tuple<string, string>> list = new List<Tuple<string, string>> { new Tuple<string, string>(name, tagline) };
            List<Summoner> outList = getSummoners(list);
            if (outList == null) return null;
            else return outList[0];
        }

        private Summoner getLocalSummoner()
        {
            HttpResponseMessage response = request(requestMethod.GET, "/lol-summoner/v1/current-summoner").Result;

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                logError("Failed to get local summoner data.");
                return null;
            }
            else
            {
                return JsonConvert.DeserializeObject<Summoner>(response.Content.ReadAsStringAsync().Result);
            }
        }

        public void CreateLobby(int mapId = MapType.SUMMONERS_RIFT, string lobbyName = "Lobbyn Custom Game", Int16 teamSize = 5, string password = "", string pickType = PickType.TOURNAMENT, string spectatorPolicy = SpectatorPolicy.ALL)
        {
            string gamemode;
            Int16 mutator;

            if (teamSize > 5) teamSize = 5;
            else if (teamSize < 1) teamSize = 1;

            switch (pickType)
            {
                case PickType.BLIND:
                    mutator = 1;
                    break;
                case PickType.DRAFT:
                    mutator = 2;
                    break;
                case PickType.ALL_RANDOM:
                    mutator = 4;
                    break;
                case PickType.TOURNAMENT:
                    mutator = 6;
                    break;
                default:
                    mutator = 6;
                    break;
            }

            switch (mapId)
            {
                case MapType.SUMMONERS_RIFT:
                    gamemode = "CLASSIC";
                    break;
                case MapType.HOWLING_ABYSS:
                    gamemode = "ARAM";
                    break;
                default:
                    gamemode = "CLASSIC";
                    break;
            }

            object data = new
            {
                customGameLobby = new
                {
                    configuration = new
                    {
                        gameMode = gamemode,
                        mapId = mapId,
                        teamSize = teamSize,
                        mutators = new
                        {
                            id = mutator
                        },
                        spectatorPolicy = spectatorPolicy,
                    },
                    lobbyName = lobbyName,
                    lobbyPassword = password,
                },
                isCustom = true,
            };

            HttpResponseMessage response = request(requestMethod.POST, "/lol-lobby/v2/lobby", data).Result;

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                logError($"Failed to create lobby \"{lobbyName}\" {gamemode} {pickType} {spectatorPolicy}");
            }
            else
            {
                log($"Created lobby \"{lobbyName}\" {gamemode} {pickType} {spectatorPolicy}");
            }
        }
    }
}
