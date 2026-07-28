using Fusion;
using UnityEngine;

/// <summary>
/// Stateless facade for all party operations. It is the single seam the UI talks to
/// so no business logic leaks into panels/popups. It coordinates the three networking
/// pieces built in earlier steps:
///   • <see cref="PhotonManager"/>  — connection + spawning the PartyLobby object.
///   • <see cref="PartyLobby"/>     — the replicated roster / state / RPCs.
///   • <see cref="PlayerPresence"/> — per-player identity + invite mailbox.
///
/// Everything here is a thin translation from an intent ("invite this friend") to the
/// right networked call; there is no local mutable state — the authoritative state
/// lives on the Fusion objects. UI reads state via PartyLobby.Local + its events.
/// </summary>
public static class PartyService
{
    /// <summary>The party the local player is currently in, or null.</summary>
    public static PartyLobby CurrentParty => PartyLobby.Local;

    /// <summary>True when the local player is in a party and owns it.</summary>
    public static bool IsHost => PartyLobby.Local != null && PartyLobby.Local.IsLocalHost;

    /// <summary>True when connected to the social lobby (invites/party possible).</summary>
    public static bool IsOnline => PhotonManager.Instance != null && PhotonManager.Instance.IsConnected;

    // ─────────────────────────────────────────────────────────────────────────
    // 24.3 Create Party
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Create a party owned by the local player. Requires being connected to the
    /// social lobby. Returns the party (existing one if already in a party), or null
    /// if offline / spawn failed.
    /// </summary>
    public static PartyLobby CreateParty()
    {
        if (!IsOnline)
        {
            Debug.LogWarning("[PartyService] CreateParty ignored — not connected to social lobby.");
            return null;
        }
        return PhotonManager.Instance.CreateParty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 24.4 Invite Player
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Why an invite could not be sent. The UI needs this because "you are
    /// offline" and "your friend is offline" look identical from a bool.</summary>
    public enum InviteResult
    {
        Sent,
        NotConnected,     // local client has no lobby connection / no local presence
        FriendOffline,    // friend has no live presence in this lobby
        PartyUnavailable, // party could not be created, or we are not its host
        PartyFull,
    }

    /// <summary>
    /// Invite an online friend by profile id. The friend must currently be present in
    /// the social lobby (have a live <see cref="PlayerPresence"/>). Auto-creates the
    /// party if the local player has not made one yet.
    /// </summary>
    public static InviteResult InviteByProfileId(int friendProfileId)
    {
        if (!IsOnline) return InviteResult.NotConnected;

        // Verify the friend is reachable BEFORE creating a party, so a failed invite
        // never leaves the host stuck in an orphan party of one.
        var target = PlayerPresence.Find(friendProfileId);
        if (target == null)
        {
            Debug.Log($"[PartyService] Invite failed — friend {friendProfileId} is not online in the lobby.");
            return InviteResult.FriendOffline;
        }

        var me = PlayerPresence.Local;
        if (me == null) return InviteResult.NotConnected;

        var party = CurrentParty ?? CreateParty();
        if (party == null || !party.IsLocalHost) return InviteResult.PartyUnavailable;
        if (party.MemberCount >= PartyLobby.MaxMembers) return InviteResult.PartyFull;

        target.RPC_ReceiveInvite(me.ProfileId, me.DisplayName);
        party.RegisterPendingInvite();
        return InviteResult.Sent;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 24.6 Join Party (accept) / decline
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Accept an invite from the host with the given PROFILE id. Because the party lives
    /// in the shared social room the invitee is ALREADY connected, so it simply registers
    /// itself in the host's roster — no reconnect. Returns false if the host's party can't
    /// be found.
    ///
    /// Keyed on profile id, not PlayerRef: Fusion re-uses PlayerRefs as peers join and
    /// leave, so a slot freed by the inviter and taken by a stranger before the invitee
    /// pressed Accept used to seat them in the stranger's party.
    /// </summary>
    public static bool AcceptInvite(int hostProfileId)
    {
        if (!IsOnline) return false;

        var party = FindPartyByHostProfileId(hostProfileId);
        if (party == null)
        {
            Debug.LogWarning("[PartyService] AcceptInvite — host's party no longer exists.");
            return false;
        }

        var runner = PhotonManager.Instance.Runner;
        var me = PlayerPresence.Local;
        if (runner == null || me == null) return false;

        party.RPC_Join(runner.LocalPlayer, me.ProfileId, me.DisplayName, me.PlayerClass, me.Level);
        return true;
    }

    /// <summary>Decline an invite from the host with the given profile id (decrements its
    /// pending counter). Profile-id keyed for the same PlayerRef-reuse reason as
    /// <see cref="AcceptInvite"/>.</summary>
    public static void DeclineInvite(int hostProfileId)
    {
        FindPartyByHostProfileId(hostProfileId)?.RPC_InviteResolved();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 24.5 Kick / 24.7 Leave / 24.8 Ready
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Host-only: kick a member by PlayerRef.</summary>
    public static void KickMember(PlayerRef target)
    {
        var party = CurrentParty;
        if (party == null || !party.IsLocalHost) return;
        var runner = PhotonManager.Instance?.Runner;
        if (runner == null) return;
        party.RPC_Kick(runner.LocalPlayer, target);
    }

    /// <summary>
    /// Leave the current party. A plain member removes its own slot; the host runs the
    /// authority-transfer path and, if it was the last member, tears the party down.
    /// </summary>
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

    /// <summary>Set the local player's ready flag (24.8). Host is always ready.</summary>
    public static void SetReady(bool ready)
    {
        var party = CurrentParty;
        if (party == null) return;
        var runner = PhotonManager.Instance?.Runner;
        if (runner == null) return;
        party.RPC_SetReady(runner.LocalPlayer, ready);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Start Dungeon (host)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Host-only: publish the currently selected dungeon to the party so every
    /// member's panel shows it (24.2). Safe no-op if not in a party or not host.
    /// </summary>
    public static void SetDungeon(int configId, string sceneName, string dungeonName)
    {
        var party = CurrentParty;
        if (party == null || !party.IsLocalHost) return;
        party.HostSetDungeon(configId, sceneName, dungeonName);
    }

    /// <summary>
    /// Host-only: request dungeon start. PartyLobby validates the gate (≥2 members,
    /// all ready, no pending invite) and flips State→Loading. Step 5 hooks the actual
    /// scene load + Enter API onto <see cref="PartyLobby.OnDungeonStartRequested"/>.
    /// </summary>
    public static void StartDungeon(int configId, string sceneName)
    {
        var party = CurrentParty;
        if (party == null || !party.IsLocalHost) return;
        party.HostStartDungeon(configId, sceneName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Find the live party whose host has the given profile id, or null.</summary>
    public static PartyLobby FindPartyByHostProfileId(int hostProfileId)
    {
        if (hostProfileId <= 0) return null;
        foreach (var p in PartyLobby.All)
            if (p != null && p.HostProfileId == hostProfileId) return p;
        return null;
    }
}
