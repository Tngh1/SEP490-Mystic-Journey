using UnityEngine;
using MysticJourney.Core.Utilities;

namespace MysticJourney.Core.Services
{
    // Initializes a new default instance of the GameStateService class.
    public class GameStateService
    {
        // Executes core business logic for instance.
        public static GameStateService Instance { get; private set; } = new();

        // Executes core business logic for has character.
        public bool HasCharacter { get; set; }
        // Executes core business logic for player profile id.
        public int PlayerProfileId { get; set; }
        // Executes core business logic for player level.
        public int PlayerLevel { get; set; } = 1;
        // Executes core business logic for player name.
        public string PlayerName { get; set; }
        // Executes core business logic for player class.
        public string PlayerClass { get; set; }
        // Executes core business logic for equipped skin id.
        public int EquippedSkinId { get; set; }

        // Executes core business logic for avatar url.
        public string AvatarUrl { get; set; }

        // Executes core business logic for current map name.
        public string CurrentMapName { get; set; }
        // Executes core business logic for highest unlocked map id.
        public int HighestUnlockedMapId { get; set; } = MapProgressionRules.FirstMapId;
        // Executes core business logic for last position.
        public Vector3 LastPosition { get; set; }
        // Executes core business logic for corruption level.
        public float CorruptionLevel { get; set; }

        // Executes core business logic for reset.
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

        // Executes core business logic for load from player prefs.
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

        // Executes core business logic for save to player prefs.
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
