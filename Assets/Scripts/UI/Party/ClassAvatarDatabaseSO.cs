using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Maps a <see cref="CharacterClass"/> to the art shown in a party slot: the class
/// <see cref="ClassArt.flag"/> banner and the <see cref="ClassArt.nameplate"/> label
/// behind the player's name. The party roster only replicates a player's CLASS, so a
/// slot swaps to that class's art when a member sits down.
///
/// Place the asset in a <c>Resources</c> folder named "ClassAvatarDatabase" so it can be
/// loaded at runtime without an Inspector reference (mirrors SkinDatabaseSO.LoadDefault).
/// </summary>
[CreateAssetMenu(fileName = "ClassAvatarDatabase", menuName = "Mystic Journey/Class Avatar Database")]
public class ClassAvatarDatabaseSO : ScriptableObject
{
    [System.Serializable]
    public struct ClassArt
    {
        public CharacterClass characterClass;
        public Sprite flag;      // class banner (the "Flag" image on a slot)
        public Sprite nameplate; // class name label (the "Name" image on a slot)
    }

    [Tooltip("One entry per class (Knight / Mage / Archer). Drag the flag + name sprites for each.")]
    public List<ClassArt> classes = new List<ClassArt>();

    private Dictionary<CharacterClass, ClassArt> _lookup;

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

    /// <summary>Class banner sprite (Flag), or null when unmapped.</summary>
    public Sprite GetFlag(CharacterClass characterClass)
    {
        EnsureLookup();
        return _lookup.TryGetValue(characterClass, out var a) ? a.flag : null;
    }

    /// <summary>Class name-label sprite (Name plate), or null when unmapped.</summary>
    public Sprite GetNameplate(CharacterClass characterClass)
    {
        EnsureLookup();
        return _lookup.TryGetValue(characterClass, out var a) ? a.nameplate : null;
    }

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
        _lookup = new Dictionary<CharacterClass, ClassArt>();
        if (classes == null) return;
        foreach (var a in classes)
        {
            if (!_lookup.ContainsKey(a.characterClass))
                _lookup[a.characterClass] = a;
        }
    }
}
