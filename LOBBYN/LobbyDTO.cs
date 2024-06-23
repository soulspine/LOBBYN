using System.Collections.Generic;
using System;

namespace LOBBYN
{
    public class CustomTeamMember
    {
        public bool allowedChangeActivity { get; set; }
        public bool allowedInviteOthers { get; set; }
        public bool allowedKickOthers { get; set; }
        public bool allowedStartActivity { get; set; }
        public bool allowedToggleInvite { get; set; }
        public bool autoFillEligible { get; set; }
        public bool autoFillProtectedForPromos { get; set; }
        public bool autoFillProtectedForRemedy { get; set; }
        public bool autoFillProtectedForSoloing { get; set; }
        public bool autoFillProtectedForStreaking { get; set; }
        public int botChampionId { get; set; }
        public string botDifficulty { get; set; }
        public string botId { get; set; }
        public string botPosition { get; set; }
        public string botUuid { get; set; }
        public string firstPositionPreference { get; set; }
        public object intraSubteamPosition { get; set; }
        public bool isBot { get; set; }
        public bool isLeader { get; set; }
        public bool isSpectator { get; set; }
        public List<object> playerSlots { get; set; }
        public string puuid { get; set; }
        public object quickplayPlayerState { get; set; }
        public bool ready { get; set; }
        public string secondPositionPreference { get; set; }
        public bool showGhostedBanner { get; set; }
        public object strawberryMapId { get; set; }
        public object subteamIndex { get; set; }
        public int summonerIconId { get; set; }
        public int summonerId { get; set; }
        public string summonerInternalName { get; set; }
        public int summonerLevel { get; set; }
        public string summonerName { get; set; }
        public int teamId { get; set; }
        public object tftNPEQueueBypass { get; set; }
    }

    public class GameConfig
    {
        public List<object> allowablePremadeSizes { get; set; }
        public string customLobbyName { get; set; }
        public string customMutatorName { get; set; }
        public List<object> customRewardsDisabledReasons { get; set; }
        public string customSpectatorPolicy { get; set; }
        public List<object> customSpectators { get; set; }
        public List<CustomTeamMember> customTeam100 { get; set; }
        public List<CustomTeamMember> customTeam200 { get; set; }
        public string gameMode { get; set; }
        public bool isCustom { get; set; }
        public bool isLobbyFull { get; set; }
        public bool isTeamBuilderManaged { get; set; }
        public int mapId { get; set; }
        public int maxHumanPlayers { get; set; }
        public int maxLobbySize { get; set; }
        public int maxTeamSize { get; set; }
        public string pickType { get; set; }
        public bool premadeSizeAllowed { get; set; }
        public int queueId { get; set; }
        public bool shouldForceScarcePositionSelection { get; set; }
        public bool showPositionSelector { get; set; }
        public bool showQuickPlaySlotSelection { get; set; }
    }

    public class LocalMember
    {
        public bool allowedChangeActivity { get; set; }
        public bool allowedInviteOthers { get; set; }
        public bool allowedKickOthers { get; set; }
        public bool allowedStartActivity { get; set; }
        public bool allowedToggleInvite { get; set; }
        public bool autoFillEligible { get; set; }
        public bool autoFillProtectedForPromos { get; set; }
        public bool autoFillProtectedForRemedy { get; set; }
        public bool autoFillProtectedForSoloing { get; set; }
        public bool autoFillProtectedForStreaking { get; set; }
        public int botChampionId { get; set; }
        public string botDifficulty { get; set; }
        public string botId { get; set; }
        public string botPosition { get; set; }
        public string botUuid { get; set; }
        public string firstPositionPreference { get; set; }
        public object intraSubteamPosition { get; set; }
        public bool isBot { get; set; }
        public bool isLeader { get; set; }
        public bool isSpectator { get; set; }
        public List<object> playerSlots { get; set; }
        public string puuid { get; set; }
        public object quickplayPlayerState { get; set; }
        public bool ready { get; set; }
        public string secondPositionPreference { get; set; }
        public bool showGhostedBanner { get; set; }
        public object strawberryMapId { get; set; }
        public object subteamIndex { get; set; }
        public int summonerIconId { get; set; }
        public Int64 summonerId { get; set; }
        public string summonerInternalName { get; set; }
        public int summonerLevel { get; set; }
        public string summonerName { get; set; }
        public int teamId { get; set; }
        public object tftNPEQueueBypass { get; set; }
    }

    public class Member
    {
        public bool allowedChangeActivity { get; set; }
        public bool allowedInviteOthers { get; set; }
        public bool allowedKickOthers { get; set; }
        public bool allowedStartActivity { get; set; }
        public bool allowedToggleInvite { get; set; }
        public bool autoFillEligible { get; set; }
        public bool autoFillProtectedForPromos { get; set; }
        public bool autoFillProtectedForRemedy { get; set; }
        public bool autoFillProtectedForSoloing { get; set; }
        public bool autoFillProtectedForStreaking { get; set; }
        public int botChampionId { get; set; }
        public string botDifficulty { get; set; }
        public string botId { get; set; }
        public string botPosition { get; set; }
        public string botUuid { get; set; }
        public string firstPositionPreference { get; set; }
        public object intraSubteamPosition { get; set; }
        public bool isBot { get; set; }
        public bool isLeader { get; set; }
        public bool isSpectator { get; set; }
        public List<object> playerSlots { get; set; }
        public string puuid { get; set; }
        public object quickplayPlayerState { get; set; }
        public bool ready { get; set; }
        public string secondPositionPreference { get; set; }
        public bool showGhostedBanner { get; set; }
        public object strawberryMapId { get; set; }
        public object subteamIndex { get; set; }
        public int summonerIconId { get; set; }
        public int summonerId { get; set; }
        public string summonerInternalName { get; set; }
        public int summonerLevel { get; set; }
        public string summonerName { get; set; }
        public int teamId { get; set; }
        public object tftNPEQueueBypass { get; set; }
    }

    public class MucJwtDto
    {
        public string channelClaim { get; set; }
        public string domain { get; set; }
        public string jwt { get; set; }
        public string targetRegion { get; set; }
    }

    public class Lobby
    {
        public bool canStartActivity { get; set; }
        public GameConfig gameConfig { get; set; }
        public List<object> invitations { get; set; }
        public LocalMember localMember { get; set; }
        public List<Member> members { get; set; }
        public MucJwtDto mucJwtDto { get; set; }
        public string multiUserChatId { get; set; }
        public string multiUserChatPassword { get; set; }
        public string partyId { get; set; }
        public string partyType { get; set; }
        public object restrictions { get; set; }
        public List<object> scarcePositions { get; set; }
        public object warnings { get; set; }
    }
}
