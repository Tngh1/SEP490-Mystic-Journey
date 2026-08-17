using UnityEngine;

// Executes core business logic for mono behaviour.
public class DamagePopupManager : MonoBehaviour
{
    // Executes core business logic for instance.
    public static DamagePopupManager Instance { get; private set; }

    [SerializeField] private GameObject damagePopupPrefab;

    // Initializes internal component caches and dependencies for DamagePopupManager upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        Instance = this;
    }

    // Executes core business logic for create text.
    public void CreateText(Vector3 position, string text, Color color, float fontSize = 4.5f)
    {
        if (!SettingsService.Instance.ShowDamageNumbers)
            return;

        // Randomize the eligible candidates before selecting this gameplay result.
        Vector3 spawnPos = position + new Vector3(Random.Range(-0.3f, 0.3f), 1.2f, 0);
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

        DamagePopup damagePopup = popup.GetComponent<DamagePopup>();
        if (damagePopup != null)
        {
            damagePopup.SetupText(text, color, fontSize);
        }
    }

    // Executes core business logic for create.
    public void Create(Vector3 position, int damageAmount, bool isCritical, bool isPlayer = false, bool isHeal = false)
    {
        if (!SettingsService.Instance.ShowDamageNumbers)
            return;

        // Randomize the eligible candidates before selecting this gameplay result.
        Vector3 spawnPos = position + new Vector3(Random.Range(-0.5f, 0.5f), 1f, 0);
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

        DamagePopup damagePopup = popup.GetComponent<DamagePopup>();
        damagePopup.Setup(damageAmount, isCritical, isPlayer, isHeal);
    }
}
