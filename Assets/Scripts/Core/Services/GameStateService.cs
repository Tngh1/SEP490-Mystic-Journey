using UnityEngine;
using MysticJourney.Core.Utilities;

namespace MysticJourney.Core.Services
{
    public class GameStateService
    {
        public static GameStateService Instance { get; private set; } = new();

        public bool HasCharacter { get; set; }
        public int PlayerProfileId { get; set; }
        public int PlayerLevel { get; set; } = 1;
        public string PlayerName { get; set; }
        public string PlayerClass { get; set; }
        public int EquippedSkinId { get; set; }

        /// <summary>
        /// Resource name of the player's profile avatar (e.g. "avatar_3"), matching a
        /// sprite under Resources/Avatars. Kept here — not only on the HUD — because
        /// NetworkPlayer replicates it so party members can show each other's avatar.
        /// </summary>
        public string AvatarUrl { get; set; }

        public string CurrentMapName { get; set; }
        public int HighestUnlockedMapId { get; set; } = MapProgressionRules.FirstMapId;
        public Vector3 LastPosition { get; set; }
        public float CorruptionLevel { get; set; }

        public void Reset()
        {
            HasCharacter = false;
            PlayerProfileId = 0;
            PlayerLevel = 1;
            PlayerName = null;
            PlayerClass = string.Empty;
            EquippedSkinId = 0;
            AvatarUrl = null;
            CurrentMapName = "ElfForest";
            HighestUnlockedMapId = MapProgressionRules.FirstMapId;
            LastPosition = new Vector3(11.9f, 17.8f, 0f);
            CorruptionLevel = 0f;
        }

        public void LoadFromPlayerPrefs()
        {
            PlayerProfileId = PlayerPrefs.GetInt("mj_player_profile_id", 0);
            PlayerLevel = PlayerPrefs.GetInt("mj_player_level", 1);
            PlayerName = PlayerPrefs.GetString("mj_user_name", string.Empty);
            PlayerClass = PlayerPrefs.GetString("mj_player_class", string.Empty);
            EquippedSkinId = PlayerPrefs.GetInt("mj_equipped_skin_id", 0);
            AvatarUrl = PlayerPrefs.GetString("mj_avatar_url", string.Empty);
            CurrentMapName = PlayerPrefs.GetString("mj_last_map", "ElfForest");
            HighestUnlockedMapId = Mathf.Max(
                MapProgressionRules.FirstMapId,
                MapProgressionRules.GetMapId(CurrentMapName));
            LastPosition = new Vector3(
                PlayerPrefs.GetFloat("mj_pos_x", 11.9f),
                PlayerPrefs.GetFloat("mj_pos_y", 17.8f),
                0f
            );
        }

        public void SaveToPlayerPrefs()
        {
            PlayerPrefs.SetInt("mj_player_profile_id", PlayerProfileId);
            PlayerPrefs.SetInt("mj_player_level", PlayerLevel);
            PlayerPrefs.SetString("mj_user_name", PlayerName ?? string.Empty);
            PlayerPrefs.SetString("mj_player_class", PlayerClass ?? string.Empty);
            PlayerPrefs.SetInt("mj_equipped_skin_id", EquippedSkinId);
            PlayerPrefs.SetString("mj_avatar_url", AvatarUrl ?? string.Empty);
            PlayerPrefs.SetString("mj_last_map", CurrentMapName ?? "ElfForest");
            PlayerPrefs.SetFloat("mj_pos_x", LastPosition.x);
            PlayerPrefs.SetFloat("mj_pos_y", LastPosition.y);
            PlayerPrefs.Save();
        }
    }
}
