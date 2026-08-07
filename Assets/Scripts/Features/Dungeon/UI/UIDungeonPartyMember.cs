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
    [SerializeField] private TMP_Text levelText;   // "LevelText" child — level badge below the avatar
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

        // Reset caches so RefreshIdentity() always fires on the first poll — even
        // when the proxy's initial replicated value equals the C# default (0 / "").
        _lastLevel     = -1;
        _lastName      = null;
        _lastAvatarUrl = null;
        _lastHp        = -1;
        _lastMaxHp     = -1;
        _buffPanelBound = false;

        // Blank the text immediately so stale content from a previous target is not
        // visible on the frame before the first RefreshIdentity() runs.
        if (nameText  != null) nameText.text  = "...";
        if (levelText != null) levelText.text = "-";

        if (_targetPlayer != null)
        {
            RefreshIdentity();
            TryBindBuffPanel();
            UpdateHpUI(true);
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

        // Name text — player name only; level is shown in the separate LevelText badge.
        if (nameText != null && playerName != _lastName)
        {
            _lastName = playerName;
            nameText.text = string.IsNullOrWhiteSpace(playerName)
                ? "..."        // identity has not replicated yet
                : playerName;
        }

        // Level badge — update independently so a late-arriving Level value still shows.
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
