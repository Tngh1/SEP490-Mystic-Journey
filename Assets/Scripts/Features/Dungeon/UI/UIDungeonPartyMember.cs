using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Executes mono behaviour operation.
public class UIDungeonPartyMember : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private UIBuffPanel buffPanel;

    private NetworkPlayer _targetPlayer;
    private int _lastHp = -1;
    private int _lastMaxHp = -1;
    private int _lastLevel = -1;
    private string _lastName;
    private string _lastAvatarUrl;
    private bool _buffPanelBound;

    // Executes init operation.
    public void Init(NetworkPlayer target)
    {
        _targetPlayer = target;

        _lastLevel     = -1;
        _lastName      = null;
        _lastAvatarUrl = null;
        _lastHp        = -1;
        _lastMaxHp     = -1;
        _buffPanelBound = false;

        if (nameText  != null) nameText.text  = "...";
        if (levelText != null) levelText.text = "-";

        if (_targetPlayer != null)
        {
            RefreshIdentity();
            TryBindBuffPanel();
            UpdateHpUI(true);
        }
    }

    // Per-frame update loop for UIDungeonPartyMember.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (_targetPlayer == null || _targetPlayer.Object == null || !_targetPlayer.Object.IsValid)
        {
            Destroy(gameObject);
            return;
        }

        RefreshIdentity();
        TryBindBuffPanel();
        UpdateHpUI(false);
    }

    // Executes refresh identity operation.
    // Validates input parameters against null or empty values.
    private void RefreshIdentity()
    {
        if (_targetPlayer == null) return;

        int level = _targetPlayer.Level;
        string playerName = _targetPlayer.PlayerName.ToString();

        if (nameText != null && playerName != _lastName)
        {
            _lastName = playerName;
            nameText.text = string.IsNullOrWhiteSpace(playerName)
                ? "..."
                : playerName;
        }

        if (level != _lastLevel)
        {
            _lastLevel = level;
            if (levelText != null)
                levelText.text = Mathf.Max(1, level).ToString();
        }

        string avatarUrl = _targetPlayer.AvatarUrl.ToString();
        if (avatarImage != null && avatarUrl != _lastAvatarUrl)
        {
            _lastAvatarUrl = avatarUrl;
            var sprite = NetworkPlayer.ResolveAvatarSprite(avatarUrl);
            if (sprite != null) avatarImage.sprite = sprite;
        }
    }

    // Executes try bind buff panel operation.
    private void TryBindBuffPanel()
    {
        if (_buffPanelBound || buffPanel == null || _targetPlayer == null) return;

        var bm = _targetPlayer.GetComponent<BuffManager>();
        if (bm == null) return;

        buffPanel.Init(bm);
        _buffPanelBound = true;
    }

    // Executes update hp ui operation.
    private void UpdateHpUI(bool force)
    {
        if (_targetPlayer == null) return;

        int currentHp = _targetPlayer.CurrentHp;
        int maxHp = _targetPlayer.MaxHp;

        if (force || currentHp != _lastHp || maxHp != _lastMaxHp)
        {
            _lastHp = currentHp;
            _lastMaxHp = maxHp;

            float ratio = maxHp > 0 ? (float)currentHp / maxHp : 0f;
            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01(ratio);
            if (hpText != null) hpText.text = $"{currentHp}/{maxHp}";
        }
    }
}
