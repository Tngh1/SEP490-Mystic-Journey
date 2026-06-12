using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameBootstrap : MonoBehaviour
{
    private IEnumerator Start()
    {
        Debug.Log("=== GAME BOOTSTRAP START ===");

        // ?? 1. GI? L?P LOGIN (load data t? DB)
        MockLoginData();

        // ?? 2. Load Scene Main (UI + EventSystem)
        yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);

        // ?? 3. B?o hi?m n?u login không có map
        if (string.IsNullOrEmpty(WorldState.CurrentMapName))
        {
            Debug.LogWarning("Không có map t? login -> dùng map m?c ??nh");
            WorldState.CurrentMapName = "ElfForest";
        }

        // ?? 4. Load Map t? data login
        yield return SceneManager.LoadSceneAsync(WorldState.CurrentMapName, LoadSceneMode.Additive);

        // ?? 5. Set Main làm active scene (r?t nên có)
        Scene mainScene = SceneManager.GetSceneByName("Main");
        SceneManager.SetActiveScene(mainScene);

        Debug.Log("=== LOAD DONE ===");

        // ?? 6. Xóa bootstrap
        Destroy(gameObject);
    }

    private void MockLoginData()
    {
        PlayerProfileDto profile = new PlayerProfileDto
        {
            LastMapName = "ElfForest",
            PositionX = 125.5f,
            PositionY = 50.2f
        };

        WorldState.CurrentMapName = profile.LastMapName;
        WorldState.LastPosition = new Vector3(profile.PositionX, profile.PositionY, 0f);

        Debug.Log("[Mock Login] ?ã load data t? DB gi?");
    }
}