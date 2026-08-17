using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Executes mono behaviour operation.
public class MonsterHealthUI : MonoBehaviour
{
    [SerializeField] private EnemyEntity enemyEntity;
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI hpText;

    // Initializes internal component caches and dependencies for MonsterHealthUI upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
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

    // Performs startup initialization for MonsterHealthUI on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (enemyEntity != null)
        {
            UpdateHealthUI(enemyEntity.CurrentHealth, enemyEntity.MaxHealth);
        }
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        if (enemyEntity != null)
        {
            enemyEntity.OnHealthChanged += UpdateHealthUI;
            enemyEntity.OnDeath += HideUI;
        }
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        if (enemyEntity != null)
        {
            enemyEntity.OnHealthChanged -= UpdateHealthUI;
            enemyEntity.OnDeath -= HideUI;
        }
    }

    // Executes late update operation.
    private void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
    }

    // Executes update health ui operation.
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

    // Executes hide ui operation.
    private void HideUI(object sender, System.EventArgs e)
    {
        gameObject.SetActive(false);
    }
}
