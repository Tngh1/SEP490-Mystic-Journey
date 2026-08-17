using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Executes scriptable object operation.
[CreateAssetMenu(fileName = "ClassAvatarDatabase", menuName = "Mystic Journey/Class Avatar Database")]
public class ClassAvatarDatabaseSO : ScriptableObject
{
    // Executes class art operation.
    [System.Serializable]
    public struct ClassArt
    {
        // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
        public CharacterClass characterClass;
        public Sprite flag;
        public Sprite nameplate;
    }

    [Tooltip("One entry per class (Knight / Mage / Archer). Drag the flag + name sprites for each.")]
    public List<ClassArt> classes = new List<ClassArt>();

    private Dictionary<CharacterClass, ClassArt> _lookup;

    // Executes load default operation.
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

    // Executes get flag operation.
    public Sprite GetFlag(CharacterClass characterClass)
    {
        EnsureLookup();
        return _lookup.TryGetValue(characterClass, out var a) ? a.flag : null;
    }

    // Executes get nameplate operation.
    public Sprite GetNameplate(CharacterClass characterClass)
    {
        EnsureLookup();
        return _lookup.TryGetValue(characterClass, out var a) ? a.nameplate : null;
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable() => RebuildLookup();
#if UNITY_EDITOR
    // Executes on validate operation.
    private void OnValidate() => RebuildLookup();
#endif

    // Executes ensure lookup operation.
    private void EnsureLookup()
    {
        if (_lookup == null) RebuildLookup();
    }

    // Executes rebuild lookup operation.
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
