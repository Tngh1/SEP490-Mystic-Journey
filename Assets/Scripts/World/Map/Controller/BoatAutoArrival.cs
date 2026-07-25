using UnityEngine;
using System.Collections;

public class BoatAutoArrival : MonoBehaviour
{
    [Tooltip("Thời gian chờ trước khi tự động rời thuyền (giây)")]
    public float delaySeconds = 3f;

    [Tooltip("Vị trí trên bờ để đưa người chơi lên (nếu có)")]
    public Transform shoreSpawnPoint;

    [Tooltip("Sự kiện xảy ra khi vừa rời thuyền (VD: Bật lại Sprite người chơi, tắt object thuyền...)")]
    public UnityEngine.Events.UnityEvent onLeaveBoat;

    private IEnumerator Start()
    {
        // CHỈ thực hiện tự động đưa lên bờ nếu lữ khách vừa mới đi thuyền sang map này!
        int justUsedBoat = PlayerPrefs.GetInt("JustUsedBoat", 0);
        if (justUsedBoat != 1)
        {
            yield break;
        }

        // Reset lại cờ sau khi đã ghi nhận
        PlayerPrefs.SetInt("JustUsedBoat", 0);
        PlayerPrefs.Save();

        // Chờ delaySeconds sau khi load scene xong
        yield return new WaitForSeconds(delaySeconds);

        LeaveBoat();
    }

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
            // Tách thuyền khỏi người chơi nếu thuyền đang gắn vào người chơi
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

            // Hiện lại tất cả Sprite người chơi
            var playerSprites = player.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sp in playerSprites)
            {
                sp.enabled = true;
            }

            // Dịch chuyển người chơi lên vị trí bờ (nếu có chỉ định)
            if (shoreSpawnPoint != null)
            {
                player.transform.position = shoreSpawnPoint.position;
            }

            if (onLeaveBoat != null) onLeaveBoat.Invoke();

            Debug.Log("[Boat] Đã rời thuyền và lên bờ tự động.");
        }
    }
}
