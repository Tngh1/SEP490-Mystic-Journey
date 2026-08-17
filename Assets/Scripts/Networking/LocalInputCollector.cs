using Fusion;
using UnityEngine;

// Executes mono behaviour operation.
public class LocalInputCollector : MonoBehaviour
{
    private GameplayInputProvider _provider;

    // Executes collect operation.
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

    // Executes resolve local provider operation.
    private GameplayInputProvider ResolveLocalProvider()
    {
        if (_provider != null) return _provider;

        var localPlayer = PlayerMovement.Instance;
        if (localPlayer != null)
            _provider = localPlayer.GetComponent<GameplayInputProvider>();

        return _provider;
    }

    // Executes read aim world operation.
    private Vector2 ReadAimWorld(GameplayInputProvider provider)
    {
        if (provider != null)
        {
            var world = provider.PointerWorldPosition;
            if (world.HasValue) return world.Value;
        }
        return Vector2.zero;
    }

    // Executes build buttons operation.
    private NetworkButtons BuildButtons(GameplayInputProvider provider)
    {
        var buttons = default(NetworkButtons);
        if (provider == null) return buttons;

        buttons.Set(InputButtons.Attack, provider.AttackHeld);
        buttons.Set(InputButtons.Skill1, provider.Skill1Held);
        buttons.Set(InputButtons.Skill2, provider.Skill2Held);
        buttons.Set(InputButtons.Skill3, provider.Skill3Held);

        return buttons;
    }
}
