using System;
using System.Collections.Generic;
using Fusion;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;
using UnityEngine;

// Executes network behaviour operation.
public class PlayerPresence : NetworkBehaviour
{
    // Executes local operation.
    public static PlayerPresence Local { get; private set; }

    private static readonly Dictionary<int, PlayerPresence> _registry = new();

    // Executes registry operation.
    public static IReadOnlyDictionary<int, PlayerPresence> Registry => _registry;

    public static event Action<int, string> OnInviteReceived;

    public static event Action<WorldChatMessageResponse> OnWorldMessageReceived;

    public static event Action<string> OnPartyDisbanded;

    // Executes profile id operation.
    [Networked, OnChangedRender(nameof(OnProfileIdChanged))]
    public int ProfileId { get; set; }

    [Networked] public NetworkString<_32> DisplayName { get; set; }
    [Networked] public int PlayerClass { get; set; }
    [Networked] public int Level { get; set; }
    [Networked] public int HighestUnlockedMapId { get; set; }
    [Networked] public NetworkBool IsInDungeon { get; set; }

    // Registers networked presence in global lookup table and hooks map progression listeners.
    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Local = this; // Cache local player presence singleton
            WorldRuntimeEvents.MapCompleted += HandleMapCompleted; // Update highest map unlocks
        }

        RegisterSelf(); // Add to presence registry
    }

    // Cleans up local presence reference and removes from registry when despawned.
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this)
            WorldRuntimeEvents.MapCompleted -= HandleMapCompleted;
        Unregister(); // Remove from registry
        if (Local == this) Local = null;
    }

    // Triggers registry re-indexing when networked ProfileId updates.
    private void OnProfileIdChanged() => RegisterSelf();


    // Synchronizes local WorldState cache (Name, Class, Level, Unlocked Maps) into networked properties.
    public void ApplyWorldState()
    {
        if (!HasStateAuthority) return;

        string className = WorldState.PlayerClass ?? "Knight";
        if (!Enum.TryParse<CharacterClass>(className, true, out var parsed))
            parsed = CharacterClass.Knight;

        if (WorldState.PlayerProfileId > 0) ProfileId = WorldState.PlayerProfileId; // Networked profile ID
        DisplayName = WorldState.PlayerName ?? "Player"; // Networked display name
        PlayerClass = (int)parsed; // Networked player class enum
        Level = Mathf.Max(1, WorldState.PlayerLevel); // Networked character level
        HighestUnlockedMapId = Mathf.Max(
            MapProgressionRules.FirstMapId,
            WorldState.HighestUnlockedMapId); // Networked map progression
        IsInDungeon = IsLocalDungeonActive(); // Networked live dungeon availability

        RegisterSelf();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        bool inDungeon = IsLocalDungeonActive();
        if ((bool)IsInDungeon != inDungeon)
            IsInDungeon = inDungeon;
    }

    private static bool IsLocalDungeonActive()
    {
        return (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon) ||
               (PhotonManager.Instance != null && PhotonManager.Instance.IsDungeonSession);
    }



    // Refreshes local presence network properties.
    public static void RefreshLocal() => Local?.ApplyWorldState();

    // Updates highest unlocked map progression when completing a storyline quest.
    private void HandleMapCompleted(int claimedQuestId)
    {
        int unlockedMapId = MapProgressionRules.GetMapUnlockedByQuest(claimedQuestId);
        if (unlockedMapId <= WorldState.HighestUnlockedMapId) return;

        WorldState.HighestUnlockedMapId = unlockedMapId;
        HighestUnlockedMapId = unlockedMapId;
    }


    // Adds this presence instance to the global profile ID dictionary.
    private void RegisterSelf()
    {
        if (ProfileId <= 0) return;

        _registry[ProfileId] = this;
    }

    // Removes this presence instance from the registry.
    private void Unregister()
    {
        if (ProfileId > 0 && _registry.TryGetValue(ProfileId, out var p) && p == this)
            _registry.Remove(ProfileId);
    }

    // Finds presence instance by player profile ID.
    public static PlayerPresence Find(int profileId)
    {
        if (!_registry.TryGetValue(profileId, out var p)) return null;
        if (p == null)
        {
            _registry.Remove(profileId);
            return null;
        }
        return p;
    }

    // Finds presence instance by Fusion PlayerRef network handle.
    public static PlayerPresence FindByPlayer(PlayerRef player)
    {
        foreach (var p in _registry.Values)
        {
            if (p == null || p.Object == null) continue;
            if (p.Object.InputAuthority == player) return p;
        }
        return null;
    }


    [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
    // Receives incoming party invitation RPC and fires OnInviteReceived event for UI popup.
    public void RPC_ReceiveInvite(int inviterProfileId, NetworkString<_32> inviterName)
    {
        OnInviteReceived?.Invoke(inviterProfileId, inviterName.Value); // Display party invitation popup
    }

    [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
    // Receives RPC from party leader indicating the team has been disbanded.
    public void RPC_PartyDisbanded(NetworkString<_32> hostName)
    {
        OnPartyDisbanded?.Invoke(hostName.Value); // Notify party UI listeners
    }


    private const float MinChatInterval = 0.5f;

    private float _lastChatAccepted = float.NegativeInfinity;

    // Checks if local Photon runner is connected and active for chat broadcast.
    public static bool WorldChatReady =>
        Local != null && Local.Runner != null && Local.Runner.IsRunning;

    // Broadcasts an outgoing chat message across all connected network peers via Photon RPC.
    public static bool BroadcastWorldMessage(WorldChatMessageResponse message)
    {
        if (message == null || message.ChatMessageId <= 0) return false;
        if (!WorldChatReady) return false; // Guard against offline status

        Local.RPC_WorldMessage(
            message.ChatMessageId,
            NetworkChatText.ClampUtf8(message.Content, NetworkChatText.MaxContentBytes)); // Truncate content within byte limits
        return true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    // Broadcasts world chat message payload from sender to all peers and invokes local UI event.
    public void RPC_WorldMessage(int chatMessageId, string content)
    {
        string body = content ?? string.Empty;
        if (chatMessageId <= 0 || string.IsNullOrWhiteSpace(body)) return;

        float now = Time.unscaledTime;
        if (now - _lastChatAccepted < MinChatInterval) return; // Anti-spam interval throttling
        _lastChatAccepted = now;

        OnWorldMessageReceived?.Invoke(new WorldChatMessageResponse
        {
            ChatMessageId = chatMessageId,
            SenderId = ProfileId, // Sender ProfileId
            SenderName = DisplayName.ToString(), // Sender display name
            Channel = "World",
            Content = body,
            IsReported = false,
            IsHidden = false,
            SentAt = DateTime.UtcNow.ToString("o") // ISO-8601 timestamp
        });
    }
}
