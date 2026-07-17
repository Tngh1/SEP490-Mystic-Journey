using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float disappearTimer;
    private Color textColor;
    private Vector3 moveVector;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    public void Setup(int damageAmount, bool isCritical, bool isPlayerTakingDamage = false, bool isHeal = false)
    {
        textMesh.text = isHeal ? $"+{damageAmount}" : damageAmount.ToString();

        if (isHeal)
        {
            textMesh.color = Color.green;
            textMesh.fontSize = 5f;
        }
        else if (isPlayerTakingDamage)
        {
            // Nếu người chơi bị đánh, hiện màu Đỏ để dễ phân biệt
            textMesh.color = Color.red;
            textMesh.fontSize = 4f;
        }
        else
        {
            // Quái bị đánh
            if (isCritical)
            {
                textMesh.color = Color.yellow; // Chí mạng màu vàng
                textMesh.fontSize = 5f; // Chữ to hơn
            }
            else
            {
                textMesh.color = Color.white; // Bình thường màu trắng
                textMesh.fontSize = 4f;
            }
        }

        textColor = textMesh.color;
        disappearTimer = 1f; // Chữ tồn tại trong 1 giây
        moveVector = new Vector3(0, 2f, 0) * 1.5f; // Tốc độ bay lên
    }

    private void Update()
    {
        // Làm chữ bay lên
        transform.position += moveVector * Time.deltaTime;

        // Làm chữ chậm dần lại
        moveVector -= moveVector * 8f * Time.deltaTime;

        // Làm mờ dần chữ
        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            float disappearSpeed = 3f;
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;

            if (textColor.a < 0)
            {
                Destroy(gameObject); // Xóa chữ khi đã mờ hết
            }
        }
    }
}