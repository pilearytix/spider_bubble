using UnityEngine;

public class bringtofront : StateMachineBehaviour
{
    private int originalSiblingIndex;
    private int originalButtonSiblingIndex;

    // Called when a state machine enters a state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (originalSiblingIndex == 0)
        {
            originalSiblingIndex = animator.transform.parent.GetSiblingIndex();
            originalButtonSiblingIndex = animator.transform.GetSiblingIndex();
            Debug.Log($"Storing original indices - Parent: {originalSiblingIndex}, Button: {originalButtonSiblingIndex}");
        }
            
        Debug.Log($"Moving {animator.gameObject.name} and its parent to front");
        animator.transform.parent.SetAsLastSibling();
        animator.transform.SetAsLastSibling();
    }

    // Called when a state machine exits a state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        Debug.Log($"Returning {animator.gameObject.name} and parent to original positions");
        animator.transform.parent.SetSiblingIndex(originalSiblingIndex);
        animator.transform.SetSiblingIndex(originalButtonSiblingIndex);
    }
}