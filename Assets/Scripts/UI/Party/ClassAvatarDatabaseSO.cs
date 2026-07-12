using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Maps a <see cref="CharacterClass"/> to the avatar sprite shown in party slots.
/// Kept separate from <see cref="SkinDatabaseSO"/> (which maps by skinId and loads full
/// gameplay prefabs) because the party roster only replicates a player's CLASS, and a
/// slot only needs a lightweight portrait — no prefab instantiation.
///
/// Place the asset in a <c>Resources</c> folder named "ClassAvatarDatabase" so it can be
/// loaded at runtime without an Inspector reference (mirrors SkinDatabaseSO.LoadDefault).
/// </summary>
[CreateAssetMenu(fileName = "ClassAvatarDatabase", menuName = "Mystic Journey/Class Avatar Database")]
public class ClassAvatarDatabaseSO : ScriptableObject
{
    [System.Serializable]
    public struct ClassAvatar
    {
        public CharacterClass characterClass;
        public Sprite avatar;
    }

    [Tooltip("One entry per class (Knight / Mage / Archer). Drag the portrait sprite for each.")]
    public List<ClassAvatar> avatars = new List<ClassAvatar>();

    [Tooltip("Optional fallback sprite when a class has no mapped avatar.")]
    public Sprite fallbackAvatar;

    private Dictionary<CharacterClass, Sprite> _lookup;

    public static ClassAvatarDatabaseSO LoadDefault()
    {
        var db = Resources.Load<ClassAvatarDatabaseSO>("ClassAvatarDatabase");
        if (db != null) return db;

#if UNITY_EDITOR
        var guids = AssetDatabase.FindAssets("t:ClassAvatarDatabaseSO");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            db = AssetDatabase.LoadAssetAtPath<ClassAvatarDatabaseSO>(path);
            if (db != null) return db;
        }
#endif
        return null;
    }

    /// <summary>Sprite for the given class, or the fallback (may be null).</summary>
    public Sprite GetSprite(CharacterClass characterClass)
    {
        EnsureLookup();
        return _lookup.TryGetValue(characterClass, out var s) && s != null ? s : fallbackAvatar;
    }

    public Sprite GetSprite(int classId) => GetSprite((CharacterClass)classId);

    private void OnEnable() => RebuildLookup();
#if UNITY_EDITOR
    private void OnValidate() => RebuildLookup();
#endif

    private void EnsureLookup()
    {
        if (_lookup == null) RebuildLookup();
    }

    private void RebuildLookup()
    {
        _lookup = new Dictionary<CharacterClass, Sprite>();
        if (avatars == null) return;
        foreach (var a in avatars)
        {
            if (!_lookup.ContainsKey(a.characterClass))
                _lookup[a.characterClass] = a.avatar;
        }
    }
}
