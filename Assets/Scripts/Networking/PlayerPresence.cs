using System;
using System.Collections.Generic;
using Fusion;
using MysticJourney.API.Models.Response;
using UnityEngine;

/// <summary>
/// A lightweight networked "business card" that every client spawns for ITSELF when
/// it joins the shared SOCIAL LOBBY room (<see cref="PhotonManager.PartyPhase.Lobby"/>).
/// It carries the player's public identity (profile id / name / class / level) so other
/// online players can be discovered, and it doubles as the per-player INVITE MAILBOX:
/// a party host looks up a friend's presence in <see cref="Registry"/> and calls
/// <see cref="RPC_ReceiveInvite"/> on it to deliver a party invitation.
///
/// Why invites travel on Presence and not on PartyLobby: a Fusion RPC can only reach
/// peers already inside the same room. Because the party is created INSIDE the social
/// room (not in a separate room), an idle friend already owns a Presence here, so the
/// host can reach them directly — no backend, no reconnect.
///
/// Authority (Shared Mode): each client owns State + Input authority over its OWN
/// presence object. Invite RPCs are routed to <see cref="RpcTargets.InputAuthority"/>
/// so only the invited player receives them.
///
/// This object holds NO gameplay logic and NO avatar — it exists only for the
/// pre-dungeon social/party phase. Avatars are still spawned exclusively in the
/// dungeon phase by <see cref="PhotonManager.OnPlayerJoined"/>.
/// </summary>
public class PlayerPresence : NetworkBehaviour
{
    /// <summary>The local client's own presence, or null before it spawns.</summary>
    public static PlayerPresence Local { get; private set; }

    /// <summary>All live presences in the social room, keyed by ProfileId.</summary>
    private static readonly Dictionary<int, PlayerPresence> _registry = new();

    /// <summary>Read-only view of every online presence, keyed by ProfileId.</summary>
    public static IReadOnlyDictionary<int, PlayerPresence> Registry => _registry;

    /// <summary>
    /// Raised on the INVITED client when a host sends a party invite. Args: inviter
    /// profile id, inviter display name. The invite popup UI subscribes to this — no
    /// business logic lives in the UI itself.
    /// </summary>
    public static event Action<int, string> OnInviteReceived;

    /// <summary>
    /// Raised on EVERY client when a world-chat message arrives over Fusion. Static
    /// because the message travels on the sender's presence, so each listener would
    /// otherwise have to subscribe to every peer's object.
    /// </summary>
    public static event Action<WorldChatMessageResponse> OnWorldMessageReceived;

    [Networked, OnChangedRender(nameof(OnProfileIdChanged))]
    public int ProfileId { get; set; }

