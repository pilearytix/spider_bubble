using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using AC;

namespace GGJ2025.UI
{
    public class VirtualizedConversationUI : MonoBehaviour
    {
        [SerializeField] private GameObject optionButtonPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private float optionHeight = 25f;
        
        private List<ButtonDialog> allOptions = new List<ButtonDialog>();
        private List<GameObject> pooledButtons = new List<GameObject>();
        private int visibleOptionsCount;
        private float viewportHeight;
        
        private void Start()
        {
            viewportHeight = scrollRect.viewport.rect.height;
            visibleOptionsCount = Mathf.CeilToInt(viewportHeight / optionHeight) + 1;
            
            // Initialize button pool
            InitializeButtonPool();
            
            scrollRect.onValueChanged.AddListener(OnScroll);
        }
        
        private void InitializeButtonPool()
        {
            // Create enough buttons to fill viewport plus one extra row
            for (int i = 0; i < visibleOptionsCount; i++)
            {
                GameObject button = Instantiate(optionButtonPrefab, contentContainer);
                button.SetActive(false);
                pooledButtons.Add(button);
            }
        }
        
        public void SetOptions(List<ButtonDialog> options)
        {
            allOptions = options;
            
            // Update content size to accommodate all options
            contentContainer.sizeDelta = new Vector2(
                contentContainer.sizeDelta.x,
                options.Count * optionHeight
            );
            
            RefreshVisibleOptions();
        }
        
        private void RefreshVisibleOptions()
        {
            if (allOptions == null || allOptions.Count == 0) return;

            float normalizedPos = 1f - scrollRect.verticalNormalizedPosition;
            int startIndex = Mathf.FloorToInt(normalizedPos * 
                            (allOptions.Count - visibleOptionsCount));
            startIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, allOptions.Count - visibleOptionsCount));
            
            // Deactivate all buttons first
            foreach (var button in pooledButtons)
            {
                button.SetActive(false);
            }
            
            // Show and position visible buttons
            for (int i = 0; i < visibleOptionsCount && (startIndex + i) < allOptions.Count; i++)
            {
                if (i >= pooledButtons.Count) break;
                
                GameObject buttonObj = pooledButtons[i];
                ButtonDialog option = allOptions[startIndex + i];
                
                // Position the button
                RectTransform rt = buttonObj.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0, -optionHeight * (startIndex + i));
                
                // Update button text and data
                var text = buttonObj.GetComponentInChildren<TMP_Text>();
                text.text = option.label;
                
                // Set up button click handler
                var button = buttonObj.GetComponent<UnityEngine.UI.Button>();
                int optionIndex = startIndex + i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnOptionSelected(optionIndex));
                
                buttonObj.SetActive(true);
            }
        }
        
        private void OnScroll(Vector2 value)
        {
            RefreshVisibleOptions();
        }
        
        private void OnOptionSelected(int index)
        {
            if (index >= 0 && index < allOptions.Count)
            {
                // Use the correct AC method to select the dialogue option
                KickStarter.playerInput.activeConversation.RunOption(allOptions[index].ID);
            }
        }
    }
}
