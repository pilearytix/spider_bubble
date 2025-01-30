using UnityEngine;
using UnityEngine.EventSystems;

namespace GGJ2025
{
    public class ButtonToFront : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private int originalSiblingIndex;
        private int originalParentSiblingIndex;

        void Start()
        {
            // Store the original indices
            originalSiblingIndex = transform.GetSiblingIndex();
            originalParentSiblingIndex = transform.parent.GetSiblingIndex();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Bring to front
            transform.parent.SetAsLastSibling();
            transform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Return to original position
            transform.parent.SetSiblingIndex(originalParentSiblingIndex);
            transform.SetSiblingIndex(originalSiblingIndex);
        }
    }
} 