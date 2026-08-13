using UnityEngine;

/// <summary>
/// Keeps enemy body colliders from transferring physics motion to player bodies.
/// Combat uses explicit range checks/physics queries, so disabling body contacts does
/// not change melee targeting or projectile collision layers.
/// </summary>
public static class MonsterPlayerCollisionPolicy
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        int monsterLayer = LayerMask.NameToLayer("Monster");
        int playerLayer = LayerMask.NameToLayer("Player");

        if (monsterLayer < 0 || playerLayer < 0)
        {
            Debug.LogWarning("[Physics] Monster or Player layer is missing; collision policy was not applied.");
            return;
        }

        Physics2D.IgnoreLayerCollision(monsterLayer, playerLayer, true);
    }
}
