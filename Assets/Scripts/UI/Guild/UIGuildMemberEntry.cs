using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models;
using System;

namespace MysticJourney.UI.Guild
{
    // Executes mono behaviour operation.
    // Validates input parameters against null or empty values.
    public class UIGuildMemberEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txtMemberName;
        [SerializeField] private TextMeshProUGUI txtLevel;
        [SerializeField] private TextMeshProUGUI txtMedals;
        [SerializeField] private TextMeshProUGUI txtFeats;
        [SerializeField] private TextMeshProUGUI txtStatus;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image onlineIndicator;
        [SerializeField] private Button btnKick;
        [SerializeField] private Sprite leaderIconSprite;

        private bool canKick;
        private Image rowBackground;
        private Color defaultBackgroundColor;
        private Color defaultNameColor;
        private FontStyles defaultNameStyle;
        private bool visualDefaultsCached;

        // Process the supplied values: normalizes or validates the text before returning the derived result and maps the input discriminator to the corresponding domain value and fallback.
        public void Setup(
            GuildMemberResponseDto member,
            bool canKick = false,
            Action<int> onKick = null,
            bool isKickMode = false,
            bool isCurrentPlayer = false)
        {
            this.canKick = canKick;
            EnsureVisualReferences();

            if (txtMemberName != null)
            {
                txtMemberName.text = member.playerDisplayName;
                txtMemberName.color = isCurrentPlayer ? new Color32(91, 48, 8, 255) : defaultNameColor;
                txtMemberName.fontStyle = isCurrentPlayer ? defaultNameStyle | FontStyles.Bold : defaultNameStyle;
            }
            if (txtLevel != null) txtLevel.text = $"Lv. {member.playerLevel}";

            if (rowBackground != null)
            {
                rowBackground.color = isCurrentPlayer
                    ? new Color32(255, 229, 145, 255)
                    : defaultBackgroundColor;
            }

            SetLeaderCrown(string.Equals(member.role, "Leader", StringComparison.OrdinalIgnoreCase));

            if (txtMedals != null) txtMedals.text = member.medals.ToString();
            if (txtFeats != null) txtFeats.text = member.feats.ToString();

            if (txtStatus != null)
            {
                txtStatus.text = member.isOnline ? "Online" : "Offline";
                txtStatus.color = member.isOnline ? Color.green : Color.gray;
            }

            if (onlineIndicator != null)
            {
                onlineIndicator.color = member.isOnline ? Color.green : Color.gray;
                onlineIndicator.enabled = member != null;
            }

            if (avatarImage != null)
            {
                avatarImage.enabled = true;
                string avatarUrl = string.IsNullOrWhiteSpace(member.playerAvatarUrl) ? "avatar_1" : member.playerAvatarUrl;
                Sprite avatarSprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
                if (avatarSprite != null)
                {
                    avatarImage.sprite = avatarSprite;
                }
            }

            if (btnKick != null)
            {
                btnKick.gameObject.SetActive(this.canKick && isKickMode);
                btnKick.onClick.RemoveAllListeners();
                btnKick.onClick.AddListener(() =>
                {
                    MysticJourney.UI.UIPopup.Instance.ShowConfirm(
                        "Kick Member",
                        $"Are you sure you want to kick {member.playerDisplayName}?",
                        () => onKick?.Invoke(member.playerProfileId),
                        null
                    );
                });
            }
        }

        // Executes set kick mode operation.
        public void SetKickMode(bool isKickMode)
        {
            if (btnKick != null)
            {
                btnKick.gameObject.SetActive(this.canKick && isKickMode);
            }
        }

        // Executes ensure visual references operation.
        private void EnsureVisualReferences()
        {
            if (rowBackground == null)
            {
                rowBackground = transform.Find("Background")?.GetComponent<Image>();
            }

            if (visualDefaultsCached)
            {
                return;
            }

            if (rowBackground != null)
            {
                defaultBackgroundColor = rowBackground.color;
            }

            if (txtMemberName != null)
            {
                defaultNameColor = txtMemberName.color;
                defaultNameStyle = txtMemberName.fontStyle;
            }

            visualDefaultsCached = true;
        }

        // Executes set leader crown operation.
        private void SetLeaderCrown(bool visible)
        {
            Transform existing = transform.Find("LeaderCrown");
            GameObject crownObject = existing != null ? existing.gameObject : null;

            if (crownObject == null && visible && leaderIconSprite != null)
            {
                crownObject = new GameObject("LeaderCrown", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform crownRect = crownObject.GetComponent<RectTransform>();
                crownRect.SetParent(transform, false);
                crownRect.anchorMin = new Vector2(0f, 1f);
                crownRect.anchorMax = new Vector2(0f, 1f);
                crownRect.pivot = new Vector2(0.5f, 0.5f);
                crownRect.anchoredPosition = new Vector2(113f, -15f);
                crownRect.sizeDelta = new Vector2(38f, 30f);

                Image crownImage = crownObject.GetComponent<Image>();
                crownImage.sprite = leaderIconSprite;
                crownImage.preserveAspect = true;
                crownImage.raycastTarget = false;

                Shadow shadow = crownObject.AddComponent<Shadow>();
                shadow.effectColor = new Color32(50, 28, 8, 190);
                shadow.effectDistance = new Vector2(1f, -1f);
                shadow.useGraphicAlpha = true;
            }

            if (crownObject != null)
            {
                crownObject.transform.SetAsLastSibling();
                crownObject.SetActive(visible);
            }
        }
    }
}
