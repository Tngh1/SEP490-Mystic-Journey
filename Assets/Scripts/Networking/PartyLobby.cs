using System;
using System.Collections.Generic;
using Fusion;
using MysticJourney.API.Models.Response;
using UnityEngine;

/// <summary>
/// Shared party roster for the pre-dungeon lobby. Spawned ONCE by the host
/// (master client) via <see cref="PhotonManager.EnsurePartyLobbySpawned"/> right
/// after entering the party room. Every client reads the replicated member list
/// to render the party slots + ready state in <see cref="UIPartyPanel"/>.
///
/// Authority model (Shared Mode): the host holds StateAuthority over this object
/// and is the only one that mutates the [Networked] arrays. Clients request
/// changes (join / ready / kick / leave) via RPCs routed to the StateAuthority.
///
/// Party lifecycle:
///   Lobby   → gathering members, inviting, ready-checking.
///   Loading → host pressed Start; the group is transitioning into the dungeon.
///   InDungeon → the dungeon session is live (avatars spawned, combat running).
///
/// The party lives inside the shared SOCIAL LOBBY room (see PhotonManager) so a
/// host can still reach idle friends via <see cref="PlayerPresence"/> invites.
/// </summary>
public class PartyLobby : NetworkBehaviour, IStateAuthorityChanged
{
    public const int MaxMembers = 4;

    public enum PartyState { Lobby, Loading, InDungeon }

    /// <summary>
    /// Every live party in the current room. Multiple parties can coexist in the
    /// shared social lobby, so this is a set, not a singleton. Keyed by NetworkId.
    /// </summary>
    private static readonly Dictionary<NetworkId, PartyLobby> _all = new();

    /// <summary>Read-only view of all live parties in the room.</summary>
    public static IReadOnlyCollection<PartyLobby> All => _all.Values;

    /// <summary>
    /// The party the local player currently belongs to (created or joined), or null.
    /// Set when the local player is seated in a roster; cleared when it leaves.
    /// </summary>
    public static PartyLobby Local { get; private set; }

    /// <summary>Raised whenever <see cref="Local"/> changes (joined / left a party).</summary>
    public static event Action OnLocalPartyChanged;

    /// <summary>One party member row. NetworkStruct so it can live in a NetworkArray.</summary>
    public struct Member : INetworkStruct
    {
        public PlayerRef Player;          // default(PlayerRef) == empty slot
        public int ProfileId;
        public NetworkString<_32> Name;
        public int PlayerClass;
        public int Level;
        public NetworkBool Ready;

        public bool IsOccupied => Player != default;
    }

    [Networked, Capacity(MaxMembers)]
    public NetworkArray<Member> Members => default;

    [Networked] public PlayerRef HostPlayer { get; set; }

    [Networked] public PartyState State { get; set; }

    /// <summary>
    /// Number of invites sent but not yet accepted/declined. Host-authority only.
    /// Gates Start Dungeon ("no invite pending"). Kept as a simple counter — an
    /// accept (<see cref="RPC_Join"/>) or a decline (<see cref="RPC_InviteResolved"/>)
    /// decrements it.
    /// </summary>
    [Networked] public int PendingInviteCount { get; set; }

    // Dungeon target, published by the host on Start so every client transitions to
    // the SAME scene/session. Members reuse the host's DungeonSessionId instead of
    // calling the Enter API again (no backend duplication).
    [Networked] public int DungeonConfigId { get; set; }
    [Networked] public int DungeonSessionId { get; set; }
    [Networked] public NetworkString<_32> DungeonSceneName { get; set; }
    [Networked] public NetworkString<_32> DungeonName { get; set; }

    /// <summary>Raised on every client whenever the replicated roster/host changes.</summary>
    public event Action OnRosterChanged;

    /// <summary>Raised on every client whenever <see cref="State"/> changes.</summary>
    public event Action<PartyState> OnPartyStateChanged;

    /// <summary>
    /// Raised on the host when the party should begin dungeon entry (State just became
    /// Loading). Step 5 wires this to DungeonManager. Args: configId, sceneName.
    /// </summary>
    public event Action<int, string> OnDungeonStartRequested;

    public event Action<PartyChatMessageResponse> PartyMessageReceived;

    private ChangeDetector _changes;

    // Set locally on the outgoing host right before it disconnects, so the promoted
    // member knows to claim StateAuthority when it arrives.
    private bool _claimAuthorityOnArrival;

