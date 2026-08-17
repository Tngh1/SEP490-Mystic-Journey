using System;
using System.Collections.Generic;
using Fusion;
using MysticJourney.API.Models.Response;
using UnityEngine;

// Executes i state authority changed operation.
public class PartyLobby : NetworkBehaviour, IStateAuthorityChanged
{
    public const int MaxMembers = 4;

    // Executes party state operation.
    public enum PartyState { Lobby, Loading, InDungeon }

    private static readonly Dictionary<NetworkId, PartyLobby> _all = new();

    // Executes all operation.
    public static IReadOnlyCollection<PartyLobby> All => _all.Values;

    // Executes local operation.
    public static PartyLobby Local { get; private set; }

    public static event Action OnLocalPartyChanged;

    // Executes i network struct operation.
    public struct Member : INetworkStruct
    {
        public PlayerRef Player;
        public int ProfileId;
        public NetworkString<_32> Name;
        // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
        public int PlayerClass;
        public int Level;
        public NetworkBool Ready;
        public int SkinId;

        // Executes is occupied operation.
        public bool IsOccupied => Player != default;
    }

    // Executes members operation.
    [Networked, Capacity(MaxMembers)]
    public NetworkArray<Member> Members => default;

    [Networked] public PlayerRef HostPlayer { get; set; }

    [Networked] public PartyState State { get; set; }

    [Networked] public int PendingInviteCount { get; set; }

    [Networked] public int DungeonConfigId { get; set; }
    [Networked] public int DungeonSessionId { get; set; }
    [Networked] public NetworkString<_32> DungeonSceneName { get; set; }
    [Networked] public NetworkString<_32> DungeonName { get; set; }

    public event Action OnRosterChanged;

    public event Action<PartyState> OnPartyStateChanged;

    public event Action<int, string> OnDungeonStartRequested;

    public event Action<PartyChatMessageResponse> PartyMessageReceived;

    private ChangeDetector _changes;

    // Registers networked party lobby instance and seats the host player in slot 0.
    public override void Spawned()
    {
        _all[Object.Id] = this; // Register lobby in lookup table
        _changes = GetChangeDetector(ChangeDetector.Source.SnapshotFrom); // Setup state change detector

        if (HasStateAuthority)
        {
            HostPlayer = Object.InputAuthority; // Designate host player authority
            State = PartyState.Lobby; // Set initial state
            SeatSelf(0, ready: true); // Seat host in slot 0 with ready status
        }

        RefreshLocalMembership(); // Evaluate whether local client is seated in this party
        OnRosterChanged?.Invoke(); // Trigger UI redraw
    }

