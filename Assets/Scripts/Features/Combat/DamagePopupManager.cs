using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [SerializeField] private GameObject damagePopupPrefab; // Kéo prefab ở Bước 1 vào đây

    private void Awake()
    {
        Instance = this;
    }

    public void CreateText(Vector3 position, string text, Color color, float fontSize = 4.5f)
    {
        if (!SettingsService.Instance.ShowDamageNumbers)
            return;

        Vector3 spawnPos = position + new Vector3(Random.Range(-0.3f, 0.3f), 1.2f, 0);
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

        DamagePopup damagePopup = popup.GetComponent<DamagePopup>();
        if (damagePopup != null)
        {
            damagePopup.SetupText(text, color, fontSize);
        }
    }

    public void Create(Vector3 position, int damageAmount, bool isCritical, bool isPlayer = false, bool isHeal = false)
    {
        if (!SettingsService.Instance.ShowDamageNumbers)
            return;

        Vector3 spawnPos = position + new Vector3(Random.Range(-0.5f, 0.5f), 1f, 0);
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

        DamagePopup damagePopup = popup.GetComponent<DamagePopup>();
        damagePopup.Setup(damageAmount, isCritical, isPlayer, isHeal);
    }
}