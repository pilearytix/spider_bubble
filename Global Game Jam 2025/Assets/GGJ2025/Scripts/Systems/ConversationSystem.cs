using UnityEngine;
using System.Collections.Generic;
using AC;

namespace GGJ2025.Systems
{
    public interface IConversationSystem
    {
        void ShowOptions(IEnumerable<ButtonDialogExtended> options);
        void HideOptions();
        void RefreshUI();
    }

    public class AdventureCreatorConversationSystem : IConversationSystem
    {
        public void ShowOptions(IEnumerable<ButtonDialogExtended> options)
        {
            if (KickStarter.playerMenus != null)
            {
                KickStarter.playerMenus.RefreshDialogueOptions();
            }
        }

        public void HideOptions()
        {
            if (KickStarter.playerInput != null)
            {
                KickStarter.playerInput.EndConversation();
            }
        }

        public void RefreshUI()
        {
            if (KickStarter.playerMenus != null)
            {
                KickStarter.playerMenus.RefreshDialogueOptions();
            }
        }
    }
} 