    // Cleans up party registration and fires OnLocalPartyChanged if local player left.
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _all.Remove(Object.Id); // Unregister lobby
        if (Local == this)
        {
            Local = null;
            OnLocalPartyChanged?.Invoke(); // Notify local party change
        }
    }

    // Inspects networked snapshot changes per frame and dispatches granular UI update events.
    public override void Render()
    {
        foreach (var change in _changes.DetectChanges(this))
        {
            if (change == nameof(State))
                OnPartyStateChanged?.Invoke(State); // Notify dungeon loading or transition
            else
                OnRosterChanged?.Invoke(); // Notify member joining, leaving, or ready toggle
        }

        RefreshLocalMembership(); // Re-verify local seat assignment
    }

    // Updates local singleton reference if local player was added or removed from this party.
    private void RefreshLocalMembership()
    {
        if (Runner == null) return;

        bool localSeated = FindSlot(Runner.LocalPlayer) >= 0; // Check if local player occupies any slot

        if (localSeated && Local != this)
        {
            Local = this; // Assign as active local party
            OnLocalPartyChanged?.Invoke();
        }
        else if (!localSeated && Local == this)
        {
            Local = null; // Detach active party
            OnLocalPartyChanged?.Invoke();
        }
    }


    // Fills party member metadata (Class, Level, SkinId, Ready flag) for a designated slot.
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
            SkinId = WorldState.EquippedSkinId,
        });
    }


    // Executes is local host operation.
    public bool IsLocalHost =>
        Object != null && Runner != null && HostPlayer == Runner.LocalPlayer;

    // Executes host profile id operation.
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

    // Executes host display name operation.
    public string HostDisplayName
    {
        get
        {
            for (int i = 0; i < MaxMembers; i++)
            {
                var m = Members[i];
                if (m.IsOccupied && m.Player == HostPlayer) return m.Name.Value;
            }
            return string.Empty;
        }
    }

    // Executes member count operation.
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

    // Executes ready count operation.
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

    // Executes all ready operation.
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

    // Executes can start dungeon operation.
    // Validates input parameters against null or empty values.
    // Evaluates conditions and returns a boolean result.
    public bool CanStartDungeon =>
        PartyLifecycleRules.CanStartDungeon(true, (int)State, MemberCount, ReadyCount, PendingInviteCount);

    // Executes is local member operation.
    // Validates input parameters against null or empty values.
    // Evaluates conditions and returns a boolean result.
    public bool IsLocalMember =>
        Runner != null && FindSlot(Runner.LocalPlayer) >= 0;

    // Executes broadcast party message operation.
    // Validates input parameters against null or empty values.
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
        if (!PartyLifecycleRules.CanUsePartyChat(IsLocalMember, HasMemberProfileId(senderId), senderId))
        {
            return false;
        }

        string senderName = !string.IsNullOrWhiteSpace(WorldState.PlayerName)
            ? WorldState.PlayerName
            : message.SenderName;

        string networkSenderName = NetworkChatText.ClampUtf8(senderName, NetworkChatText.MaxSenderNameBytes);
        string networkContent = NetworkChatText.ClampUtf8(message.Content, NetworkChatText.MaxContentBytes);
        string networkSentAt = NetworkChatText.ClampUtf8(
            string.IsNullOrWhiteSpace(message.SentAt) ? DateTime.UtcNow.ToString("O") : message.SentAt,
            NetworkChatText.MaxTimestampBytes);

        RPC_PartyMessageReceived(senderId, networkSenderName, networkContent, networkSentAt);
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    // Process rpc party message received using sender id, sender name, content, and sent at; it updates local membership and guards invalid or unavailable states.
    private void RPC_PartyMessageReceived(
        int senderId,
        string senderName,
        string content,
        string sentAt)
    {
        RefreshLocalMembership();

        if (!PartyLifecycleRules.CanUsePartyChat(Local == this, HasMemberProfileId(senderId), senderId))
        {
            return;
        }

        PartyMessageReceived?.Invoke(new PartyChatMessageResponse
        {
            SenderId = senderId,
            SenderName = senderName ?? string.Empty,
            Content = content ?? string.Empty,
            Channel = "Party",
            SentAt = sentAt ?? string.Empty
        });
    }

    // Executes has member profile id operation.
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

    // Executes find slot operation.
    private int FindSlot(PlayerRef player)
    {
        for (int i = 0; i < MaxMembers; i++)
            if (Members[i].Player == player) return i;
        return -1;
    }

    // Executes first free slot operation.
    private int FirstFreeSlot()
    {
        for (int i = 0; i < MaxMembers; i++)
            if (!Members[i].IsOccupied) return i;
        return -1;
    }


    // Executes register pending invite operation.
    public void RegisterPendingInvite()
    {
        if (!HasStateAuthority) return;
        PendingInviteCount++;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Executes rpc_invite resolved operation.
    public void RPC_InviteResolved()
    {
        if (!HasStateAuthority) return;
        if (PendingInviteCount > 0) PendingInviteCount--;
    }


    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Executes rpc_join operation.
    public void RPC_Join(PlayerRef player, int profileId, NetworkString<_32> name, int playerClass, int level, int skinId)
    {
        if (!HasStateAuthority) return;
        if (!PartyLifecycleRules.CanJoin((int)State, MemberCount, FindSlot(player) >= 0)) return;

        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        int slot = FirstFreeSlot();
        if (slot < 0) return;

        var arr = Members;
        arr.Set(slot, new Member
        {
            Player = player,
            ProfileId = profileId,
            Name = name,
            PlayerClass = playerClass,
            Level = level,
            Ready = false,
            SkinId = skinId,
        });

        if (PendingInviteCount > 0) PendingInviteCount--;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Executes rpc_set ready operation.
    public void RPC_SetReady(PlayerRef player, NetworkBool ready)
    {
        if (!HasStateAuthority) return;
        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        int slot = FindSlot(player);
        if (!PartyLifecycleRules.CanChangeReady(slot >= 0, player == HostPlayer)) return;

        var m = Members[slot];
        m.Ready = ready;
        var arr = Members;
        arr.Set(slot, m);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Executes rpc_kick operation.
    public void RPC_Kick(PlayerRef requester, PlayerRef target)
    {
        if (!HasStateAuthority) return;
        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        int slot = FindSlot(target);
        if (!PartyLifecycleRules.CanKick(requester == HostPlayer, slot >= 0, target == HostPlayer)) return;
        RemoveMember(target);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Executes rpc_leave operation.
    public void RPC_Leave(PlayerRef player)
    {
        if (!HasStateAuthority) return;
        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        int slot = FindSlot(player);
        if (!PartyLifecycleRules.CanLeave(slot >= 0, player == HostPlayer)) return;
        RemoveMember(player);
    }

    // Executes handle network player left operation.
    // Evaluates conditions and returns a boolean result.
    public void HandleNetworkPlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        if (player == HostPlayer) return;
        RemoveMember(player);
    }

    // Executes remove member operation.
    // Evaluates conditions and returns a boolean result.
    private bool RemoveMember(PlayerRef player)
    {
        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        int slot = FindSlot(player);
        if (slot < 0) return false;
        var arr = Members;
        arr.Set(slot, default);
        return true;
    }


    // Executes leave as host operation.
    public void LeaveAsHost()
    {
        if (!HasStateAuthority) return;

        NotifyMembersDisbanded();

        if (Runner != null && Object != null)
            Runner.Despawn(Object);
    }

    // Executes notify members disbanded operation.
    private void NotifyMembersDisbanded()
    {
        string hostName = HostDisplayName;

        for (int i = 0; i < MaxMembers; i++)
        {
            var m = Members[i];
            if (!m.IsOccupied) continue;
            if (m.Player == HostPlayer) continue;

            var presence = PlayerPresence.Find(m.ProfileId) ?? PlayerPresence.FindByPlayer(m.Player);
            presence?.RPC_PartyDisbanded(hostName);
        }
    }

    // Executes state authority changed operation.
    public void StateAuthorityChanged()
    {
    }


    // Executes host set dungeon operation.
    public void HostSetDungeon(int configId, string sceneName, string dungeonName)
    {
        if (!HasStateAuthority) return;
        DungeonConfigId = configId;
        DungeonSceneName = sceneName ?? string.Empty;
        DungeonName = dungeonName ?? string.Empty;
    }

    // Executes host start dungeon operation.
    public void HostStartDungeon(int configId, string sceneName)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[PartyLobby] Start rejected: local peer has no state authority.");
            return;
        }
        if (!CanStartDungeon)
        {
            Debug.LogWarning($"[PartyLobby] Start rejected: state={State}, members={MemberCount}, " +
                             $"ready={ReadyCount}, pendingInvites={PendingInviteCount}.");
            return;
        }

        DungeonConfigId = configId;
        DungeonSceneName = sceneName ?? string.Empty;
        PendingInviteCount = 0;
        State = PartyState.Loading;

        OnDungeonStartRequested?.Invoke(configId, sceneName);
    }

    // Executes host publish dungeon session operation.
    public void HostPublishDungeonSession(int sessionId)
    {
        if (!HasStateAuthority) return;
        DungeonSessionId = sessionId;
        State = PartyState.InDungeon;
    }

    // Executes revert to lobby operation.
    public void RevertToLobby()
    {
        if (!HasStateAuthority) return;
        if (State != PartyState.Loading) return;
        DungeonSessionId = 0;
        State = PartyState.Lobby;
    }
}
