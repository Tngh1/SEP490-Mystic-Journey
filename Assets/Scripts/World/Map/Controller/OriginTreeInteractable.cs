using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(WorldInteractable))]
public class OriginTreeInteractable : MonoBehaviour
{
    [Header("Quest Link")]
    [SerializeField] private int linkedQuestId = 45;
    [SerializeField] private string objectKey = "ElfForest.OriginTree";
    [SerializeField] private string displayName = "Origin Tree";

    [Header("Healing Visual")]
    [SerializeField] private float healingDuration = 2.5f;
    [SerializeField] private Color healedColor = new Color(0.72f, 1f, 0.72f, 1f);
    [SerializeField] private float pulseScale = 1.04f;

    [Header("Video")]
    // Clip nằm trong Resources nên load bằng tên, không cần gắn vào scene.
    // Chưa có file thì bỏ qua video và chỉ chạy hiệu ứng tween như trước.
    [Tooltip("Tên VideoClip trong thư mục Resources.")]
    [SerializeField] private string videoResourceName = "The_Purification_of_the_Origi_Tree";

    [Tooltip("Thời gian chờ tối đa cho video.")]
    [SerializeField] private float maxVideoWait = 30f;

    private WorldInteractable _interactable;
    private SpriteRenderer _treeRenderer;
    private Vector3 _baseScale;
    private bool _isHealing;
    private bool _healed;
    private VideoPlayer _videoPlayer;
    private bool _videoFinished;

    private void Awake()
    {
        _interactable = GetComponent<WorldInteractable>();
        _treeRenderer = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
    }

    private void Start()
    {
        _interactable.ConfigureQuestItem(objectKey, displayName, linkedQuestId, 1, 3.5f);
        RefreshVisibility();
        WorldRuntimeEvents.QuestsChanged += RefreshVisibility;
    }

    private void OnDestroy()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshVisibility;
        if (_videoPlayer != null)
            _videoPlayer.loopPointReached -= OnVideoFinished;
    }

    public void StartHeal()
    {
        if (_isHealing || _healed) return;

        var questState = QuestUIManager.Instance?.GetQuestState(linkedQuestId);
        if (questState == null ||
            !string.Equals(questState.status, "InProgress", System.StringComparison.OrdinalIgnoreCase))
        {
            WorldRuntimeEvents.RaiseMessage("Speak with Lyra and accept the healing rite first.");
            return;
        }

        if (!ApiClient.Instance.HasToken())
        {
            WorldRuntimeEvents.RaiseMessage("The healing rite requires a connection to the world.");
            return;
        }

        _isHealing = true;
        WorldInteractionPromptRuntime.Hide();
        WorldApi.Instance.InteractObject(
            objectKey,
            "Interact",
            linkedQuestId,
            1,
            response =>
            {
                if (response?.Quest != null)
                    QuestUIManager.Instance?.ApplyServerQuestState(response.Quest);
                InventoryUIManager.RefreshAny(refreshStats: false);
                WorldRuntimeEvents.RaiseQuestsChanged();
                StartCoroutine(HealingSequence());
            },
            error =>
            {
                _isHealing = false;
                Debug.LogWarning($"[OriginTreeInteractable] InteractObject failed: {error.Message}");
                WorldRuntimeEvents.RaiseMessage(error.Message);
                RefreshVisibility();
            });
    }

    private IEnumerator HealingSequence()
    {
        // Video chạy sau khi API trả về OK, vì call đó tiêu 4 quyển sách và có thể fail.
        // Phát cutscene trước rồi báo lỗi thiếu sách sẽ khó hiểu hơn là không phát.
        yield return PlayPurificationVideo();

        var startColor = _treeRenderer != null ? _treeRenderer.color : Color.white;
        var elapsed = 0f;
        while (elapsed < healingDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / healingDuration);
            var pulse = Mathf.Sin(t * Mathf.PI) * (pulseScale - 1f);
            transform.localScale = _baseScale * (1f + pulse);
            if (_treeRenderer != null)
                _treeRenderer.color = Color.Lerp(startColor, healedColor, t);
            yield return null;
        }

        transform.localScale = _baseScale;
        ApplyHealedVisual();
        _isHealing = false;
        _healed = true;
        SetColliderEnabled(false);
        _interactable.UpdateOverheadUI();
        WorldRuntimeEvents.RaiseMessage("The Origin Tree is healing. Talk to Lyra.");
    }

    private IEnumerator PlayPurificationVideo()
    {
        if (string.IsNullOrEmpty(videoResourceName)) yield break;

        var clip = Resources.Load<VideoClip>(videoResourceName);
        if (clip == null)
        {
            // ponytail: chưa có file video thì chỉ cảnh báo và chạy tween.
            // Thêm Assets/UI/Videos/Resources/<videoResourceName>.mp4 là tự động chạy.
            Debug.LogWarning($"[OriginTreeInteractable] Không tìm thấy VideoClip '{videoResourceName}' trong Resources, bỏ qua cutscene.");
            yield break;
        }

        if (_videoPlayer == null)
        {
            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.loopPointReached += OnVideoFinished;
        }

        // Ẩn player trong lúc chiếu, giống IvyTree
        var player = GameObject.FindGameObjectWithTag("Player");
        SpriteRenderer[] playerSprites = null;
        if (player != null)
        {
            playerSprites = player.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sp in playerSprites) sp.enabled = false;
        }

        _videoFinished = false;
        _videoPlayer.clip = clip;
        MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoStarted(_videoPlayer);
        _videoPlayer.Play();

        var elapsed = 0f;
        while (!_videoFinished && elapsed < maxVideoWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        _videoFinished = false;

        _videoPlayer.Stop();
        MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoEnded(_videoPlayer);

        if (playerSprites != null)
            foreach (var sp in playerSprites) sp.enabled = true;
    }

    private void OnVideoFinished(VideoPlayer vp) => _videoFinished = true;

    private void RefreshVisibility()
    {
        if (QuestUIManager.Instance == null) return;

        var state = QuestUIManager.Instance.GetQuestState(linkedQuestId);
        var status = state?.status ?? string.Empty;
        _healed = string.Equals(status, "Completed", System.StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(status, "Claimed", System.StringComparison.OrdinalIgnoreCase);

        if (_healed)
            ApplyHealedVisual();

        var inProgress = string.Equals(status, "InProgress", System.StringComparison.OrdinalIgnoreCase);
        SetColliderEnabled(inProgress && !_healed && !_isHealing);
        _interactable.UpdateOverheadUI();
    }

    private void ApplyHealedVisual()
    {
        transform.localScale = _baseScale;
        if (_treeRenderer != null)
            _treeRenderer.color = healedColor;
    }

    private void SetColliderEnabled(bool enabled)
    {
        var col2D = GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = enabled;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = enabled;
    }
}
