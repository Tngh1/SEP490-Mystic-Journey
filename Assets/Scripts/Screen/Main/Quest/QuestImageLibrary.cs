using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Mystic Journey/UI Image Library", fileName = "QuestImageLibrary")]
public class QuestImageLibrary : ScriptableObject
{
    [Serializable]
    public class ImageEntry
    {
        public string id;
        public Sprite sprite;
    }

    [SerializeField] private List<ImageEntry> images = new List<ImageEntry>();

    public Sprite GetSprite(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        for (var i = 0; i < images.Count; i++)
        {
            var entry = images[i];
            if (entry != null && string.Equals(entry.id, id, StringComparison.OrdinalIgnoreCase))
                return entry.sprite;
        }

        return null;
    }
}
