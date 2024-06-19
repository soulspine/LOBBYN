using PoniLCU;
using static PoniLCU.LeagueClient;

using Salaros.Configuration;
using Newtonsoft.Json;
using System.Text;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using System.CodeDom;

namespace LOBBYN
{
    internal class MainHandler
    {
        public bool isConnected => _lcu.IsConnected;

        public LeagueClient _lcu = new LeagueClient(credentials.cmd);
        private ConfigParser _config = new ConfigParser(File.ReadAllText("config.ini"));

        private bool _inChampSelect = false;
        private List<Dictionary<string, object>> _champSelectEventHistory = new List<Dictionary<string, object>>();
        private bool _inLobby = false;
        private Lobby? _lobbyDetails = null;

        private string _logfilePath;
        public MainHandler()
        {
            _logfilePath = _config.GetValue("Logger", "logfile");
            _lcu.Subscribe("/lol-champ-select/v1/session", ChampSelectUpdate);
            _lcu.Subscribe("/lol-lobby/v2/lobby", LobbyUpdate);
            _lcu.Subscribe("/lol-gameflow/v1/session", GameflowUpdate);
        }

        public void log(string message, bool toFile = true)
        {
            string dateSig = $"{DateTime.Now.ToString("[dd-MM-yyyy | HH:mm:ss]")}";
            message = $"{dateSig} {message}";
            Console.WriteLine(message);

            if (!File.Exists(_logfilePath)) File.Create(_logfilePath).Close();

            FileStream fs = File.Open(_logfilePath, FileMode.Append);
            fs.Write(Encoding.UTF8.GetBytes($"{message}\n"));
            fs.Close();
        }

        private void logWithErrorCheck(string response, string successMessage, string errorMessage)
        {
            var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);

            if (responseObj == null)
            {
                log($"[ERROR] {errorMessage} - Response passed to logWithErrorCheck() is null");
            }
            else if (responseObj.ContainsKey("errorCode"))
            {
                log($"[ERROR] {errorMessage} - {responseObj["message"]}");
            }
            else
            {
                log(successMessage);
            }
        }

        public async Task<List<Summoner>> getSummoners(List<Tuple<string,string>> tupList)
        {
            List<string> names = new List<string>();

            foreach ((string name, string tagline) in tupList)
            {
                names.Add($"{name}#{tagline}");
            }



            string response =  await _lcu.Request(requestMethod.POST, "/lol-summoner/v2/summoners/names", JsonConvert.SerializeObject(names));
            return JsonConvert.DeserializeObject<List<Summoner>>(response);
        }

        public async Task<Summoner?> getSummoner(string name, string tagline)
        {
            List<Tuple<string,string>> list = new List<Tuple<string, string>> { new Tuple<string, string>(name, tagline) };
            List<Summoner> outList = await getSummoners(list);
            if (outList.Count == 0) return null;
            else return outList[0];
        }

        public async Task CreateLobby(int mapId = MapType.SUMMONERS_RIFT, string lobbyName = "Lobbyn Custom Game", Int16 teamSize = 5, string password = "", string pickType = PickType.TOURNAMENT, string spectatorPolicy = SpectatorPolicy.ALL)
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

            string response = await _lcu.Request(requestMethod.POST, "/lol-lobby/v2/lobby", JsonConvert.SerializeObject(data));

            string lobbyMessage = $"\"{lobbyName}\" - {gamemode}, map {mapId}, , team size {teamSize}, pick type {pickType}, spectator policy {spectatorPolicy}";

            logWithErrorCheck(response, $"Created game lobby - {lobbyMessage}" , $"Failed to create game lobby");

            
            //log($"Created custom lobby - {lobbyName} - {gamemode}, map {mapId}, , team size {teamSize}, pick type {pickType}, spectator policy {spectatorPolicy}");
        }

        public async Task InvitePlayers(string[] summonerNames)
        {
            object data = new
            {
                summonerNames = summonerNames,
            };

            string response = await _lcu.Request(requestMethod.POST, "/lol-lobby/v2/lobby/invitations", JsonConvert.SerializeObject(data));

            logWithErrorCheck(response, $"Invited players to lobby", $"Failed to invite players to lobby");
        }

        private void ChampSelectUpdate(OnWebsocketEventArgs obj)
        {

            if (obj.Data != null)
            {
                if (_inChampSelect == false)
                {
                    _inChampSelect = true;
                    log($"Entered champ select for lobby \"{_lobbyDetails.gameConfig.customLobbyName}\"");
                }

                _champSelectEventHistory.Add(obj.Data.ToObject<Dictionary<string, object>>());

            }
            else if (_inChampSelect == true)
            {
                _inChampSelect = false;
                File.WriteAllText("test.json", JsonConvert.SerializeObject(_champSelectEventHistory));
                _champSelectEventHistory.Clear();
                log($"Finished champ select for lobby \"{_lobbyDetails.gameConfig.customLobbyName}\"");
            }

                
        }

        private void LobbyUpdate(OnWebsocketEventArgs obj)
        {

            if (obj.Data != null)
            {
                _lobbyDetails = obj.Data.ToObject<Lobby>();
                if (_inLobby == false)
                {
                    _inLobby = true;
                    log($"Entered lobby \"{_lobbyDetails.gameConfig.customLobbyName}\"");
                }
            }
            else if (_inLobby == true)
            {
                _inLobby = false;
                log($"Exited lobby \"{_lobbyDetails.gameConfig.customLobbyName}\"");
            }

            
        }

        private void GameflowUpdate(OnWebsocketEventArgs obj)
        {
            Console.WriteLine(obj.Data);
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
    }
