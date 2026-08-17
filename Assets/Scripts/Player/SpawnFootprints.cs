using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Executes mono behaviour operation.
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


    private float stepTimer;
    private Vector2 lastPosition;
    private bool isLeftFoot = true;

    private Stack<GameObject> m_Pool;
    private Transform m_PoolRoot;


    void Start()
    {
        lastPosition = transform.position;
        stepTimer = stepInterval;

        if (footstepPrefab == null) return;

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

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (m_PoolRoot != null)
            Destroy(m_PoolRoot.gameObject);
    }


    // Create pooled object; it instantiates the required Unity object and updates active.
    private GameObject CreatePooledObject()
    {
        var go = Instantiate(footstepPrefab, m_PoolRoot);
        go.SetActive(false);
        return go;
    }

    // Executes get from pool operation.
    private GameObject GetFromPool()
    {
        if (m_Pool == null) return null;

        GameObject go;
        while (m_Pool.Count > 0)
        {
            go = m_Pool.Pop();
            if (go != null) return go;
        }

        return CreatePooledObject();
    }

    // Executes return to pool operation.
    private void ReturnToPool(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        go.transform.SetParent(m_PoolRoot);
        m_Pool.Push(go);
    }


    // Executes spawn footstep operation.
    private void SpawnFootstep()
    {
        var footstep = GetFromPool();
        if (footstep == null) return;

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
            var c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, 1f);
        }

        isLeftFoot = !isLeftFoot;

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(FadeAndReturn(footstep, sr));
    }

    // Executes fade and return operation.
    private IEnumerator FadeAndReturn(GameObject footstep, SpriteRenderer sr)
    {
        yield return new WaitForSeconds(fadeStartTime);

        float fadeDuration = footstepLifetime - fadeStartTime;
        float elapsed = 0f;

        if (sr != null)
        {
            Color originalColor = sr.color;
            while (elapsed < fadeDuration)
            {
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

        ReturnToPool(footstep);
    }
}
