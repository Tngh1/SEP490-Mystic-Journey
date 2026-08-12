using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public struct SkinPrefabMap
{
    [Tooltip("Tên Skin (để dễ nhìn trong Editor)")]
    public string skinName;

    [Tooltip("Class của nhân vật (Knight, Mage, Archer)")]
    public CharacterClass characterClass;

    [Tooltip("Skin ID trên Database")]
    public int skinId;



    public GameObject prefab;

    [Tooltip("Optional UI preview. If empty, the first suitable SpriteRenderer sprite from the prefab is used.")]
    public Sprite previewSprite;
    public RuntimeAnimatorController controller;
}

[CreateAssetMenu(fileName = "SkinDatabase", menuName = "Mystic Journey/Skin Database")]
public class SkinDatabaseSO : ScriptableObject
{
    [Tooltip("Map SkinId to a specific prefab and animator controller.")]
    public List<SkinPrefabMap> skinPrefabs = new List<SkinPrefabMap>();

    private Dictionary<int, SkinPrefabMap> _lookup;

    public static SkinDatabaseSO LoadDefault()
    {
        var database = Resources.Load<SkinDatabaseSO>("SkinDatabase");
        if (database != null)
            return database;

#if UNITY_EDITOR
        var guids = AssetDatabase.FindAssets("t:SkinDatabaseSO SkinDatabase");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            database = AssetDatabase.LoadAssetAtPath<SkinDatabaseSO>(path);
            if (database != null)
                return database;
        }
#endif

        return null;
    }

    public bool TryGetSkinData(int skinId, out SkinPrefabMap skinData)
    {
        EnsureLookup();
        return _lookup.TryGetValue(skinId, out skinData);
    }

    public SkinPrefabMap? GetSkinData(int skinId)
    {
        return TryGetSkinData(skinId, out var skinData) ? skinData : null;
    }

    public bool TryGetPreviewSprite(int skinId, out Sprite previewSprite)
    {
        previewSprite = null;

        if (!TryGetSkinData(skinId, out var skinData))
            return false;

        previewSprite = ResolvePreviewSprite(skinData);
        return previewSprite != null;
    }

    public Sprite GetPreviewSprite(int skinId)
    {
        return TryGetPreviewSprite(skinId, out var previewSprite) ? previewSprite : null;
    }

    public Sprite GetDefaultPreviewSprite(CharacterClass characterClass)
    {
        if (skinPrefabs == null) return null;
        for (var i = 0; i < skinPrefabs.Count; i++)
        {
            var skin = skinPrefabs[i];
            if (skin.characterClass != characterClass) continue;
            var preview = ResolvePreviewSprite(skin);
            if (preview != null) return preview;
        }
        return null;
    }

    private static Sprite ResolvePreviewSprite(SkinPrefabMap skinData)
    {
        if (skinData.previewSprite != null)
            return skinData.previewSprite;

        if (skinData.prefab == null)
            return null;

        var renderers = skinData.prefab.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        Sprite bestSprite = null;
        var bestArea = -1f;
        var bestEnabledScore = -1;

        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
                continue;
                
            // Skip common non-visual or utility sprites
            string objName = renderer.gameObject.name.ToLower();
            if (objName.Contains("shadow") || objName.Contains("hitbox") || objName.Contains("bound") || objName.Contains("collider"))
                continue;

            var sprite = renderer.sprite;
            var rect = sprite.rect;
            var area = Mathf.Max(1f, rect.width * rect.height);
            var enabledScore = renderer.enabled ? 1 : 0;

            if (enabledScore > bestEnabledScore || (enabledScore == bestEnabledScore && area > bestArea))
            {
                bestSprite = sprite;
                bestArea = area;
                bestEnabledScore = enabledScore;
            }
        }

        return bestSprite;
    }


    private void OnEnable()
    {
        RebuildLookup();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildLookup();
    }
#endif

    private void EnsureLookup()
    {
        if (_lookup == null)
            RebuildLookup();
    }

    private void RebuildLookup()
    {
        _lookup = new Dictionary<int, SkinPrefabMap>();

        if (skinPrefabs == null)
            return;

        foreach (var skinMap in skinPrefabs)
        {
            if (skinMap.skinId <= 0)
                continue;

            if (_lookup.ContainsKey(skinMap.skinId))
            {
                Debug.LogWarning($"[SkinDatabaseSO] Duplicate skinId={skinMap.skinId} on '{name}'. Keeping the first entry.", this);
                continue;
            }

            _lookup.Add(skinMap.skinId, skinMap);
        }
    }
}
