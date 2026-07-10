using UnityEngine;

public class CharacterFactory : MonoBehaviour
{
    [Header("Visual Prefabs (base body)")]
    [SerializeField] private GameObject archerPrefab;
    [SerializeField] private GameObject knightPrefab;
    [SerializeField] private GameObject magePrefab;

    [Header("Animator Controllers (optional override)")]
    [Tooltip("If set, the instantiated visual's Animator will be assigned this controller. " +
             "Leave null to use whatever controller the prefab ships with.")]
    [SerializeField] private RuntimeAnimatorController archerController;
    [SerializeField] private RuntimeAnimatorController knightController;
    [SerializeField] private RuntimeAnimatorController mageController;

    [Header("Sorting")]
    [Tooltip("Sorting layer applied to the SpriteRenderer on the instantiated visual. " +
             "Must match a layer defined in Edit > Project Settings > Tags and Layers.")]
    [SerializeField] private string sortingLayerName = "Characters";

    [Tooltip("Sorting order baseline. Actual order = this + a small offset if the visual has multiple renderers.")]
    [SerializeField] private int baseSortingOrder = 0;

    /// <summary>
    /// Instantiate the visual for the given class under <paramref name="parent"/>.
    /// </summary>
    public GameObject Create(CharacterClass characterClass, Transform parent)
    {
        GameObject prefab = ResolvePrefab(characterClass);
        if (prefab == null)
        {
            Debug.LogWarning($"[CharacterFactory] No prefab assigned for class {characterClass}. " +
                             "Returning a placeholder empty GameObject so networking can continue.");
            var placeholder = new GameObject($"Visual_{characterClass}_Placeholder");
            if (parent != null)
                placeholder.transform.SetParent(parent, worldPositionStays: false);
            return placeholder;
        }

        var instance = Instantiate(prefab, parent);
        instance.name = $"Visual_{characterClass}";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        ConfigureSorting(instance);
        ConfigureAnimator(instance, characterClass);

        return instance;
    }

    private GameObject ResolvePrefab(CharacterClass characterClass)
    {
        switch (characterClass)
        {
            case CharacterClass.Archer: return archerPrefab;
            case CharacterClass.Knight: return knightPrefab;
            case CharacterClass.Mage:   return magePrefab;
            default: return null;
        }
    }

    private RuntimeAnimatorController ResolveController(CharacterClass characterClass)
    {
        switch (characterClass)
        {
            case CharacterClass.Archer: return archerController;
            case CharacterClass.Knight: return knightController;
            case CharacterClass.Mage:   return mageController;
            default: return null;
        }
    }

    private void ConfigureSorting(GameObject instance)
    {
        if (string.IsNullOrEmpty(sortingLayerName)) return;

        var renderers = instance.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingLayerName = sortingLayerName;
            renderers[i].sortingOrder = baseSortingOrder + i;
        }
    }

    private void ConfigureAnimator(GameObject instance, CharacterClass characterClass)
    {
        RuntimeAnimatorController controller = ResolveController(characterClass);
        if (controller == null) return;

        var animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
        if (animators.Length == 0)
        {
            Debug.LogWarning($"[CharacterFactory] Visual for {characterClass} has no Animator; " +
                             $"controller '{controller.name}' will not be assigned.");
            return;
        }
        foreach (var a in animators)
        {
            a.runtimeAnimatorController = controller;
        }
    }
}