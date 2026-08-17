using UnityEngine;
using TMPro;

// Executes mono behaviour operation.
public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float disappearTimer;
    private Color textColor;
    private Vector3 moveVector;

    // Initializes internal component caches and dependencies for DamagePopup upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        ApplySilverFont();
    }

    // Executes apply silver font operation.
    private void ApplySilverFont()
    {
        if (textMesh != null && SilverFontResolver.Font != null)
            textMesh.font = SilverFontResolver.Font;
    }

    // Executes setup text operation.
    public void SetupText(string text, Color color, float fontSize = 4.5f)
    {
        if (textMesh == null) textMesh = GetComponent<TextMeshPro>();
        ApplySilverFont();
        textMesh.text = text;
        textMesh.color = color;
        textMesh.fontSize = fontSize;

        textColor = textMesh.color;
        disappearTimer = 1f;
        moveVector = new Vector3(0, 2.5f, 0) * 1.5f;
    }

    // Executes setup operation.
    public void Setup(int damageAmount, bool isCritical, bool isPlayerTakingDamage = false, bool isHeal = false)
    {
        ApplySilverFont();
        textMesh.text = isHeal ? $"+{damageAmount}" : damageAmount.ToString();

        if (isHeal)
        {
            textMesh.color = Color.green;
            textMesh.fontSize = 5f;
        }
        else if (isPlayerTakingDamage)
        {
            textMesh.color = Color.red;
            textMesh.fontSize = 4f;
        }
        else
        {
            if (isCritical)
            {
                textMesh.color = Color.yellow;
                textMesh.fontSize = 5f;
            }
            else
            {
                textMesh.color = Color.white;
                textMesh.fontSize = 4f;
            }
        }

        textColor = textMesh.color;
        disappearTimer = 1f;
        moveVector = new Vector3(0, 2f, 0) * 1.5f;
    }

    // Per-frame update loop for DamagePopup.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        transform.position += moveVector * Time.deltaTime;

        moveVector -= moveVector * 8f * Time.deltaTime;

        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            float disappearSpeed = 3f;
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;

            if (textColor.a < 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
