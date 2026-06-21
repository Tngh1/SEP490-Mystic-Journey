using System.Collections;
using UnityEngine;

public class FootstepController : MonoBehaviour
{
    [Header("Footstep Settings")]
    [SerializeField] private GameObject footstepPrefab; // Assign your footprint prefab
    [SerializeField] private float stepInterval = 0.5f; // Time between footsteps
    [SerializeField] private float footstepLifetime = 5f; // How long footsteps stay visible
    [SerializeField] private float fadeStartTime = 3f; // When to start fading
    [SerializeField] private Vector2 footstepOffset = new Vector2(0, -0.5f); // Offset from character
    
    [Header("Movement Detection")]
    [SerializeField] private float movementThreshold = 0.1f; // Minimum speed to create footsteps
    
    private float stepTimer;
    private Vector2 lastPosition;
    private Rigidbody2D rb;
    private bool isLeftFoot = true; // Alternate between left and right
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        lastPosition = transform.position;
        stepTimer = stepInterval; // Start ready to make a footstep
    }
    
    void Update()
    {
        // Calculate movement speed
        float distanceMoved = Vector2.Distance(transform.position, lastPosition);
        lastPosition = transform.position;
        
        // Check if character is moving
        if (distanceMoved > movementThreshold * Time.deltaTime)
        {
            stepTimer += Time.deltaTime;
            
            // Time to create a new footstep
            if (stepTimer >= stepInterval)
            {
                CreateFootstep();
                stepTimer = 0f;
            }
        }
    }
    
    void CreateFootstep()
    {
        if (footstepPrefab == null)
        {
            Debug.LogWarning("Footstep prefab is not assigned!");
            return;
        }
        
        // Calculate footstep position
        Vector2 footstepPosition = (Vector2)transform.position + footstepOffset;
        
        // Alternate between left and right foot positions
        float horizontalOffset = isLeftFoot ? -0.15f : 0.15f;
        footstepPosition.x += horizontalOffset;
        
        // Instantiate the footstep prefab
        GameObject footstep = Instantiate(footstepPrefab, footstepPosition, Quaternion.identity);
        
        // Get the SpriteRenderer from the prefab
        SpriteRenderer spriteRenderer = footstep.GetComponent<SpriteRenderer>();
        
        if (spriteRenderer != null)
        {
            // Optional: Flip the sprite for left/right foot
            spriteRenderer.flipX = isLeftFoot;
            
            // Match character's facing direction (optional)
            if (transform.localScale.x < 0)
            {
                spriteRenderer.flipY = true;
            }
            
            // Start the fade coroutine
            StartCoroutine(FadeAndDestroyFootstep(footstep, spriteRenderer));
        }
        else
        {
            // If no SpriteRenderer, just destroy after lifetime
            Destroy(footstep, footstepLifetime);
        }
        
        // Toggle foot
        isLeftFoot = !isLeftFoot;
    }
    
    IEnumerator FadeAndDestroyFootstep(GameObject footstep, SpriteRenderer spriteRenderer)
    {
        Color originalColor = spriteRenderer.color;
        
        // Wait before starting to fade
        yield return new WaitForSeconds(fadeStartTime);
        
        // Fade out over remaining time
        float fadeDuration = footstepLifetime - fadeStartTime;
        float elapsed = 0f;
        
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }
        
        // Destroy the footstep GameObject
        Destroy(footstep);
    }
}