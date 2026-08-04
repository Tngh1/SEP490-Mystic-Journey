using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the HP, name and avatar of one party member in the dungeon.
/// Automatically polls the assigned NetworkPlayer for updates.
///
/// Everything is polled rather than read once in <see cref="Init"/>: this row is built for
/// a REMOTE player, i.e. a Fusion proxy, and a proxy's [Networked] properties are not
/// guaranteed to hold their real values on the frame the object appears. Reading Level /
/// PlayerName / AvatarUrl only at Init produced rows reading "Lv.0" with no name and a
/// default avatar, which then never corrected themselves.
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
    private int _lastMaxHp = -1;
    private int _lastLevel = -1;
    private string _lastName;
    private string _lastAvatarUrl;
    private bool _buffPanelBound;

    public void Init(NetworkPlayer target)
    {
        _targetPlayer = target;

        if (_targetPlayer != null)
        {
            RefreshIdentity();
            TryBindBuffPanel();
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

        RefreshIdentity();
        TryBindBuffPanel();
        UpdateHpUI(false);
    }

    /// <summary>
    /// Re-read the replicated identity (level, name, avatar) and repaint only on change,
    /// so this stays cheap despite running every frame.
    /// </summary>
    private void RefreshIdentity()
    {
        if (_targetPlayer == null) return;

        int level = _targetPlayer.Level;
        string playerName = _targetPlayer.PlayerName.ToString();

        if (nameText != null && (level != _lastLevel || playerName != _lastName))
        {
            _lastLevel = level;
            _lastName = playerName;
            nameText.text = string.IsNullOrWhiteSpace(playerName)
                ? "..."                                 // identity has not replicated yet
                : $"Lv.{Mathf.Max(1, level)} {playerName}";
        }

        string avatarUrl = _targetPlayer.AvatarUrl.ToString();
        if (avatarImage != null && avatarUrl != _lastAvatarUrl)
        {
            _lastAvatarUrl = avatarUrl;
            var sprite = NetworkPlayer.ResolveAvatarSprite(avatarUrl);
            if (sprite != null) avatarImage.sprite = sprite;
        }
    }

    /// <summary>
    /// Bind the buff panel once the member's BuffManager exists. It can be added a frame
    /// after the NetworkObject spawns, so a single attempt at Init could miss it.
    /// </summary>
    private void TryBindBuffPanel()
    {
        if (_buffPanelBound || buffPanel == null || _targetPlayer == null) return;

        var bm = _targetPlayer.GetComponent<BuffManager>();
        if (bm == null) return;

        buffPanel.Init(bm);
        _buffPanelBound = true;
    }

    private void UpdateHpUI(bool force)
    {
        if (_targetPlayer == null) return;

        int currentHp = _targetPlayer.CurrentHp;
        int maxHp = _targetPlayer.MaxHp;

        // MaxHp is part of the condition because it replicates independently of CurrentHp:
        // when a proxy showed up with 0/0, a later MaxHp-only update left the row stuck on
        // "0/0" forever because CurrentHp had not changed.
        if (force || currentHp != _lastHp || maxHp != _lastMaxHp)
        {
            _lastHp = currentHp;
            _lastMaxHp = maxHp;

            float ratio = maxHp > 0 ? (float)currentHp / maxHp : 0f;
            if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01(ratio);
            if (hpText != null) hpText.text = $"{currentHp}/{maxHp}";
        }
    }
}
