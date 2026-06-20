using UnityEngine;

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
        public string CurrentMapName { get; set; }
        public Vector3 LastPosition { get; set; }

        public void Reset()
        {
            HasCharacter = false;
            PlayerProfileId = 0;
            PlayerLevel = 1;
            PlayerName = null;
            PlayerClass = "Knight";
            CurrentMapName = "ElfForest";
            LastPosition = new Vector3(11.9f, 17.8f, 0f);
        }

        public void LoadFromPlayerPrefs()
        {
            PlayerProfileId = PlayerPrefs.GetInt("mj_player_profile_id", 0);
            PlayerLevel = PlayerPrefs.GetInt("mj_player_level", 1);
            PlayerName = PlayerPrefs.GetString("mj_user_name", string.Empty);
            PlayerClass = PlayerPrefs.GetString("mj_player_class", "Knight");
            CurrentMapName = PlayerPrefs.GetString("mj_last_map", "ElfForest");
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
            PlayerPrefs.SetString("mj_player_class", PlayerClass ?? "Knight");
            PlayerPrefs.SetString("mj_last_map", CurrentMapName ?? "ElfForest");
            PlayerPrefs.SetFloat("mj_pos_x", LastPosition.x);
            PlayerPrefs.SetFloat("mj_pos_y", LastPosition.y);
            PlayerPrefs.Save();
        }
    }
}
