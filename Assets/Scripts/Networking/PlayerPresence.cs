using System;
using System.Collections.Generic;
using Fusion;
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
    /// <see cref="PlayerRef"/>, inviter profile id, inviter display name. The invite
    /// popup UI subscribes to this — no business logic lives in the UI itself.
    /// </summary>
    public static event Action<PlayerRef, int, string> OnInviteReceived;

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
    // Registry
    // ─────────────────────────────────────────────────────────────────────────

    private void RegisterSelf()
    {
        if (ProfileId <= 0) return;
        if (_registry.TryGetValue(ProfileId, out var existing) && existing != null && existing != this)
            return; // first valid presence for this profile wins
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

    // ─────────────────────────────────────────────────────────────────────────
    // Invite mailbox
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called BY THE HOST on a target friend's presence object to deliver an invite.
    /// Fusion routes it to the target (the InputAuthority owner), which raises
    /// <see cref="OnInviteReceived"/> for the popup UI to render Accept / Decline.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
    public void RPC_ReceiveInvite(PlayerRef inviter, int inviterProfileId, NetworkString<_32> inviterName)
    {
        OnInviteReceived?.Invoke(inviter, inviterProfileId, inviterName.Value);
    }
}