    [Networked] public NetworkString<_32> DisplayName { get; set; }
    [Networked] public int PlayerClass { get; set; }
    [Networked] public int Level { get; set; }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Local = this;
        }

        // Data is set by the spawning client via the Spawn() initializer (so it is
        // present before first replication). Register whatever we already know; the
        // OnChangedRender guard below re-registers proxies whose ProfileId arrives late.
        RegisterSelf();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unregister();
        if (Local == this) Local = null;
    }

    private void OnProfileIdChanged() => RegisterSelf();

    // ─────────────────────────────────────────────────────────────────────────
    // Identity
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Copy the local player's identity from <see cref="WorldState"/> onto the networked
    /// fields. Callable at any time (not just from the Spawn initializer): the presence is
    /// spawned during Main-scene bootstrap while PlayerSpawner is still hydrating WorldState
    /// from the API, so a spawn-only write froze whatever was known at boot — usually
    /// profile id 0 (unreachable for invites) and a stale class / level 1 in the roster.
    /// No-op unless this client owns the object.
    /// </summary>
    public void ApplyWorldState()
    {
        if (!HasStateAuthority) return;

        string className = WorldState.PlayerClass ?? "Knight";
        if (!Enum.TryParse<CharacterClass>(className, true, out var parsed))
            parsed = CharacterClass.Knight;

        // Never overwrite a good profile id with 0: RegisterSelf ignores non-positive ids,
        // which would leave a stale registry entry pointing at this presence.
        if (WorldState.PlayerProfileId > 0) ProfileId = WorldState.PlayerProfileId;
        DisplayName = WorldState.PlayerName ?? "Player";
        PlayerClass = (int)parsed;
        Level = Mathf.Max(1, WorldState.PlayerLevel);

        // Register explicitly rather than relying on OnProfileIdChanged: a presence that
        // spawned before the profile id was known must land in the registry the moment it
        // arrives, or the local player stays invisible to inviters.
        RegisterSelf();
    }

    /// <summary>Re-publish the local presence's identity. Safe no-op when offline.</summary>
    public static void RefreshLocal() => Local?.ApplyWorldState();

    // ─────────────────────────────────────────────────────────────────────────
    // Registry
    // ─────────────────────────────────────────────────────────────────────────

    private void RegisterSelf()
    {
        if (ProfileId <= 0) return;

        // LAST valid presence for this profile wins. On a fast reconnect the remote
        // peers can still hold the previous (about-to-despawn) proxy for this profile;
        // a first-wins rule would reject the live presence and, with no retry, leave
        // the reconnected player permanently unreachable for invites.
        _registry[ProfileId] = this;
    }

    private void Unregister()
    {
        if (ProfileId > 0 && _registry.TryGetValue(ProfileId, out var p) && p == this)
            _registry.Remove(ProfileId);
    }

    /// <summary>
    /// Look up an online player's presence by profile id, or null if that player is
    /// offline / not currently in the social room. Prunes stale (destroyed) entries.
    /// </summary>
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

    /// <summary>
    /// Look up the presence owned by a given peer, or null if that peer has none.
    /// Used to attribute an incoming RPC to a real player instead of trusting the
    /// identity the sender put in the payload.
    /// </summary>
    public static PlayerPresence FindByPlayer(PlayerRef player)
    {
        foreach (var p in _registry.Values)
        {
            if (p == null || p.Object == null) continue;
            if (p.Object.InputAuthority == player) return p;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Invite mailbox
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called BY THE HOST on a target friend's presence object to deliver an invite.
    /// Fusion routes it to the target (the InputAuthority owner), which raises
    /// <see cref="OnInviteReceived"/> for the popup UI to render Accept / Decline.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
    public void RPC_ReceiveInvite(int inviterProfileId, NetworkString<_32> inviterName)
    {
        OnInviteReceived?.Invoke(inviterProfileId, inviterName.Value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // World chat relay
    //
    // World chat rides the presence object instead of a separate networked relay:
    // presence is already spawned per player in the social room, already carries the
    // identity the chat list needs, and — because a player can only send on the object
    // it owns (RpcSources.InputAuthority) — the sender cannot be forged at all.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Minimum seconds between two accepted messages from the same player.</summary>
    private const float MinChatInterval = 0.5f;

    /// <summary>Must match the NetworkString width used by <see cref="RPC_WorldMessage"/>.</summary>
    private const int MaxChatChars = 256;

    private float _lastChatAccepted = float.NegativeInfinity;

    /// <summary>True when a world-chat message can actually be relayed over Photon.</summary>
    public static bool WorldChatReady =>
        Local != null && Local.Runner != null && Local.Runner.IsRunning;

    /// <summary>
    /// Relay a freshly-sent world message to everyone in the social room. Returns false
    /// when offline, in which case callers keep their HTTP history polling fallback.
    /// </summary>
    public static bool BroadcastWorldMessage(WorldChatMessageResponse message)
    {
        if (message == null || message.ChatMessageId <= 0) return false;
        if (!WorldChatReady) return false;

        string body = message.Content ?? string.Empty;
        if (body.Length > MaxChatChars) body = body.Substring(0, MaxChatChars);

        Local.RPC_WorldMessage(message.ChatMessageId, body);
        return true;
    }

    /// <summary>
    /// Sent by a player ON ITS OWN presence and received by every peer. Sender identity
    /// is read from this object's networked fields, never from the payload.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_WorldMessage(int chatMessageId, NetworkString<_256> content)
    {
        string body = content.ToString();
        if (chatMessageId <= 0 || string.IsNullOrWhiteSpace(body)) return;

        // Flood guard: the send cooldown lives in the UI, so a modified client could
        // otherwise push a message every tick to every peer in the room.
        float now = Time.unscaledTime;
        if (now - _lastChatAccepted < MinChatInterval) return;
        _lastChatAccepted = now;

        OnWorldMessageReceived?.Invoke(new WorldChatMessageResponse
        {
            ChatMessageId = chatMessageId,
            SenderId = ProfileId,
            SenderName = DisplayName.ToString(),
            Channel = "World",
            Content = body,
            // A just-sent message is never reported or hidden; the server owns both, and
            // trusting the sender for them let a client hide/flag other players' text.
            IsReported = false,
            IsHidden = false,
            SentAt = DateTime.UtcNow.ToString("o")
        });
    }
}
