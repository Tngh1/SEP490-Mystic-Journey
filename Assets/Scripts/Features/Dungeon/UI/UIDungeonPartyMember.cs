using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the HP and MP for a specific party member in the dungeon.
/// Automatically polls the assigned NetworkPlayer for updates.
/// </summary>
public class UIDungeonPartyMember : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private UIBuffPanel buffPanel;

    private NetworkPlayer _targetPlayer;
    private int _lastHp = -1;

    public void Init(NetworkPlayer target)
    {
        _targetPlayer = target;

        if (_targetPlayer != null)
        {
            if (nameText != null)
                nameText.text = $"Lv.{_targetPlayer.Level} {_targetPlayer.PlayerName}";
                
            // Avatar logic can be expanded here based on class
            if (avatarImage != null)
            {
                // Temporarily just load a default avatar since NetworkPlayer lacks AvatarUrl
                avatarImage.sprite = Resources.Load<Sprite>("Avatars/avatar_1");
            }

            if (buffPanel != null)
            {
                var bm = _targetPlayer.GetComponent<BuffManager>();
                if (bm != null) buffPanel.Init(bm);
            }

            UpdateHpUI(true); // Force initial update
        }
    }

    private void Update()
    {
        if (_targetPlayer == null || _targetPlayer.Object == null || !_targetPlayer.Object.IsValid)
        {
            // Player disconnected or left
            Destroy(gameObject);
            return;
        }

        UpdateHpUI(false);
    }

    private void UpdateHpUI(bool force)
    {
        if (_targetPlayer == null) return;

        int currentHp = _targetPlayer.CurrentHp;
        int maxHp = _targetPlayer.MaxHp;

        if (force || currentHp != _lastHp)
        {
            _lastHp = currentHp;

            float ratio = maxHp > 0 ? (float)currentHp / maxHp : 0f;
            if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01(ratio);
            if (hpText != null) hpText.text = $"{currentHp}/{maxHp}";
        }
    }
}
