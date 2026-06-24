using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [SerializeField] private GameObject damagePopupPrefab; // Kéo prefab ở Bước 1 vào đây

    private void Awake()
    {
        Instance = this;
    }

    public void Create(Vector3 position, int damageAmount, bool isCritical, bool isPlayer = false)
    {
        // Sinh ra chữ nhích lên một chút so với vị trí gốc
        Vector3 spawnPos = position + new Vector3(Random.Range(-0.5f, 0.5f), 1f, 0);
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

        DamagePopup damagePopup = popup.GetComponent<DamagePopup>();
        damagePopup.Setup(damageAmount, isCritical, isPlayer);
    }
}