    public override void Spawned()
    {
        _all[Object.Id] = this;
        _changes = GetChangeDetector(ChangeDetector.Source.SnapshotFrom);

        if (HasStateAuthority)
        {
            HostPlayer = Object.InputAuthority;
            State = PartyState.Lobby;
            // Seat the host in slot 0 from its local profile.
            SeatSelf(0, ready: true);
        }

        RefreshLocalMembership();
        OnRosterChanged?.Invoke();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _all.Remove(Object.Id);
        if (Local == this)
        {
            Local = null;
            OnLocalPartyChanged?.Invoke();
        }
    }

    public override void Render()
    {
        // Route networked changes to the relevant UI event.
        foreach (var change in _changes.DetectChanges(this))
        {
            if (change == nameof(State))
                OnPartyStateChanged?.Invoke(State);
            else
                OnRosterChanged?.Invoke();
        }

        RefreshLocalMembership();
    }

    /// <summary>
    /// Keep the static <see cref="Local"/> pointer in sync with roster membership:
    /// this party becomes Local when the local player is seated in it, and is cleared
    /// when the local player is no longer a member.
    /// </summary>
    private void RefreshLocalMembership()
    {
        if (Runner == null) return;

        bool localSeated = FindSlot(Runner.LocalPlayer) >= 0;

        if (localSeated && Local != this)
        {
            Local = this;
            OnLocalPartyChanged?.Invoke();
        }
        else if (!localSeated && Local == this)
        {
            Local = null;
            OnLocalPartyChanged?.Invoke();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Local helpers (state authority only)
    // ─────────────────────────────────────────────────────────────────────────

    private void SeatSelf(int slot, bool ready)
    {
        string className = WorldState.PlayerClass ?? "Knight";
        if (!Enum.TryParse<CharacterClass>(className, true, out var parsed))
            parsed = CharacterClass.Knight;

        var arr = Members;
        arr.Set(slot, new Member
        {
            Player = Object.InputAuthority,
            ProfileId = WorldState.PlayerProfileId,
            Name = WorldState.PlayerName ?? "Host",
            PlayerClass = (int)parsed,
            Level = Mathf.Max(1, WorldState.PlayerLevel),
            Ready = ready,
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Queries
    // ─────────────────────────────────────────────────────────────────────────

    public bool IsLocalHost =>
        Object != null && Runner != null && HostPlayer == Runner.LocalPlayer;

    /// <summary>Profile id of the host member (stable across rooms), or 0 if not found.
    /// Used to derive the dungeon room name so every member targets the same room.</summary>
    public int HostProfileId
    {
        get
        {
            for (int i = 0; i < MaxMembers; i++)
            {
                var m = Members[i];
                if (m.IsOccupied && m.Player == HostPlayer) return m.ProfileId;
            }
            return 0;
        }
    }

    public int MemberCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < MaxMembers; i++)
                if (Members[i].IsOccupied) n++;
            return n;
        }
    }

    public int ReadyCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < MaxMembers; i++)
            {
                var m = Members[i];
                if (m.IsOccupied && m.Ready) n++;
            }
            return n;
        }
    }

    /// <summary>True when every occupied slot is marked ready (host counts as ready).</summary>
    public bool AllReady
    {
        get
        {
            bool any = false;
            for (int i = 0; i < MaxMembers; i++)
            {
                var m = Members[i];
                if (!m.IsOccupied) continue;
                any = true;
                if (!m.Ready) return false;
            }
            return any;
        }
    }

    /// <summary>
    /// Host-only gate for Start Dungeon: at least 2 members, everyone ready, and no
    /// invite still pending.
    /// </summary>
    public bool CanStartDungeon =>
        MemberCount >= 2 && AllReady && PendingInviteCount <= 0 && State == PartyState.Lobby;
    public bool IsLocalMember =>
        Runner != null && FindSlot(Runner.LocalPlayer) >= 0;

    public bool BroadcastPartyMessage(PartyChatMessageResponse message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content))
        {
            return false;
        }

        if (Runner == null || !Runner.IsRunning || !IsLocalMember)
        {
            return false;
        }

        int senderId = WorldState.PlayerProfileId > 0 ? WorldState.PlayerProfileId : message.SenderId;
        if (senderId <= 0 || !HasMemberProfileId(senderId))
        {
            return false;
        }

        string senderName = !string.IsNullOrWhiteSpace(WorldState.PlayerName)
            ? WorldState.PlayerName
            : message.SenderName;

        NetworkString<_128> networkSenderName = TrimForFusion(senderName, 120);
        NetworkString<_512> networkContent = TrimForFusion(message.Content, 500);
        NetworkString<_64> networkSentAt = TrimForFusion(
            string.IsNullOrWhiteSpace(message.SentAt) ? DateTime.UtcNow.ToString("O") : message.SentAt,
            60);

        RPC_PartyMessageReceived(senderId, networkSenderName, networkContent, networkSentAt);
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_PartyMessageReceived(
        int senderId,
        NetworkString<_128> senderName,
        NetworkString<_512> content,
        NetworkString<_64> sentAt)
    {
        RefreshLocalMembership();

        if (Local != this || senderId <= 0 || !HasMemberProfileId(senderId))
        {
            return;
        }

        PartyMessageReceived?.Invoke(new PartyChatMessageResponse
        {
            SenderId = senderId,
            SenderName = senderName.ToString(),
            Content = content.ToString(),
            Channel = "Party",
            SentAt = sentAt.ToString()
        });
    }

    private bool HasMemberProfileId(int profileId)
    {
        for (int i = 0; i < MaxMembers; i++)
        {
            var member = Members[i];
            if (member.IsOccupied && member.ProfileId == profileId)
            {
                return true;
            }
        }

        return false;
    }

    private static string TrimForFusion(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    private int FindSlot(PlayerRef player)
    {
        for (int i = 0; i < MaxMembers; i++)
            if (Members[i].Player == player) return i;
        return -1;
    }

    private int FirstFreeSlot()
    {
        for (int i = 0; i < MaxMembers; i++)
            if (!Members[i].IsOccupied) return i;
        return -1;
    }

    /// <summary>First occupied slot whose player is not the host, or default when none.</summary>
    private PlayerRef FirstNonHostMember()
    {
        for (int i = 0; i < MaxMembers; i++)
        {
            var m = Members[i];
            if (m.IsOccupied && m.Player != HostPlayer) return m.Player;
        }
        return default;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Invite bookkeeping (host state authority)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called locally on the host right after it dispatches an invite (via the target's
    /// <see cref="PlayerPresence.RPC_ReceiveInvite"/>) so the pending counter reflects it.
    /// </summary>
    public void RegisterPendingInvite()
    {
        if (!HasStateAuthority) return;
        PendingInviteCount++;
    }

    /// <summary>
    /// Client → host: an invite was resolved without a join (the invitee declined, or
    /// the invite expired). Decrements the pending counter.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_InviteResolved()
    {
        if (!HasStateAuthority) return;
        if (PendingInviteCount > 0) PendingInviteCount--;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RPCs — client request → host (StateAuthority) applies
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by a client right after it joins the host's room, to register itself
    /// in the roster. Identity comes from the joining client's WorldState, forwarded
    /// here as arguments.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Join(PlayerRef player, int profileId, NetworkString<_32> name, int playerClass, int level)
    {
        if (!HasStateAuthority) return;
        if (State != PartyState.Lobby) return; // cannot join a party already entering the dungeon
        if (FindSlot(player) >= 0) return;      // already seated

        int slot = FirstFreeSlot();
        if (slot < 0) return; // party full

        var arr = Members;
        arr.Set(slot, new Member
        {
            Player = player,
            ProfileId = profileId,
            Name = name,
            PlayerClass = playerClass,
            Level = level,
            Ready = false,
        });

        // The joiner filled a pending invite slot.
        if (PendingInviteCount > 0) PendingInviteCount--;
    }

    /// <summary>Toggle a member's ready flag. The host slot stays always-ready.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetReady(PlayerRef player, NetworkBool ready)
    {
        if (!HasStateAuthority) return;
        int slot = FindSlot(player);
        if (slot < 0) return;
        if (player == HostPlayer) return; // host is always ready

        var m = Members[slot];
        m.Ready = ready;
        var arr = Members;
        arr.Set(slot, m);
    }

    /// <summary>Host-only: remove a member from the roster (24.5 Kick).</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Kick(PlayerRef requester, PlayerRef target)
    {
        if (!HasStateAuthority) return;
        if (requester != HostPlayer) return;   // only host may kick
        if (target == HostPlayer) return;       // host cannot kick itself

        int slot = FindSlot(target);
        if (slot < 0) return;

        var arr = Members;
        arr.Set(slot, default);
    }

    /// <summary>Client leaves the party (removes its own slot). Host leave is handled
    /// separately by <see cref="LeaveAsHost"/> because it transfers authority.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Leave(PlayerRef player)
    {
        if (!HasStateAuthority) return;
        if (player == HostPlayer) return; // host uses LeaveAsHost()

        int slot = FindSlot(player);
        if (slot < 0) return;

        var arr = Members;
        arr.Set(slot, default);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Leave / host transfer (24.7)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Graceful host leave. Runs on the host (state authority). If another member
    /// exists, authority transfers to the first of them: the host rewrites HostPlayer
    /// (while it still owns authority), clears its own slot, and asks the promoted
    /// member to claim StateAuthority. If the host is alone, the party is destroyed.
    /// Returns true if the party was torn down (caller should shut the session down).
    /// </summary>
    public bool LeaveAsHost()
    {
        if (!HasStateAuthority) return false;

        PlayerRef next = FirstNonHostMember();
        if (next == default)
        {
            // No one else — tear the party down.
            if (Runner != null && Object != null)
                Runner.Despawn(Object);
            return true;
        }

        // Promote: update host + drop own slot while we still hold authority, then
        // hand the object to the promoted member.
        int hostSlot = FindSlot(HostPlayer);
        HostPlayer = next;
        if (hostSlot >= 0)
        {
            var arr = Members;
            arr.Set(hostSlot, default);
        }

        RPC_PromoteHost(next);
        return false;
    }

    /// <summary>Tell the promoted member to claim StateAuthority over the party.</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PromoteHost([RpcTarget] PlayerRef target)
    {
        _claimAuthorityOnArrival = true;
        Object.RequestStateAuthority();
    }

    /// <summary>Fusion callback: our StateAuthority over this object changed.</summary>
    public void StateAuthorityChanged()
    {
        if (HasStateAuthority && _claimAuthorityOnArrival)
        {
            _claimAuthorityOnArrival = false;
            // HostPlayer was already set to us by the outgoing host; ensure our slot
            // is marked ready as the new host and normalise state.
            HostPlayer = Runner.LocalPlayer;
            int slot = FindSlot(Runner.LocalPlayer);
            if (slot >= 0)
            {
                var m = Members[slot];
                m.Ready = true;
                var arr = Members;
                arr.Set(slot, m);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Start dungeon (host)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Host-only: publish the selected dungeon so every member's panel shows it (24.2).
    /// Called when the host opens/changes the dungeon selection, before Start.
    /// </summary>
    public void HostSetDungeon(int configId, string sceneName, string dungeonName)
    {
        if (!HasStateAuthority) return;
        DungeonConfigId = configId;
        DungeonSceneName = sceneName ?? string.Empty;
        DungeonName = dungeonName ?? string.Empty;
    }

    /// <summary>
    /// Host-only: begin dungeon entry. Validates the gate, publishes the target
    /// dungeon to all clients, flips State→Loading and raises
    /// <see cref="OnDungeonStartRequested"/> on the host so DungeonManager can run the
    /// Enter API (Step 5). Ignored if the gate is not satisfied.
    /// </summary>
    public void HostStartDungeon(int configId, string sceneName)
    {
        if (!HasStateAuthority) return;
        if (!CanStartDungeon) return;

        DungeonConfigId = configId;
        DungeonSceneName = sceneName ?? string.Empty;
        State = PartyState.Loading;

        OnDungeonStartRequested?.Invoke(configId, sceneName);
    }

    /// <summary>
    /// Host-only: publish the backend dungeon session id (from the Enter API) to the
    /// party and mark the party in-dungeon so members transition without re-entering.
    /// </summary>
    public void HostPublishDungeonSession(int sessionId)
    {
        if (!HasStateAuthority) return;
        DungeonSessionId = sessionId;
        State = PartyState.InDungeon;
    }

    /// <summary>
    /// Host-only: revert from <see cref="PartyState.Loading"/> back to
    /// <see cref="PartyState.Lobby"/> when the dungeon entry pipeline fails (e.g.
    /// backend rejected the Enter call). Resets the pending session id so the host
    /// can retry without orphaning the party in Loading.
    /// </summary>
    public void RevertToLobby()
    {
        if (!HasStateAuthority) return;
        if (State != PartyState.Loading) return;
        DungeonSessionId = 0;
        State = PartyState.Lobby;
    }
}
