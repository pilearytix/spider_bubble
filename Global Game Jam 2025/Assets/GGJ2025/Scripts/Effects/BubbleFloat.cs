using UnityEngine;

namespace GGJ2025.Effects
{
    public class BubbleFloat : MonoBehaviour
    {
        [Header("Float Settings")]
        [SerializeField] private float minSpeed = 1f;
        [SerializeField] private float maxSpeed = 3f;
        [SerializeField] private float wanderStrength = 0.5f;
        [SerializeField] private float minWobbleFrequency = 1f;
        [SerializeField] private float maxWobbleFrequency = 3f;
        [SerializeField] private float minWobbleAmplitude = 0.1f;
        [SerializeField] private float maxWobbleAmplitude = 0.5f;
        [SerializeField] private float returnDuration = 0.5f;
        
        [Header("Spawn Settings")]
        [Tooltip("Spawn radius as a percentage of the screen height")]
        [Range(0f, 1f)]
        [SerializeField] private float spawnRadiusPercent = 0.2f;
        [Tooltip("If true, will visualize the spawn radius in the editor")]
        [SerializeField] private bool showSpawnRadius = true;
        
        [Header("Lifetime Settings")]
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float fadeOutDuration = 1f;
        
        [Header("Visibility")]
        [SerializeField] private bool isEnabled = true;
        
        private float speed;
        private Vector2 wanderDirection;
        private float startTime;
        private Vector3 startPosition;
        private SpriteRenderer spriteRenderer;
        private CanvasGroup canvasGroup;
        private bool wasVisible;
        private float destroyTime;
        private Transform rootParent;
        private bool isReturning;
        private float returnStartTime;
        private Vector3 returnStartPos;
        
        // Separate wobble parameters for X and Y
        private float wobbleFrequencyX;
        private float wobbleFrequencyY;
        private float wobbleAmplitudeX;
        private float wobbleAmplitudeY;
        private float wobblePhaseX;
        private float wobblePhaseY;
        
        private float CurrentSpawnRadius 
        {
            get 
            {
                // If we're in a Canvas
                RectTransform parentRect = transform.parent?.GetComponent<RectTransform>();
                if (parentRect != null)
                {
                    // Use the smallest dimension of the parent rect
                    return Mathf.Min(parentRect.rect.width, parentRect.rect.height) * spawnRadiusPercent;
                }
                
                // If we're in world space, use screen height as reference
                return Screen.height * spawnRadiusPercent;
            }
        }
        
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            canvasGroup = GetComponent<CanvasGroup>();
            rootParent = FindRootParent();
            wasVisible = true;
        }

        private void OnEnable()
        {
            // Reset bubble position and movement whenever the object becomes active
            RandomizePosition();
            InitializeMovement();
        }

        private Transform FindRootParent()
        {
            Transform current = transform;

            // Walk up the hierarchy until we find ConversationUI or hit null
            while (current != null)
            {
                if (current.name == "ConversationUI Variant")
                    return current;
                current = current.parent;
            }

            // Fallback to immediate parent if ConversationUI not found
            return transform.parent;
        }

        private void RandomizePosition()
        {
            // Find the parent to use as center point
            Transform centerParent = transform.parent != null ? transform.parent : transform;
            
            // Get random point within circle
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float randomRadius = Random.Range(0f, CurrentSpawnRadius);
            Vector2 randomOffset = new Vector2(
                Mathf.Cos(randomAngle) * randomRadius,
                Mathf.Sin(randomAngle) * randomRadius
            );
            
            // Set position relative to parent
            transform.position = centerParent.position + new Vector3(randomOffset.x, randomOffset.y, 0);
        }
        
