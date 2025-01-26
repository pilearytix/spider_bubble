using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonToFront : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Transform parentTransform;
    private int originalSiblingIndex;

    private void Start()
    {
        parentTransform = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Move to last sibling (top layer)
        transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Return to original position in hierarchy
        transform.SetSiblingIndex(originalSiblingIndex);
    }
}
