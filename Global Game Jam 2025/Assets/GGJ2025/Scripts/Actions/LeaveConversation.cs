using UnityEngine;
using AC;
using UnityEngine.UI; // Add for Button

public class LeaveConversation : MonoBehaviour
{
    void Start()
    {        // Get the Button component and add our click handler
        UnityEngine.UI.Button button = GetComponent<UnityEngine.UI.Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }

    private void OnButtonClick()
    {
        // End the conversation using the correct Adventure Creator method
        KickStarter.playerInput.EndConversation();
    }
} 