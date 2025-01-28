using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

namespace GGJ2025.UI
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DialogOptionHeightAdjuster : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("The minimum size for both width and height")]
        [SerializeField] private float minSize = 80f;
        
        [Tooltip("Target width for text wrapping")]
        [SerializeField] private float targetWidth = 200f;
        
        [Tooltip("Height per line of text")]
        [SerializeField] private float heightPerLine = 25f;
        
        [Tooltip("Maximum size allowed")]
        [SerializeField] private float maxSize = 400f;
        
        [Tooltip("Maximum words per line")]
        [SerializeField] private int maxWordsPerLine = 3;
        
        [Tooltip("Padding around text")]
        [SerializeField] private float padding = 15f;
        
        [Tooltip("Internal text margins")]
        [SerializeField] private Vector4 textMargins = new Vector4(8, 8, 8, 8);

        private TextMeshProUGUI tmpText;
        private RectTransform parentButton;
        private float lastTextLength;
        private float updateCooldown = 0.1f;
        private float lastUpdateTime;

        private void Awake()
        {
            tmpText = GetComponent<TextMeshProUGUI>();
            tmpText.enableWordWrapping = true;
            tmpText.margin = textMargins;
            
            Transform current = transform;
            while (current != null && !current.name.StartsWith("btnOption"))
            {
                current = current.parent;
            }
            
            if (current != null)
            {
                parentButton = current.GetComponent<RectTransform>();
            }
        }

        private void OnEnable()
        {
            StartCoroutine(AdjustSizeNextFrame());
        }

        private System.Collections.IEnumerator AdjustSizeNextFrame()
        {
            yield return null;
            AdjustSize();
        }

        private void AdjustSize()
        {
            if (tmpText == null || parentButton == null) return;

            RectTransform textRect = tmpText.GetComponent<RectTransform>();
            if (textRect == null) return;

            // Count words
            string[] words = tmpText.text.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            int wordCount = words.Length;

            // Calculate desired number of lines based on word count and max words per line
            int desiredLines = Mathf.Max(1, Mathf.CeilToInt((float)wordCount / maxWordsPerLine));
            
            // Calculate width needed to force the desired number of lines
            float avgWordWidth = words.Length > 0 ? 
                words.Max(w => tmpText.GetPreferredValues(w).x) : 0;
            float widthForDesiredLines = (avgWordWidth * maxWordsPerLine) + textMargins.x + textMargins.z;
            
            // Set width to force line breaks
            textRect.sizeDelta = new Vector2(widthForDesiredLines, 0);
            tmpText.ForceMeshUpdate();

            // Get actual measurements after wrapping
            int lineCount = Mathf.Max(desiredLines, tmpText.textInfo.lineCount);
            float totalMarginX = textMargins.x + textMargins.z + (padding * 2);
            float totalMarginY = textMargins.y + textMargins.w + (padding * 2);

            // Calculate required dimensions
            float requiredWidth = Mathf.Min(widthForDesiredLines + totalMarginX, targetWidth);
            float requiredHeight = (lineCount * heightPerLine) + totalMarginY;

            // Ensure minimum size
            requiredWidth = Mathf.Max(requiredWidth, minSize);
            requiredHeight = Mathf.Max(requiredHeight, minSize);

            // Use the larger dimension to maintain square shape
            float size = Mathf.Min(Mathf.Max(requiredWidth, requiredHeight), maxSize);

            // Apply size
            parentButton.sizeDelta = new Vector2(size, size);

            // Update text rect transform
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(padding, padding);
            textRect.offsetMax = new Vector2(-padding, -padding);

            lastTextLength = tmpText.text.Length;
            
            Debug.Log($"Words: {wordCount}, Desired Lines: {desiredLines}, " +
                     $"Actual Lines: {tmpText.textInfo.lineCount}, Size: {size}");
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime < updateCooldown) return;
            
            if (tmpText.text.Length != lastTextLength)
            {
                AdjustSize();
                lastUpdateTime = Time.time;
            }
        }

        private void OnValidate()
        {
            minSize = Mathf.Max(minSize, 40f);
            heightPerLine = Mathf.Max(heightPerLine, 15f);
            padding = Mathf.Max(padding, 5f);
            maxWordsPerLine = Mathf.Max(maxWordsPerLine, 1);
            targetWidth = Mathf.Max(targetWidth, 50f);
            
            if (tmpText != null)
            {
                tmpText.margin = textMargins;
            }
        }
    }
} 