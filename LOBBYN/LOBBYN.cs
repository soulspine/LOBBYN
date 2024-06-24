using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using soulspine.LCU;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using OBSWebsocketDotNet;
using soulspine.LOBBYN;

namespace LOBBYN
{
    internal class LOBBYN
    {
        private List<JObject> champSelectEventHistory;
        private JObject lastChampSelectEventRaw; //needed to compare changes, otherwise its a bit of a mess

        private Lobby lastLobbyDetails = null;
        private DateTime? lastLobbyEnterTimestamp = null;
        private string lastSavedChampSelectFilepath = null;
        private DateTime? lastChampSelectEnterTimestamp = null;

        private LOBBYNconfig lobbynConfig;

        public LeagueClient lcu { get; private set; } = new LeagueClient();
        public OBSWebsocket obs { get; private set; } = new OBSWebsocket();

        private Logger logger = new Logger("soulspineLCU.log");

        public LOBBYN(LOBBYNconfig config = null)
        {
            if (config != null)
            {
                config = new LOBBYNconfig();
            }

            lobbynConfig = config;

            lcu.OnChampSelectEnter += ChampSelectRecordingEnable;
            lcu.OnChampSelectLeave += ChampSelectRecordingDisable;

            lcu.OnLobbyEnter += CaptureCurrentLobby;

            lcu.Subscribe("/lol-gameflow/v1/gameflow-phase", gameflowUpdate);

            if (lcu.isInChampSelect) ChampSelectRecordingEnable();
        }

        private void gameflowUpdate(OnWebsocketEventArgs e)
        {
            logger.log(e.Data.ToString(), false);
        }

        private void _champSelectRecordingOnUpdate(OnWebsocketEventArgs e)
        {
            champSelectEventHistory.Add(onlyChangedKeys(JObject.Parse(e.Data.ToString()), lastChampSelectEventRaw));
        }

        public void ChampSelectProjection(string filename) //TODO - CHAMP SELECT RENDER IN OBS
        {
            JArray champSelectEvents = JArray.Parse(File.ReadAllText(filename));
        }

        public void ChampSelectRecordingEnable()
        {
            champSelectEventHistory = new List<JObject>();
            lastChampSelectEventRaw = null;
            lcu.Subscribe("/lol-champ-select/v1/session", _champSelectRecordingOnUpdate);
            lastChampSelectEnterTimestamp = DateTime.Now;
        }

        public void ChampSelectRecordingDisable()
        {
            lcu.Unsubscribe("/lol-champ-select/v1/session", _champSelectRecordingOnUpdate);

            string filename;

            if (lobbynConfig.champSelectRecordingPath.StartsWith("/")) filename = lobbynConfig.champSelectRecordingPath.Substring(1);
            else filename = lobbynConfig.champSelectRecordingPath;

            if (!lobbynConfig.champSelectRecordingPath.EndsWith("/")) filename += "/";

            if (lastLobbyDetails == null) filename += $"UnknownLobby {DateTime.Now.ToString("dd-MM-yyyy HH-mm-ss")}";
            else if (lastLobbyDetails.gameConfig.isCustom) filename += $"{lastLobbyDetails.gameConfig.customLobbyName} {lastChampSelectEnterTimestamp.Value.ToString("dd-MM-yyyy HH-mm-ss")}";
            else filename += $"{(QueueID)lastLobbyDetails.gameConfig.queueId} {lastChampSelectEnterTimestamp.Value.ToString("dd-MM-yyyy HH-mm-ss")}";

            //filename += "test";

            filename += ".json";


            FileInfo fileinfo = new FileInfo(filename);
            fileinfo.Directory.Create(); // If the directory already exists, this method does nothing.

            File.WriteAllText(fileinfo.FullName, JsonConvert.SerializeObject(champSelectEventHistory));

            lastSavedChampSelectFilepath = fileinfo.FullName;

            logger.log($"Champ select recording saved to {lastSavedChampSelectFilepath}");
        }

        private void CaptureCurrentLobby()
        {
            HttpResponseMessage response = lcu.request(requestMethod.GET, "/lol-lobby/v2/lobby").Result;
            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                logger.logError("Failed to capture current lobby");
                return;
            }
            lastLobbyDetails = JsonConvert.DeserializeObject<Lobby>(response.Content.ReadAsStringAsync().Result);
            lastLobbyEnterTimestamp = DateTime.Now;
            logger.log(lastLobbyDetails.gameConfig.queueId.ToString(), false);
        }

        public JObject onlyChangedKeys(JObject current, JObject previous)
        {
            if (previous == null) return current;

            JObject changed = new JObject();

            foreach (var kvp in current)
            {
                if (previous[kvp.Key] == null) // key only in current, add it
                {
                    changed.Add(kvp.Key, kvp.Value);
                    continue;
                }
                else if (JToken.DeepEquals(kvp.Value, previous[kvp.Key])) continue; // key in both, but same value, 
                else if (kvp.Value.Type == JTokenType.Object) changed.Add(kvp.Key, onlyChangedKeys((JObject)kvp.Value, (JObject)previous[kvp.Key])); // key in both, different value, both are objects, recursively check
                else if (kvp.Value.Type == JTokenType.Array) // key in both, both are arrays, only leave new elements
                {
                    JArray currentArray = (JArray)kvp.Value;
                    JArray previousArray = (JArray)previous[kvp.Key];

                    JArray newArray = new JArray();

                    foreach (JToken token in currentArray) // comparison using JToken.DeepEquals
                    {
                        if (!previousArray.Any(x => JToken.DeepEquals(x, token))) newArray.Add(token);
                    }

                    changed.Add(kvp.Key, newArray);
                }
                else changed.Add(kvp.Key, kvp.Value);
            }

            return changed;
        }

        public async Task CreateCustomLobby(int mapId = MapType.SUMMONERS_RIFT, string lobbyName = "Lobbyn Custom Game", Int16 teamSize = 5, string password = "", string pickType = PickType.TOURNAMENT, string spectatorPolicy = SpectatorPolicy.ALL)
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

            HttpResponseMessage response = await lcu.request(requestMethod.POST, "/lol-lobby/v2/lobby", data);

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                logger.logError($"Failed to create lobby \"{lobbyName}\" {gamemode} {pickType} {spectatorPolicy}");
            }
            else
            {
                logger.log($"Created lobby \"{lobbyName}\" {gamemode} {pickType} {spectatorPolicy}");
            }
        }

        public async Task InvitePlayers(List<Tuple<string, string>> names)
        {

            List<Summoner> summoners = await lcu.GetSummoners(names);


            List<object> data = new List<object>();

            foreach (Summoner summoner in summoners)
            {
                data.Add(new
                {
                    toSummonerId = summoner.summonerId,
                    toSummonerName = summoner.displayName,
                });
            }

            await lcu.request(requestMethod.POST, "/lol-lobby/v2/lobby/invitations", data);
        }

        public async Task InvitePlayer(string name, string tagline)
        {
            await InvitePlayers(new List<Tuple<string, string>> { Tuple.Create(name, tagline) });
        }
    }

    internal class LOBBYNconfig
    {
        public int obsWebsocketPort = 4455;
        public string obsWebsocketPassword = null;

        public string champSelectRecordingPath = "/champSelects";
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
}
