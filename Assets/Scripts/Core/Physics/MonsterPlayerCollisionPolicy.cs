using UnityEngine;

// Initializes a new default instance of the MonsterPlayerCollisionPolicy class.
public static class MonsterPlayerCollisionPolicy
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    // Executes apply operation.
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
