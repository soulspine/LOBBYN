using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using soulspineLCU;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;

namespace LOBBYN
{
    internal class LOBBYN : LeagueClient
    {
        private List<JObject> champSelectEventHistory;
        private Lobby lastLobbyDetails = null;
        private DateTime? lastLobbyEnterTimestamp = null;

        private DateTime? lastChampSelectEnterTimestamp = null;

        private LOBBYNconfig lobbynConfig;

        public LOBBYN(LOBBYNconfig config = null) : base(config.lcuConfig)
        {
            if (config != null)
            {
                config = new LOBBYNconfig();
            }

            lobbynConfig = config;

            OnChampSelectEnter += ChampSelectRecordingEnable;
            OnChampSelectLeave += ChampSelectRecordingDisable;

            OnLobbyEnter += CaptureCurrentLobby;

            Subscribe("/lol-gameflow/v1/gameflow-phase", gameflowUpdate);
        }

        private void gameflowUpdate(OnWebsocketEventArgs e)
        {
            log(e.Data.ToString(), false);
        }

        private void _champSelectRecordingOnUpdate(OnWebsocketEventArgs e)
        {
            champSelectEventHistory.Add(JObject.Parse(e.Data.ToString()));
        }

        public void ChampSelectRecordingEnable()
        {
            champSelectEventHistory = new List<JObject>();
            Subscribe("/lol-champ-select/v1/session", _champSelectRecordingOnUpdate);
            lastChampSelectEnterTimestamp = DateTime.Now;
        }

        public void ChampSelectRecordingDisable()
        {
            Unsubscribe("/lol-champ-select/v1/session", _champSelectRecordingOnUpdate);

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

            File.WriteAllText(filename, JsonConvert.SerializeObject(champSelectEventHistory));

            log($"Champ select recording saved to {filename}");

            lastChampSelectEnterTimestamp = null;
        }

        private void CaptureCurrentLobby()
        {
            HttpResponseMessage response = request(requestMethod.GET, "/lol-lobby/v2/lobby").Result;
            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                logError("Failed to capture current lobby");
                return;
            }
            lastLobbyDetails = JsonConvert.DeserializeObject<Lobby>(response.Content.ReadAsStringAsync().Result);
            lastLobbyEnterTimestamp = DateTime.Now;
            log(lastLobbyDetails.gameConfig.queueId.ToString(), false);
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

            HttpResponseMessage response = await request(requestMethod.POST, "/lol-lobby/v2/lobby", data);

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                logError($"Failed to create lobby \"{lobbyName}\" {gamemode} {pickType} {spectatorPolicy}");
            }
            else
            {
                log($"Created lobby \"{lobbyName}\" {gamemode} {pickType} {spectatorPolicy}");
            }
        }

        public async Task InvitePlayers(List<Tuple<string, string>> names)
        {

            List<Summoner> summoners = await GetSummoners(names);


            List<object> data = new List<object>();

            foreach (Summoner summoner in summoners)
            {
                data.Add(new
                {
                    toSummonerId = summoner.summonerId,
                    toSummonerName = summoner.displayName,
                });
            }

            await request(requestMethod.POST, "/lol-lobby/v2/lobby/invitations", data);
        }

        public async Task InvitePlayer(string name, string tagline)
        {
            await InvitePlayers(new List<Tuple<string, string>> { Tuple.Create(name, tagline) });
        }
    }

    internal class LOBBYNconfig
    {
        public LeagueClientConfig lcuConfig = new LeagueClientConfig();
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
