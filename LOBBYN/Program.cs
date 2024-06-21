using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using soulspine;

namespace LOBBYN
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            LeagueClientConfig config = new LeagueClientConfig
            {
                logToFile = true,
                logEverything = true,
                logfilePath = "LOBBYN.log",
            };

            LOBBYN lobbyn = new LOBBYN(config);
            lobbyn.OnChampSelectEnter += onChampSelectEnter;

            while (true)
            {
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                break;
            }
        }

        private static void onChampSelectEnter(object sender, EventArgs e)
        {
            Console.WriteLine("Entered Champ Select");
        }
        /*
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

            HttpResponseMessage response = await lcu.request(requestMethod.POST, "/lol-lobby/v2/lobby", data);

            if (response == null || response.StatusCode != HttpStatusCode.OK)
            {
                lcu.logError($"Failed to create lobby \"{lobbyName}\" {gamemode} {pickType} {spectatorPolicy}");
            }
            else
            {
                lcu.log($"Created lobby \"{lobbyName}\" {gamemode} {pickType} {spectatorPolicy}");
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
        }*/
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
