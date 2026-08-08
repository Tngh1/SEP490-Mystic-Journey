using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tạo dấu chân khi nhân vật di chuyển.
/// Dùng Object Pool để tránh Instantiate/Destroy liên tục gây GC pressure và giật nhỏ.
/// </summary>
public class FootstepController : MonoBehaviour
{
    [Header("Footstep Settings")]
    [SerializeField] private GameObject footstepPrefab;
    [SerializeField] private float stepInterval = 0.5f;
    [SerializeField] private float footstepLifetime = 5f;
    [SerializeField] private float fadeStartTime = 3f;
    [SerializeField] private Vector2 footstepOffset = new Vector2(0, -0.5f);

    [Header("Movement Detection")]
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("Pool Settings")]
    [Tooltip("Số footstep object khởi tạo trước trong pool.")]
    [SerializeField] private int poolInitialSize = 10;

    // ──────────────────────────────────────────────────────────────────────────
    // Runtime
    // ──────────────────────────────────────────────────────────────────────────

    private float stepTimer;
    private Vector2 lastPosition;
    private bool isLeftFoot = true;

    // Pool — dùng Stack cho O(1) push/pop
    private Stack<GameObject> m_Pool;
    private Transform m_PoolRoot; // parent để giữ hierarchy gọn

    // ──────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    void Start()
    {
        lastPosition = transform.position;
        stepTimer = stepInterval;

        if (footstepPrefab == null) return;

        // Tạo pool root ẩn
        m_PoolRoot = new GameObject("[FootstepPool]").transform;
        m_PoolRoot.SetParent(null);

        m_Pool = new Stack<GameObject>(poolInitialSize);
        for (int i = 0; i < poolInitialSize; i++)
            m_Pool.Push(CreatePooledObject());
    }

    void Update()
    {
        if (footstepPrefab == null) return;

        float distanceMoved = Vector2.Distance(transform.position, lastPosition);
        lastPosition = transform.position;

        if (distanceMoved > movementThreshold * Time.deltaTime)
        {
            stepTimer += Time.deltaTime;
            if (stepTimer >= stepInterval)
            {
                SpawnFootstep();
                stepTimer = 0f;
            }
        }
    }

    private void OnDestroy()
    {
        // Dọn sạch pool root khi object bị destroy
        if (m_PoolRoot != null)
            Destroy(m_PoolRoot.gameObject);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Pool helpers
    // ──────────────────────────────────────────────────────────────────────────

    private GameObject CreatePooledObject()
    {
        var go = Instantiate(footstepPrefab, m_PoolRoot);
        go.SetActive(false);
        return go;
    }

    private GameObject GetFromPool()
    {
        if (m_Pool == null) return null;

        GameObject go;
        // Lọc object bị destroy ngoài ý muốn (edge case khi scene change)
        while (m_Pool.Count > 0)
        {
            go = m_Pool.Pop();
            if (go != null) return go;
        }

        // Pool rỗng → tạo thêm 1 object (pool tự grow)
        return CreatePooledObject();
    }

    private void ReturnToPool(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        go.transform.SetParent(m_PoolRoot);
        m_Pool.Push(go);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Footstep logic
    // ──────────────────────────────────────────────────────────────────────────

    private void SpawnFootstep()
    {
        var footstep = GetFromPool();
        if (footstep == null) return;

        // Đặt vị trí và bỏ khỏi pool root để nằm trong scene
        Vector2 pos = (Vector2)transform.position + footstepOffset;
        pos.x += isLeftFoot ? -0.15f : 0.15f;

        footstep.transform.SetParent(null);
        footstep.transform.position = pos;
        footstep.transform.rotation = Quaternion.identity;
        footstep.SetActive(true);

        var sr = footstep.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.flipX = isLeftFoot;
            sr.flipY = transform.localScale.x < 0;
            // Reset alpha về 1 (quan trọng khi reuse từ pool)
            var c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, 1f);
        }

        isLeftFoot = !isLeftFoot;

        StartCoroutine(FadeAndReturn(footstep, sr));
    }

    private IEnumerator FadeAndReturn(GameObject footstep, SpriteRenderer sr)
    {
        // Chờ trước khi fade
        yield return new WaitForSeconds(fadeStartTime);

        float fadeDuration = footstepLifetime - fadeStartTime;
        float elapsed = 0f;

        if (sr != null)
        {
            Color originalColor = sr.color;
            while (elapsed < fadeDuration)
            {
                // Guard: object có thể bị return về pool sớm nếu thiếu object
                if (footstep == null || !footstep.activeSelf) yield break;

                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }

        // Trả về pool thay vì Destroy
        ReturnToPool(footstep);
    }
}