using UnityEngine;

public class CharacterFactory : MonoBehaviour
{
    [Header("Visual Prefabs (base body)")]
    [SerializeField] private GameObject archerPrefab;
    [SerializeField] private GameObject knightPrefab;
    [SerializeField] private GameObject magePrefab;

    [Header("Animator Controllers (optional override)")]
    [Tooltip("If set, the instantiated visual's Animator will be assigned this controller. Leave null to use whatever controller the prefab ships with.")]
    [SerializeField] private RuntimeAnimatorController archerController;
    [SerializeField] private RuntimeAnimatorController knightController;
    [SerializeField] private RuntimeAnimatorController mageController;

    [Header("Skin Overrides")]
    [Tooltip("The global SkinDatabaseSO to map SkinId to prefabs and controllers.")]
    [SerializeField] private SkinDatabaseSO skinDatabase;

    [Header("Sorting")]
    [Tooltip("Sorting layer applied to the SpriteRenderer on the instantiated visual. Must match a layer defined in Project Settings > Tags and Layers.")]
    [SerializeField] private string sortingLayerName = "Characters";

    [Tooltip("Sorting order baseline. Actual order = this + a small offset if the visual has multiple renderers.")]
    [SerializeField] private int baseSortingOrder = 0;

    private bool _loggedMissingSkinDatabase;

    private void Awake()
    {
        EnsureSkinDatabase();
    }

    public GameObject Create(int skinId, CharacterClass characterClass, Transform parent)
    {
        GameObject prefab = ResolvePrefab(skinId, characterClass);
        if (prefab == null)
        {
            Debug.LogWarning($"[CharacterFactory] No prefab assigned for class {characterClass}. Returning a placeholder empty GameObject so networking can continue.");
            var placeholder = new GameObject($"Visual_{characterClass}_Placeholder");
            if (parent != null)
                placeholder.transform.SetParent(parent, worldPositionStays: false);
            return placeholder;
        }

        var instance = Instantiate(prefab, parent);
        instance.name = skinId > 0 ? $"Visual_{characterClass}_Skin_{skinId}" : $"Visual_{characterClass}";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        StripGameplayComponents(instance);
        ConfigureSorting(instance);
        ConfigureAnimator(instance, skinId, characterClass);

        return instance;
    }

    private SkinDatabaseSO EnsureSkinDatabase()
    {
        if (skinDatabase == null)
            skinDatabase = SkinDatabaseSO.LoadDefault();

        if (skinDatabase == null && !_loggedMissingSkinDatabase)
        {
            _loggedMissingSkinDatabase = true;
            Debug.LogWarning("[CharacterFactory] SkinDatabaseSO is not assigned and no default SkinDatabase could be loaded. Skin ids will fall back to class prefabs.", this);
        }

        return skinDatabase;
    }

    private static void StripGameplayComponents(GameObject visual)
    {
        DestroyAll<PlayerWorldInteractor>(visual);
        DestroyAll<GameplayInputProvider>(visual);
        DestroyAll<PlayerCombat>(visual);
        DestroyAll<PlayerMovement>(visual);
        DestroyAll<PlayerEntity>(visual);
        DestroyAll<UnityEngine.InputSystem.PlayerInput>(visual);
        DestroyAll<Fusion.NetworkObject>(visual);
        DestroyAll<Rigidbody2D>(visual);
        DestroyAll<Collider2D>(visual);
    }

    private static void DestroyAll<T>(GameObject root) where T : Component
    {
        var found = root.GetComponentsInChildren<T>(includeInactive: true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                Destroy(found[i]);
        }
    }

    private GameObject ResolvePrefab(int skinId, CharacterClass characterClass)
    {
        var database = EnsureSkinDatabase();
        if (database != null && database.TryGetSkinData(skinId, out var skinData))
        {
            if (skinData.prefab != null)
                return skinData.prefab;

            Debug.LogWarning($"[CharacterFactory] SkinId={skinId} is mapped in '{database.name}' but has no prefab. Falling back to class prefab.", database);
        }

        switch (characterClass)
        {
            case CharacterClass.Archer: return archerPrefab;
            case CharacterClass.Knight: return knightPrefab;
            case CharacterClass.Mage: return magePrefab;
            default: return null;
        }
    }

    private RuntimeAnimatorController ResolveController(int skinId, CharacterClass characterClass)
    {
        var database = EnsureSkinDatabase();
        if (database != null && database.TryGetSkinData(skinId, out var skinData) && skinData.controller != null)
            return skinData.controller;

        switch (characterClass)
        {
            case CharacterClass.Archer: return archerController;
            case CharacterClass.Knight: return knightController;
            case CharacterClass.Mage: return mageController;
            default: return null;
        }
    }

    private void ConfigureSorting(GameObject instance)
    {
        if (string.IsNullOrEmpty(sortingLayerName))
            return;

        var renderers = instance.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingLayerName = sortingLayerName;
            renderers[i].sortingOrder = baseSortingOrder + i;
        }
    }

    private void ConfigureAnimator(GameObject instance, int skinId, CharacterClass characterClass)
    {
        RuntimeAnimatorController controller = ResolveController(skinId, characterClass);
        if (controller == null)
            return;

        var animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
        if (animators.Length == 0)
        {
            Debug.LogWarning($"[CharacterFactory] Visual for {characterClass} has no Animator; controller '{controller.name}' will not be assigned.");
            return;
        }

        RuntimeAnimatorController defaultClassController = GetClassController(characterClass);

        foreach (var animator in animators)
        {
            animator.runtimeAnimatorController = controller;

            // Fallback nếu Controller của Skin rỗng / thiếu Parameter (MoveX, MoveY, Speed, v.v.)
            if (animator.parameterCount == 0 && defaultClassController != null && controller != defaultClassController)
            {
                Debug.LogWarning($"[CharacterFactory] Controller '{controller.name}' on skin {skinId} has 0 parameters. Overriding with class controller '{defaultClassController.name}'.");
                animator.runtimeAnimatorController = defaultClassController;
            }
        }
    }

    private RuntimeAnimatorController GetClassController(CharacterClass characterClass)
    {
        switch (characterClass)
        {
            case CharacterClass.Archer: return archerController;
            case CharacterClass.Knight: return knightController;
            case CharacterClass.Mage: return mageController;
            default: return null;
        }
    }
}
