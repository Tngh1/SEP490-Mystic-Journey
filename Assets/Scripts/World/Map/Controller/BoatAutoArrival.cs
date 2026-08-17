using UnityEngine;
using System.Collections;

// Executes mono behaviour operation.
public class BoatAutoArrival : MonoBehaviour
{
    [Tooltip("Thời gian chờ trước khi tự động rời thuyền (giây)")]
    public float delaySeconds = 3f;

    [Tooltip("Vị trí trên bờ để đưa người chơi lên (nếu có)")]
    public Transform shoreSpawnPoint;

    [Tooltip("Sự kiện xảy ra khi vừa rời thuyền (VD: Bật lại Sprite người chơi, tắt object thuyền...)")]
    public UnityEngine.Events.UnityEvent onLeaveBoat;

    // Performs startup initialization for BoatAutoArrival on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private IEnumerator Start()
    {
        int justUsedBoat = PlayerPrefs.GetInt("JustUsedBoat", 0);
        if (justUsedBoat != 1)
        {
            yield break;
        }

        PlayerPrefs.SetInt("JustUsedBoat", 0);
        PlayerPrefs.Save();

        yield return new WaitForSeconds(delaySeconds);

        LeaveBoat();
    }

    // Executes leave boat operation.
    public void LeaveBoat()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null) player = pm.gameObject;
        }

        if (player != null)
        {
            if (this.transform.parent == player.transform)
            {
                this.transform.SetParent(null);
            }
            else if (player.transform.parent == this.transform)
            {
                player.transform.SetParent(null);
            }

            foreach (Transform child in player.transform)
            {
                if (child.name.IndexOf("Boat", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    child.name.IndexOf("Thuyen", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    child.SetParent(null);
                }
            }

            var playerSprites = player.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sp in playerSprites)
            {
                sp.enabled = true;
            }

            if (shoreSpawnPoint != null)
            {
                player.transform.position = shoreSpawnPoint.position;
            }

            if (onLeaveBoat != null) onLeaveBoat.Invoke();

            Debug.Log("[Boat] Đã rời thuyền và lên bờ tự động.");
        }
    }
}
