using Fusion;
using UnityEngine;

/// <summary>
/// Packages the local player's input into a <see cref="NetworkInputData"/>
/// snapshot for Fusion, once per tick from <c>PhotonManager.OnInput</c>.
///
/// It reads NOTHING from the Input System itself. All input comes from the local
/// player's <see cref="GameplayInputProvider"/> — the single source of truth —
/// so multiplayer input honours the exact same rebindings as offline play. This
/// class is now purely a "translate provider → network struct" adapter (SRP).
/// </summary>
public class LocalInputCollector : MonoBehaviour
{
    // Cached provider for the local input-authority player. Re-resolved if the
    // player it belonged to was destroyed (e.g. respawn, scene change).
    private GameplayInputProvider _provider;

    /// <summary>
    /// Build a fresh <see cref="NetworkInputData"/> snapshot from current input state.
    /// Called once per Fusion tick from PhotonManager.OnInput.
    /// </summary>
    public NetworkInputData Collect()
    {
        var provider = ResolveLocalProvider();

        var data = new NetworkInputData
        {
            Move = provider != null ? provider.Move : Vector2.zero,
            AimWorldPosition = ReadAimWorld(provider),
            Buttons = BuildButtons(provider),
        };

        return data;
    }

    /// <summary>
    /// Find the local player's input provider. The local input-authority player
    /// is tracked by <see cref="PlayerMovement.Instance"/>; the provider lives on
    /// the same GameObject. Cached and re-resolved when stale.
    /// </summary>
    private GameplayInputProvider ResolveLocalProvider()
    {
        if (_provider != null) return _provider;

        var localPlayer = PlayerMovement.Instance;
        if (localPlayer != null)
            _provider = localPlayer.GetComponent<GameplayInputProvider>();

        return _provider;
    }

    private Vector2 ReadAimWorld(GameplayInputProvider provider)
    {
        if (provider != null)
        {
            var world = provider.PointerWorldPosition;
            if (world.HasValue) return world.Value;
        }
        return Vector2.zero;
    }

    private NetworkButtons BuildButtons(GameplayInputProvider provider)
    {
        var buttons = default(NetworkButtons);
        if (provider == null) return buttons;

        // Only simulated actions travel over the network. Interact / Inventory /
        // Map are client-local and polled directly on the local player, so they
        // are intentionally absent here.
        buttons.Set(InputButtons.Attack, provider.AttackHeld);
        buttons.Set(InputButtons.Skill1, provider.Skill1Held);
        buttons.Set(InputButtons.Skill2, provider.Skill2Held);
        buttons.Set(InputButtons.Skill3, provider.Skill3Held);

        return buttons;
    }
}