        private void InitializeMovement()
        {
            // Cancel any pending destruction
            CancelInvoke("DestroyObject");
            
            // Initialize random movement parameters
            speed = Random.Range(minSpeed, maxSpeed);
            
            // Random direction for wandering (normalized to keep consistent speed)
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            wanderDirection = new Vector2(
                Mathf.Cos(randomAngle),
                Mathf.Sin(randomAngle)
            ).normalized * wanderStrength;
            
            // Initialize random wobble parameters for both X and Y
            wobbleFrequencyX = Random.Range(minWobbleFrequency, maxWobbleFrequency);
            wobbleFrequencyY = Random.Range(minWobbleFrequency, maxWobbleFrequency);
            wobbleAmplitudeX = Random.Range(minWobbleAmplitude, maxWobbleAmplitude);
            wobbleAmplitudeY = Random.Range(minWobbleAmplitude, maxWobbleAmplitude);
            
            // Random phase offsets to make bubbles start at different positions in their wobble
            wobblePhaseX = Random.Range(0f, Mathf.PI * 2f);
            wobblePhaseY = Random.Range(0f, Mathf.PI * 2f);
            
            startTime = Time.time;
            startPosition = transform.position;
            destroyTime = startTime + lifetime;

            // Reset alpha
            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = 1f;
                spriteRenderer.color = color;
            }
            else if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            
            // Schedule destruction
            Invoke("DestroyObject", lifetime);
        }
        
        private void DestroyObject()
        {
            Destroy(gameObject);
        }
        
        private void Update()
        {
            // Remove the visibility state change check since we're using OnEnable instead
            if (!isEnabled) return;

            if (isReturning)
            {
                // Handle return to center animation
                float elapsed = Time.time - returnStartTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                
                // Use smooth step for more natural movement
                t = t * t * (3f - 2f * t);
                
                Transform centerParent = transform.parent != null ? transform.parent : transform;
                transform.position = Vector3.Lerp(returnStartPos, centerParent.position, t);

                // When return animation is complete, start normal bubble movement
                if (t >= 1f)
                {
                    isReturning = false;
                    RandomizePosition();
                    InitializeMovement();
                }
                return;
            }
            
            float timeSinceStart = Time.time - startTime;
            
            // Calculate base movement in the wander direction
            Vector2 basePosition = startPosition + (Vector3)(wanderDirection * speed * timeSinceStart);
            
            // Calculate wobble for both X and Y
            float wobbleX = Mathf.Sin(timeSinceStart * wobbleFrequencyX + wobblePhaseX) * wobbleAmplitudeX;
            float wobbleY = Mathf.Sin(timeSinceStart * wobbleFrequencyY + wobblePhaseY) * wobbleAmplitudeY;
            
            // Combine base movement and wobble
            Vector3 newPosition = new Vector3(
                basePosition.x + wobbleX,
                basePosition.y + wobbleY,
                transform.position.z
            );
            
            // Update position
            transform.position = newPosition;
            
            // Handle fading out near end of lifetime
            float remainingTime = destroyTime - Time.time;
            if (remainingTime < fadeOutDuration)
            {
                float alpha = remainingTime / fadeOutDuration;
                
                // Apply fade to either SpriteRenderer or CanvasGroup
                if (spriteRenderer != null)
                {
                    Color color = spriteRenderer.color;
                    color.a = alpha;
                    spriteRenderer.color = color;
                }
                else if (canvasGroup != null)
                {
                    canvasGroup.alpha = alpha;
                }
            }
        }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (showSpawnRadius)
            {
                Transform centerParent = transform.parent != null ? transform.parent : transform;
                
                // Draw spawn radius using current calculated radius
                UnityEditor.Handles.color = new Color(0.2f, 0.8f, 1f, 0.2f);
                UnityEditor.Handles.DrawSolidDisc(centerParent.position, Vector3.forward, CurrentSpawnRadius);
                UnityEditor.Handles.color = new Color(0.2f, 0.8f, 1f, 1f);
                UnityEditor.Handles.DrawWireDisc(centerParent.position, Vector3.forward, CurrentSpawnRadius);
            }
        }
        #endif
    }
} 