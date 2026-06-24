using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MonsterHealthUI : MonoBehaviour
{
    [SerializeField] private EnemyEntity enemyEntity;
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI hpText;

    private void Awake()
    {
        if (enemyEntity == null)
        {
            enemyEntity = GetComponentInParent<EnemyEntity>();
            if (enemyEntity == null)
            {
                enemyEntity = GetComponent<EnemyEntity>();
            }
        }
    }

    private void Start()
    {
        if (enemyEntity != null)
        {
            UpdateHealthUI(enemyEntity.CurrentHealth, enemyEntity.MaxHealth);
        }
    }

    private void OnEnable()
    {
        if (enemyEntity != null)
        {
            enemyEntity.OnHealthChanged += UpdateHealthUI;
            // Đăng ký thêm sự kiện khi chết
            enemyEntity.OnDeath += HideUI;
        }
    }

    private void OnDisable()
    {
        if (enemyEntity != null)
        {
            enemyEntity.OnHealthChanged -= UpdateHealthUI;
            // Hủy đăng ký sự kiện khi chết
            enemyEntity.OnDeath -= HideUI;
        }
    }

    private void UpdateHealthUI(int currentHp, int maxHp)
    {
        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = maxHp > 0 ? (float)currentHp / maxHp : 0;
        }

        if (hpText != null)
        {
            hpText.text = $"{currentHp} / {maxHp}";
        }
    }

    // Hàm mới: Ẩn nguyên cái Canvas khi quái ngỏm
    private void HideUI(object sender, System.EventArgs e)
    {
        gameObject.SetActive(false);
    }
}