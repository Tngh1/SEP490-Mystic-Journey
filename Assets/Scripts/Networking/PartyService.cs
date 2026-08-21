using Fusion;
using UnityEngine;
using MysticJourney.Core.Utilities;

// Initializes a new default instance of the PartyService class.
public static class PartyService
{
    public const string DungeonAlreadyStartedMessage = "This party is no longer available because the dungeon has already started.";
    public const string PartyNoLongerExistsMessage = "This party no longer exists.";
    public const string PartyJoinUnavailableMessage = "Unable to join this party right now.";
    public const float InviteCooldownSeconds = 5f;
    private static float _nextInviteAllowedAt;

    public static float InviteCooldownRemaining => Mathf.Max(0f, _nextInviteAllowedAt - Time.unscaledTime);

    // Executes core business logic for current party.
    public static PartyLobby CurrentParty => PartyLobby.Local;

    // Executes core business logic for is host.
    public static bool IsHost => PartyLobby.Local != null && PartyLobby.Local.IsLocalHost;

    // Executes core business logic for is online.
    public static bool IsOnline => PhotonManager.Instance != null && PhotonManager.Instance.IsConnected;


    // Executes core business logic for create party.
    public static PartyLobby CreateParty()
    {
        if (!IsOnline)
        {
            Debug.LogWarning("[PartyService] CreateParty ignored — not connected to social lobby.");
            return null;
        }
        return PhotonManager.Instance.CreateParty();
    }


    // Executes core business logic for invite result.
    public enum InviteResult
    {
        Sent,
        NotConnected,
        FriendOffline,
        PartyUnavailable,
        PartyFull,
        FriendInDungeon,
        Cooldown,
        MapLocked,
    }

    public enum InviteAvailability
    {
        Available,
        PartyMissing,
        DungeonStarted,
    }

    // Process invite by profile id using friend profile id and required map id; it loads find and creates party and guards invalid or unavailable states.
    public static InviteResult InviteByProfileId(
        int friendProfileId,
        int requiredMapId = MapProgressionRules.FirstMapId)
    {
        if (!IsOnline) return InviteResult.NotConnected;
        if (InviteCooldownRemaining > 0f) return InviteResult.Cooldown;

        var target = PlayerPresence.Find(friendProfileId);
        if (target == null)
        {
            Debug.Log($"[PartyService] Invite failed — friend {friendProfileId} is not online in the lobby.");
            return InviteResult.FriendOffline;
        }

        if ((bool)target.IsInDungeon)
        {
            Debug.Log($"[PartyService] Invite failed — friend {friendProfileId} is already in a dungeon.");
            return InviteResult.FriendInDungeon;
        }

        var me = PlayerPresence.Local;
        if (me == null) return InviteResult.NotConnected;

        if (!MapProgressionRules.CanInviteToMap(requiredMapId, target.HighestUnlockedMapId))
        {
            Debug.Log($"[PartyService] Invite failed: friend {friendProfileId} has unlocked map " +
                      $"{target.HighestUnlockedMapId}, but map {requiredMapId} is required.");
            return InviteResult.MapLocked;
        }

        var party = CurrentParty ?? CreateParty();
        if (party == null || !party.IsLocalHost) return InviteResult.PartyUnavailable;
        if (party.MemberCount >= PartyLobby.MaxMembers) return InviteResult.PartyFull;

        target.RPC_ReceiveInvite(me.ProfileId, me.DisplayName);
        party.RegisterPendingInvite();
        _nextInviteAllowedAt = Time.unscaledTime + InviteCooldownSeconds;
        return InviteResult.Sent;
    }


    // Executes core business logic for accept invite.
    // Returns a boolean indicating operation success.
    public static bool AcceptInvite(int hostProfileId)
    {
        if (!IsOnline) return false;

        var party = FindPartyByHostProfileId(hostProfileId);
        if (party == null)
        {
            Debug.LogWarning("[PartyService] AcceptInvite — host's party no longer exists.");
            return false;
        }

        if (party.State != PartyLobby.PartyState.Lobby)
        {
            Debug.LogWarning($"[PartyService] AcceptInvite rejected — party state is {party.State}.");
            return false;
        }

        var runner = PhotonManager.Instance.Runner;
        var me = PlayerPresence.Local;
        if (runner == null || me == null) return false;

        party.RPC_Join(runner.LocalPlayer, me.ProfileId, me.DisplayName, me.PlayerClass, me.Level, WorldState.EquippedSkinId);
        return true;
    }

    // Executes core business logic for decline invite.
    public static void DeclineInvite(int hostProfileId)
    {
        FindPartyByHostProfileId(hostProfileId)?.RPC_InviteResolved();
    }

    // Returns true when the host's party has already left the lobby state.
    // The invite popup uses this shared check for both accept and decline actions.
    public static bool IsDungeonStarted(int hostProfileId) =>
        GetInviteAvailability(hostProfileId) == InviteAvailability.DungeonStarted;


    // Executes core business logic for kick member.
    public static void KickMember(PlayerRef target)
    {
        var party = CurrentParty;
        if (party == null || !party.IsLocalHost) return;
        var runner = PhotonManager.Instance?.Runner;
        if (runner == null) return;
        party.RPC_Kick(runner.LocalPlayer, target);
    }

    // Executes core business logic for leave party.
    public static void LeaveParty()
    {
        var party = CurrentParty;
        if (party == null) return;
        var runner = PhotonManager.Instance?.Runner;
        if (runner == null) return;

        if (party.IsLocalHost)
            party.LeaveAsHost();
        else
            party.RPC_Leave(runner.LocalPlayer);
    }

    // Executes core business logic for set ready.
    public static void SetReady(bool ready)
    {
        var party = CurrentParty;
        if (party == null) return;
        var runner = PhotonManager.Instance?.Runner;
        if (runner == null) return;
        party.RPC_SetReady(runner.LocalPlayer, ready);
    }


    // Executes core business logic for set dungeon.
    public static void SetDungeon(int configId, string sceneName, string dungeonName)
    {
        var party = CurrentParty;
        if (party == null || !party.IsLocalHost) return;
        party.HostSetDungeon(configId, sceneName, dungeonName);
    }

    // Executes core business logic for start dungeon.
    // Logic details: validates numeric boundary constraints.
    public static void StartDungeon(int configId, string sceneName)
    {
        var party = CurrentParty;
        if (party == null || !party.IsLocalHost) return;
        party.HostStartDungeon(configId, sceneName);
    }


    // Executes core business logic for find party by host profile id.
    // Logic details: validates numeric boundary constraints.
    public static PartyLobby FindPartyByHostProfileId(int hostProfileId)
    {
        if (hostProfileId <= 0) return null;
        foreach (var p in PartyLobby.All)
            if (p != null && p.HostProfileId == hostProfileId) return p;
        return null;
    }


    public static InviteAvailability GetInviteAvailability(int hostProfileId)
    {
        if (hostProfileId <= 0)
            return InviteAvailability.PartyMissing;

        var party = FindPartyByHostProfileId(hostProfileId);
        if (party == null)
            return InviteAvailability.PartyMissing;

        return party.State == PartyLobby.PartyState.Lobby
            ? InviteAvailability.Available
            : InviteAvailability.DungeonStarted;
    }
}
